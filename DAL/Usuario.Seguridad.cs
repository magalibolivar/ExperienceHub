using System;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// DAL.Usuario — parcial de SEGURIDAD de la cuenta: bloqueo/desbloqueo (simple y progresivo),
    /// contador de intentos fallidos, gestión de claves y el flag de cambio obligatorio, más la
    /// restauración de una versión histórica. Toda operación que toca campos del DVH recalcula
    /// el dígito verificador (ver Usuario.Integridad.cs).
    /// </summary>
    public partial class Usuario
    {
        // Bloquea la cuenta de un usuario (Estado=0).
        // Se llama tras superar el máximo de intentos fallidos.
        public void Bloquear(int idUsuario)
        {
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET Estado = 0 WHERE IdUsuario = @idUsuario",
                    new SqlParameter[] { new SqlParameter("@idUsuario", idUsuario) });
                RecalcularDVH(idUsuario);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al bloquear el usuario ID {idUsuario}.", ex);
            }
        }

        // Bloqueo PROGRESIVO: marca el bloqueo con timestamp e incrementa la escala (1/5/15/60 min).
        // Reemplaza a Bloquear() en el flujo de login. Si la BD no está migrada, cae a bloqueo simple.
        public void BloquearConTiempo(int idUsuario)
        {
            try
            {
                try
                {
                    acceso.Escribir(
                        "UPDATE Usuario SET Estado = 0, FechaBloqueo = GETDATE(), " +
                        "       CantidadBloqueos = ISNULL(CantidadBloqueos, 0) + 1 " +
                        "WHERE IdUsuario = @idUsuario",
                        new SqlParameter[] { new SqlParameter("@idUsuario", idUsuario) });
                }
                catch (System.Data.SqlClient.SqlException sqlEx)
                    when (sqlEx.Message.Contains("FechaBloqueo") || sqlEx.Message.Contains("CantidadBloqueos"))
                {
                    // BD sin migrar: bloqueo simple permanente (comportamiento anterior).
                    acceso.Escribir(
                        "UPDATE Usuario SET Estado = 0 WHERE IdUsuario = @idUsuario",
                        new SqlParameter[] { new SqlParameter("@idUsuario", idUsuario) });
                }
                RecalcularDVH(idUsuario);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al bloquear (progresivo) el usuario ID {idUsuario}.", ex);
            }
        }

        // Auto-desbloqueo al EXPIRAR el bloqueo temporal: reactiva, limpia intentos y la fecha de
        // bloqueo (pero conserva CantidadBloqueos: la próxima vez el bloqueo dura más).
        public void AutoDesbloquear(int idUsuario)
        {
            try
            {
                try
                {
                    acceso.Escribir(
                        "UPDATE Usuario SET Estado = 1, IntentosFallidos = 0, FechaBloqueo = NULL " +
                        "WHERE IdUsuario = @idUsuario",
                        new SqlParameter[] { new SqlParameter("@idUsuario", idUsuario) });
                }
                catch (System.Data.SqlClient.SqlException sqlEx) when (sqlEx.Message.Contains("FechaBloqueo"))
                {
                    acceso.Escribir(
                        "UPDATE Usuario SET Estado = 1, IntentosFallidos = 0 WHERE IdUsuario = @idUsuario",
                        new SqlParameter[] { new SqlParameter("@idUsuario", idUsuario) });
                }
                RecalcularDVH(idUsuario);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al auto-desbloquear el usuario ID {idUsuario}.", ex);
            }
        }

        // Desbloquea la cuenta de un usuario (Estado=1) y resetea el contador de intentos.
        // Reset COMPLETO (también la escala de bloqueos y la fecha): es una acción explícita del
        // Administrador (o de una clave de emergencia), no un auto-desbloqueo por tiempo.
        public void Desbloquear(int idUsuario)
        {
            try
            {
                try
                {
                    acceso.Escribir(
                        "UPDATE Usuario SET Estado = 1, IntentosFallidos = 0, " +
                        "       CantidadBloqueos = 0, FechaBloqueo = NULL WHERE IdUsuario = @idUsuario",
                        new SqlParameter[] { new SqlParameter("@idUsuario", idUsuario) });
                }
                catch (System.Data.SqlClient.SqlException sqlEx)
                    when (sqlEx.Message.Contains("FechaBloqueo") || sqlEx.Message.Contains("CantidadBloqueos"))
                {
                    acceso.Escribir(
                        "UPDATE Usuario SET Estado = 1, IntentosFallidos = 0 WHERE IdUsuario = @idUsuario",
                        new SqlParameter[] { new SqlParameter("@idUsuario", idUsuario) });
                }
                RecalcularDVH(idUsuario);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al desbloquear el usuario ID {idUsuario}.", ex);
            }
        }

        // Incrementa en 1 el contador de intentos fallidos para el username dado.
        // El contador persiste en BD: sobrevive reinicios de la aplicación.
        public void IncrementarIntentosFallidos(string username)
        {
            SqlParameter[] parametros = new SqlParameter[]
            {
                new SqlParameter("@username", username)
            };
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET IntentosFallidos = ISNULL(IntentosFallidos, 0) + 1 " +
                    "WHERE Username = @username",
                    parametros);
                // T07 — IntentosFallidos forma parte del DVH: recalcular para no dejar la
                // fila "corrupta" y bloquear la app en el próximo arranque.
                RecalcularDVHPorUsername(username);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[DAL.Usuario.IncrementarIntentosFallidos] {ex.Message}");
            }
        }

        // Resetea a 0 el contador de intentos fallidos para el username dado.
        // Se llama tras un login exitoso.
        public void ResetearIntentosFallidos(string username)
        {
            SqlParameter[] parametros = new SqlParameter[]
            {
                new SqlParameter("@username", username)
            };
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET IntentosFallidos = 0 WHERE Username = @username",
                    parametros);
                // T07 — IntentosFallidos forma parte del DVH: recalcular tras el reset.
                RecalcularDVHPorUsername(username);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[DAL.Usuario.ResetearIntentosFallidos] {ex.Message}");
            }
        }

        // Actualiza la contraseña de TODOS los usuarios al hash recibido.
        // Recalcula DVH y DVV para todas las filas afectadas.
        public void ResetearTodasLasClaves(string claveHasheada)
        {
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET Clave = @clave",
                    new SqlParameter[] { new SqlParameter("@clave", claveHasheada) });
                // Reset masivo → todas las cuentas quedan con clave temporal: forzar el cambio.
                SetRequiereCambioClaveTodos(true);
                RecalcularTodosDVH();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al resetear todas las claves de usuario.", ex);
            }
        }

        // Aplica el snapshot de una versión histórica a la fila activa del usuario.
        // SOLO restaura datos administrativos NO sensibles (username/nombre/apellido/fecha nac./email);
        // la contraseña y el estado de bloqueo NUNCA se revierten (no hay rollback de credenciales).
        // El Username forma parte del DVH → recalcular tras el UPDATE.
        public void RestaurarVersion(BE.VersionUsuario v)
        {
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET Username = @Username, Nombre = @Nombre, Apellido = @Apellido, " +
                    "       FechaNacimiento = @FechaNac, Email = @Email WHERE IdUsuario = @Id",
                    new SqlParameter[]
                    {
                        new SqlParameter("@Username", (object)v.UsernameSnapshot ?? DBNull.Value),
                        new SqlParameter("@Nombre",   (object)v.NombreSnapshot   ?? DBNull.Value),
                        new SqlParameter("@Apellido", (object)v.ApellidoSnapshot ?? DBNull.Value),
                        new SqlParameter("@FechaNac", (object)v.FechaNacSnapshot ?? DBNull.Value),
                        new SqlParameter("@Email",    (object)v.EmailSnapshot    ?? DBNull.Value),
                        new SqlParameter("@Id",       v.IdUsuario)
                    });
                RecalcularDVH(v.IdUsuario);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al restaurar la versión del usuario ID {v.IdUsuario}.", ex);
            }
        }

        // Actualiza la contraseña de un usuario existente (ya hasheada por la BLL).
        public void ResetearClave(int idUsuario, string claveHasheada)
        {
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET Clave = @clave WHERE IdUsuario = @idUsuario",
                    new SqlParameter[]
                    {
                        new SqlParameter("@clave",     claveHasheada),
                        new SqlParameter("@idUsuario", idUsuario)
                    });
                // Clave reseteada por un admin → el usuario debe cambiarla en su próximo login.
                SetRequiereCambioClave(idUsuario, true);
                RecalcularDVH(idUsuario);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al resetear la clave del usuario ID {idUsuario}.", ex);
            }
        }

        // Cambio de clave por el PROPIO usuario (clave ya hasheada por la BLL).
        // Persiste la nueva clave, baja el flag RequiereCambioClave y recalcula el DVH.
        public void CambiarClave(int idUsuario, string claveHasheada)
        {
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET Clave = @clave WHERE IdUsuario = @idUsuario",
                    new SqlParameter[]
                    {
                        new SqlParameter("@clave",     claveHasheada),
                        new SqlParameter("@idUsuario", idUsuario)
                    });
                // El usuario ya eligió su propia clave → deja de ser temporal.
                SetRequiereCambioClave(idUsuario, false);
                RecalcularDVH(idUsuario);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al cambiar la clave del usuario ID {idUsuario}.", ex);
            }
        }

        // Marca/desmarca el flag de cambio obligatorio para un usuario. Tolerante a BD sin migrar:
        // si la columna no existe, se ignora (la función simplemente no aplica). El flag NO entra
        // al DVH, por lo que cambiarlo no requiere recalcular dígitos verificadores.
        private void SetRequiereCambioClave(int idUsuario, bool requiere)
        {
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET RequiereCambioClave = @r WHERE IdUsuario = @id",
                    new SqlParameter[]
                    {
                        new SqlParameter("@r",  requiere ? 1 : 0),
                        new SqlParameter("@id", idUsuario)
                    });
            }
            catch (System.Data.SqlClient.SqlException ex) when (ex.Message.Contains("RequiereCambioClave"))
            {
                // BD sin migrar (falta la columna): el cambio obligatorio queda inactivo. No es crítico.
                System.Diagnostics.Trace.TraceWarning(
                    "[DAL.Usuario.SetRequiereCambioClave] Columna RequiereCambioClave ausente; ejecutá 02_Actualizar.");
            }
        }

        // Igual que el anterior pero para TODOS los usuarios (tras un reset masivo de claves).
        private void SetRequiereCambioClaveTodos(bool requiere)
        {
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET RequiereCambioClave = @r",
                    new SqlParameter[] { new SqlParameter("@r", requiere ? 1 : 0) });
            }
            catch (System.Data.SqlClient.SqlException ex) when (ex.Message.Contains("RequiereCambioClave"))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "[DAL.Usuario.SetRequiereCambioClaveTodos] Columna RequiereCambioClave ausente; ejecutá 02_Actualizar.");
            }
        }
    }
}

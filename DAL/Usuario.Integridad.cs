using System;

namespace DAL
{
    /// <summary>
    /// DAL.Usuario — parcial de INTEGRIDAD (T07): recálculo de los dígitos verificadores de la
    /// tabla Usuario. Tras cualquier escritura sobre una fila, su DVH se recalcula y se reporta el
    /// nuevo estado legítimo al EspejoUsuario; el DVV de la tabla se reconstruye a partir de los DVH.
    /// Estos métodos son privados y los invocan los parciales de ABM y de seguridad.
    /// </summary>
    public partial class Usuario
    {
        // Recalcula el DVH de un usuario específico y actualiza DVV de la tabla.
        // Se llama después de cualquier operación de escritura sobre un usuario.
        private void RecalcularDVH(int idUsuario)
        {
            try
            {
                var dvDAL = new DigitoVerificador();
                var filas = dvDAL.ObtenerFilasUsuario();

                // Buscar la fila del usuario modificado y recalcular su DVH
                var svc = Seguridad.CalculadorDV.Crear();
                foreach (var fila in filas)
                {
                    if (fila.Id == idUsuario)
                    {
                        int dvh = svc.CalcularDVH(fila.CamposParaDVH());
                        dvDAL.ActualizarDVH(idUsuario, dvh);
                        fila.DVHAlmacenado = dvh;
                        // T07 — Espejo de integridad: registrar el nuevo estado legítimo de esta fila.
                        new EspejoUsuario().Upsert(fila);
                        break;
                    }
                }

                // Recalcular DVV con todos los DVH (usar los recién leídos — pueden ser null para filas antiguas)
                ActualizarDVV(dvDAL);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[DAL.Usuario.RecalcularDVH] {ex.Message}");
            }
        }

        // Igual que RecalcularDVH(int) pero resolviendo la fila por Username (los métodos de
        // intentos de login operan por username y no tienen el IdUsuario a mano).
        private void RecalcularDVHPorUsername(string username)
        {
            try
            {
                var dvDAL = new DigitoVerificador();
                var filas = dvDAL.ObtenerFilasUsuario();
                var svc   = Seguridad.CalculadorDV.Crear();
                foreach (var fila in filas)
                {
                    if (string.Equals(fila.Username, username, StringComparison.OrdinalIgnoreCase))
                    {
                        int dvh = svc.CalcularDVH(fila.CamposParaDVH());
                        dvDAL.ActualizarDVH(fila.Id, dvh);
                        fila.DVHAlmacenado = dvh;
                        // T07 — Espejo de integridad: registrar el nuevo estado legítimo de esta fila.
                        new EspejoUsuario().Upsert(fila);
                        break;
                    }
                }
                ActualizarDVV(dvDAL);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[DAL.Usuario.RecalcularDVHPorUsername] {ex.Message}");
            }
        }

        // Recalcula el DVH de TODOS los usuarios y actualiza DVV.
        // Se usa después de operaciones masivas (ResetearTodasLasClaves).
        private void RecalcularTodosDVH()
        {
            try
            {
                var dvDAL = new DigitoVerificador();
                var filas = dvDAL.ObtenerFilasUsuario();
                var svc   = Seguridad.CalculadorDV.Crear();

                foreach (var fila in filas)
                {
                    int dvh = svc.CalcularDVH(fila.CamposParaDVH());
                    dvDAL.ActualizarDVH(fila.Id, dvh);
                    fila.DVHAlmacenado = dvh;
                }

                ActualizarDVV(dvDAL);
                // T07 — Cambió el conjunto de filas (p. ej. baja física o reset masivo): el espejo
                // de integridad se reconstruye completo para reflejar el nuevo estado legítimo.
                new EspejoUsuario().Reconstruir(filas);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[DAL.Usuario.RecalcularTodosDVH] {ex.Message}");
            }
        }

        // Recalcula y persiste el DVV de la tabla Usuario a partir de los DVH actuales.
        private static void ActualizarDVV(DigitoVerificador dvDAL)
        {
            var filas = dvDAL.ObtenerFilasUsuario();
            var svc   = Seguridad.CalculadorDV.Crear();

            var dvhValues = new System.Collections.Generic.List<int>();
            foreach (var fila in filas)
                dvhValues.Add(fila.DVHAlmacenado ?? 0);

            int dvv = svc.CalcularDVV(dvhValues);
            dvDAL.GuardarDVV("Usuario", dvv);
        }
    }
}

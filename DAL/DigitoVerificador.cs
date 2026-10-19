using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Capa de Acceso a Datos — T07 Dígitos Verificadores.
    ///
    /// Opera sobre:
    ///   [Usuario].DVH      — dígito verificador horizontal por fila
    ///   [DVVertical]       — tabla de dígitos verificadores verticales por tabla
    ///
    /// Schema esperado (aplicar ExperienceHubDB_Alter_v3.0.sql antes de usar):
    ///   ALTER TABLE Usuario ADD DVH INT NULL
    ///   CREATE TABLE DVVertical (Id INT IDENTITY PK, NombreTabla VARCHAR(100) UNIQUE,
    ///                            DVV INT NOT NULL, FechaCalculo DATETIME NOT NULL)
    /// </summary>
    public class DigitoVerificador
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        // Lee el DVV almacenado para una tabla. Retorna null si no existe registro.
        public int? ObtenerDVV(string nombreTabla)
        {
            try
            {
                DataTable tabla = acceso.Leer(
                    "SELECT DVV FROM DVVertical WHERE NombreTabla = @tabla",
                    new SqlParameter[] { new SqlParameter("@tabla", nombreTabla) });

                if (tabla == null || tabla.Rows.Count == 0) return null;
                return Convert.ToInt32(tabla.Rows[0]["DVV"]);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener DVV de la tabla '{nombreTabla}'.", ex);
            }
        }

        // ── Marcador de re-sellado por tabla (__Reseal:<Tabla>) ─────────────────
        // Lo setea una MIGRACIÓN para autorizar que la verificación de integridad (re)selle el DVH/DVV
        // de una tabla (inicialización en instalación limpia o cambio de formato del DVH). Un DVH en
        // NULL o una discrepancia SIN este marcador se tratan como corrupción (no se re-sella solo).
        private const string ResealPrefix = "__Reseal:";

        /// <summary>True si hay un marcador de re-sellado pendiente para la tabla.</summary>
        public bool TieneMarcadorReseal(string nombreTabla) => ObtenerDVV(ResealPrefix + nombreTabla) != null;

        /// <summary>Setea el marcador de re-sellado (lo usa la migración).</summary>
        public void MarcarReseal(string nombreTabla) => GuardarDVV(ResealPrefix + nombreTabla, 1);

        /// <summary>Consume (borra) el marcador de re-sellado tras re-sellar la tabla.</summary>
        public void ConsumirMarcadorReseal(string nombreTabla) => EliminarDVV(ResealPrefix + nombreTabla);

        /// <summary>Borra la fila de DVVertical de una tabla/marcador.</summary>
        public void EliminarDVV(string nombreTabla)
        {
            try
            {
                acceso.Escribir("DELETE FROM DVVertical WHERE NombreTabla = @tabla",
                    new SqlParameter[] { new SqlParameter("@tabla", nombreTabla) });
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al eliminar DVV de '{nombreTabla}'.", ex);
            }
        }

        // Almacena (upsert) el DVV calculado para una tabla junto con la fecha.
        public void GuardarDVV(string nombreTabla, int dvv)
        {
            try
            {
                acceso.Escribir(
                    "IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla = @tabla) " +
                    "    UPDATE DVVertical SET DVV = @dvv, FechaCalculo = GETDATE() WHERE NombreTabla = @tabla " +
                    "ELSE " +
                    "    INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES (@tabla, @dvv, GETDATE())",
                    new SqlParameter[]
                    {
                        new SqlParameter("@tabla", nombreTabla),
                        new SqlParameter("@dvv",   dvv)
                    });
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al guardar DVV de la tabla '{nombreTabla}'.", ex);
            }
        }

        // Lee todas las filas de Usuario con sus DVH almacenados, ordenadas por IdUsuario.
        // Retorna lista de (id, username, clave, perfil, estado, intentos, dvhAlmacenado).
        public List<BE.FilaUsuarioDV> ObtenerFilasUsuario()
        {
            var lista = new List<BE.FilaUsuarioDV>();
            try
            {
                DataTable tabla = acceso.Leer(
                    "SELECT IdUsuario, Username, Clave, Rol, Perfil, Estado, IntentosFallidos, DVH " +
                    "FROM Usuario ORDER BY IdUsuario",
                    null);

                if (tabla == null) return lista;

                foreach (DataRow row in tabla.Rows)
                {
                    lista.Add(new BE.FilaUsuarioDV
                    {
                        Id               = Convert.ToInt32(row["IdUsuario"]),
                        Username         = row["Username"].ToString(),
                        Clave            = row["Clave"].ToString(),
                        Rol              = row["Rol"] != DBNull.Value ? row["Rol"].ToString() : "",
                        Perfil           = row["Perfil"] != DBNull.Value ? row["Perfil"].ToString() : "",
                        Estado           = row["Estado"] != DBNull.Value ? Convert.ToInt32(row["Estado"]).ToString() : "0",
                        IntentosFallidos = row["IntentosFallidos"] != DBNull.Value ? Convert.ToInt32(row["IntentosFallidos"]).ToString() : "0",
                        DVHAlmacenado    = row["DVH"] != DBNull.Value ? (int?)Convert.ToInt32(row["DVH"]) : null
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener filas de Usuario para verificación DV.", ex);
            }
            return lista;
        }

        // Actualiza el DVH de un usuario específico.
        public void ActualizarDVH(int idUsuario, int dvh)
        {
            try
            {
                acceso.Escribir(
                    "UPDATE Usuario SET DVH = @dvh WHERE IdUsuario = @id",
                    new SqlParameter[]
                    {
                        new SqlParameter("@dvh", dvh),
                        new SqlParameter("@id",  idUsuario)
                    });
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al actualizar DVH del usuario ID {idUsuario}.", ex);
            }
        }

        // ── DV GENÉRICO (reutilizable para cualquier tabla protegida) ───────────
        // tabla / pkCol / columnas son CONSTANTES del sistema (no entradas de usuario).
        // Como acá los identificadores NO pueden ir parametrizados (SQL no admite @param
        // para nombres de tabla/columna), se aplica defensa en profundidad: cada identificador
        // se VALIDA contra una lista blanca de identificadores simples y se encierra en
        // corchetes [ ], de modo que sea imposible inyectar SQL aunque una constante cambie.

        // Valida que el identificador sea un nombre simple ([A-Za-z_][A-Za-z0-9_]*) y lo
        // devuelve entre corchetes. Lanza si no cumple — falla cerrado, no concatena algo dudoso.
        private static string Id(string identificador)
        {
            if (string.IsNullOrEmpty(identificador))
                throw new ArgumentException("Identificador SQL vacío.");
            foreach (char c in identificador)
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    throw new ArgumentException($"Identificador SQL inválido: '{identificador}'.");
            if (char.IsDigit(identificador[0]))
                throw new ArgumentException($"Identificador SQL inválido: '{identificador}'.");
            return "[" + identificador + "]";
        }

        // Lee las filas de una tabla con sus campos relevantes para el DVH y el DVH almacenado.
        public List<BE.FilaDV> ObtenerFilas(string tabla, string pkCol, string[] columnas)
        {
            var colsQuoted = new string[columnas.Length];
            for (int i = 0; i < columnas.Length; i++) colsQuoted[i] = Id(columnas[i]);
            string cols = string.Join(", ", colsQuoted);
            DataTable dt = acceso.Leer(
                "SELECT " + Id(pkCol) + ", " + cols + ", DVH FROM " + Id(tabla) + " ORDER BY " + Id(pkCol), null);

            var lista = new List<BE.FilaDV>();
            if (dt == null) return lista;

            foreach (DataRow row in dt.Rows)
            {
                var campos = new string[columnas.Length + 1];
                campos[0] = row[pkCol].ToString();                // la PK entra al DVH
                for (int i = 0; i < columnas.Length; i++)
                    campos[i + 1] = row[columnas[i]] == DBNull.Value ? "" : row[columnas[i]].ToString();

                lista.Add(new BE.FilaDV
                {
                    Id            = Convert.ToInt32(row[pkCol]),
                    Campos        = campos,
                    DVHAlmacenado = row["DVH"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["DVH"]),
                    Descripcion   = tabla + " #" + row[pkCol]
                });
            }
            return lista;
        }

        // Actualiza el DVH de una fila de cualquier tabla.
        public void ActualizarDVH(string tabla, string pkCol, int id, int dvh)
        {
            acceso.Escribir(
                "UPDATE " + Id(tabla) + " SET DVH = @dvh WHERE " + Id(pkCol) + " = @id",
                new SqlParameter[] { new SqlParameter("@dvh", dvh), new SqlParameter("@id", id) });
        }

        // Recalcula y persiste el DVH de cada fila + el DVV de la tabla DENTRO de una transacción
        // YA abierta (conn/tx). Permite que una operación multi-tabla (p. ej. la cancelación de una
        // experiencia en cascada) recalcule los dígitos verificadores como parte de la MISMA
        // transacción atómica, en lugar de depender de la auto-reparación posterior al arranque.
        internal static void RecalcularTablaEnTransaccion(SqlConnection conn, SqlTransaction tx,
                                                          string tabla, string pkCol, string[] columnas)
        {
            var svc = Seguridad.CalculadorDV.Crear();

            var colsQuoted = new string[columnas.Length];
            for (int i = 0; i < columnas.Length; i++) colsQuoted[i] = Id(columnas[i]);
            string cols = string.Join(", ", colsQuoted);

            // 1) Leer filas (PK + columnas) dentro de la transacción.
            var filas = new List<(int Id, string[] Campos)>();
            using (var cmd = new SqlCommand(
                "SELECT " + Id(pkCol) + ", " + cols + " FROM " + Id(tabla) + " ORDER BY " + Id(pkCol), conn, tx))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    var campos = new string[columnas.Length + 1];
                    campos[0] = rd[0].ToString();                 // la PK entra al DVH
                    for (int i = 0; i < columnas.Length; i++)
                        campos[i + 1] = rd.IsDBNull(i + 1) ? "" : rd[i + 1].ToString();
                    filas.Add((Convert.ToInt32(rd[0]), campos));
                }
            }

            // 2) Recalcular y actualizar el DVH de cada fila.
            var dvhs = new List<int>();
            foreach (var f in filas)
            {
                int dvh = svc.CalcularDVH(f.Campos);
                dvhs.Add(dvh);
                using (var upd = new SqlCommand(
                    "UPDATE " + Id(tabla) + " SET DVH=@dvh WHERE " + Id(pkCol) + "=@id", conn, tx))
                {
                    upd.Parameters.AddWithValue("@dvh", dvh);
                    upd.Parameters.AddWithValue("@id", f.Id);
                    upd.ExecuteNonQuery();
                }
            }

            // 3) Upsert del DVV de la tabla.
            int dvv = svc.CalcularDVV(dvhs);
            using (var dvCmd = new SqlCommand(
                "IF EXISTS (SELECT 1 FROM DVVertical WHERE NombreTabla=@t) " +
                "   UPDATE DVVertical SET DVV=@v, FechaCalculo=GETDATE() WHERE NombreTabla=@t " +
                "ELSE " +
                "   INSERT INTO DVVertical (NombreTabla, DVV, FechaCalculo) VALUES (@t,@v,GETDATE())", conn, tx))
            {
                dvCmd.Parameters.AddWithValue("@t", tabla);
                dvCmd.Parameters.AddWithValue("@v", dvv);
                dvCmd.ExecuteNonQuery();
            }
        }

        // Recalcula y persiste el DVH de cada fila + el DVV de la tabla.
        // Debe llamarse después de cualquier escritura sobre la tabla protegida.
        public void RecalcularTabla(string tabla, string pkCol, string[] columnas)
        {
            var svc   = Seguridad.CalculadorDV.Crear();
            var filas = ObtenerFilas(tabla, pkCol, columnas);
            var dvhs  = new List<int>();
            foreach (var f in filas)
            {
                int dvh = svc.CalcularDVH(f.Campos);
                ActualizarDVH(tabla, pkCol, f.Id, dvh);
                dvhs.Add(dvh);
            }
            GuardarDVV(tabla, svc.CalcularDVV(dvhs));
        }
    }
}

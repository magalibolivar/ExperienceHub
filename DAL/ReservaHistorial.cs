using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Acceso a datos para la tabla [ReservaHistorial].
    /// Registra lotes de cambios agrupados por IdOperacion y consulta el historial de una reserva.
    /// </summary>
    public class ReservaHistorial
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        private const string INSERT_SQL =
            "INSERT INTO ReservaHistorial " +
            "(IdReserva, IdOperacion, Fecha, IdUsuario, NombreUsuario, Accion, Campo, ValorAnterior, ValorNuevo) " +
            "VALUES (@IdReserva, @IdOperacion, @Fecha, @IdUsuario, @NombreUsuario, @Accion, @Campo, @ValorAnterior, @ValorNuevo)";

        public void RegistrarCambios(List<BE.ReservaHistorial> cambios)
        {
            if (cambios == null || cambios.Count == 0) return;
            acceso.EjecutarTransaccion((conn, tx) => InsertarEnTransaccion(conn, tx, cambios));
        }

        /// <summary>
        /// Inserta un lote de registros de historial usando una conexión/transacción YA abiertas.
        /// Permite que la escritura del historial comparta la transacción de otra operación
        /// (p. ej. la creación de la reserva en <see cref="Reserva.CrearConCupo"/>), de modo que
        /// la reserva y su registro de historial sean atómicos: o se graban ambos, o ninguno.
        /// </summary>
        internal static void InsertarEnTransaccion(SqlConnection conn, SqlTransaction tx,
                                                   List<BE.ReservaHistorial> cambios)
        {
            if (cambios == null || cambios.Count == 0) return;

            foreach (var c in cambios)
                using (var cmd = new SqlCommand(INSERT_SQL, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@IdReserva",     c.IdReserva);
                    cmd.Parameters.AddWithValue("@IdOperacion",   c.IdOperacion);
                    cmd.Parameters.AddWithValue("@Fecha",         c.Fecha);
                    cmd.Parameters.AddWithValue("@IdUsuario",     (object)c.IdUsuario     ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NombreUsuario", (object)c.NombreUsuario ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Accion",        c.Accion);
                    cmd.Parameters.AddWithValue("@Campo",         c.Campo);
                    cmd.Parameters.AddWithValue("@ValorAnterior", (object)c.ValorAnterior ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ValorNuevo",    (object)c.ValorNuevo    ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
        }

        public int ObtenerSiguienteIdOperacion(int idReserva)
        {
            var dt = acceso.Leer(
                "SELECT ISNULL(MAX(IdOperacion), 0) + 1 AS Siguiente FROM ReservaHistorial WHERE IdReserva = @Id",
                new[] { new SqlParameter("@Id", idReserva) });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Siguiente"]) : 1;
        }

        public DataTable ObtenerPorReserva(int idReserva, string accion = null,
                                           DateTime? desde = null, DateTime? hasta = null)
        {
            var cond = new System.Text.StringBuilder("WHERE IdReserva = @IdReserva");
            var p = new List<SqlParameter> { new SqlParameter("@IdReserva", idReserva) };

            if (!string.IsNullOrEmpty(accion)) { cond.Append(" AND Accion = @Accion"); p.Add(new SqlParameter("@Accion", accion)); }
            if (desde.HasValue) { cond.Append(" AND Fecha >= @Desde"); p.Add(new SqlParameter("@Desde", desde.Value)); }
            if (hasta.HasValue) { cond.Append(" AND Fecha <= @Hasta"); p.Add(new SqlParameter("@Hasta", hasta.Value.AddDays(1))); }

            return acceso.Leer(
                "SELECT IdHistorial, IdOperacion, Fecha, NombreUsuario, Accion, Campo, ValorAnterior, ValorNuevo " +
                "FROM ReservaHistorial " + cond + " ORDER BY Fecha DESC, IdHistorial DESC",
                p.ToArray());
        }

        public List<BE.ReservaHistorial> ObtenerPorOperacion(int idReserva, int idOperacion)
        {
            var lista = new List<BE.ReservaHistorial>();
            var dt = acceso.Leer(
                "SELECT IdHistorial, IdReserva, IdOperacion, Fecha, IdUsuario, NombreUsuario, Accion, Campo, ValorAnterior, ValorNuevo " +
                "FROM ReservaHistorial WHERE IdReserva = @IdReserva AND IdOperacion = @IdOperacion",
                new[] { new SqlParameter("@IdReserva", idReserva), new SqlParameter("@IdOperacion", idOperacion) });
            foreach (DataRow row in dt.Rows) lista.Add(Mapear(row));
            return lista;
        }

        private BE.ReservaHistorial Mapear(DataRow row) => new BE.ReservaHistorial
        {
            IdHistorial   = Convert.ToInt32(row["IdHistorial"]),
            IdReserva     = Convert.ToInt32(row["IdReserva"]),
            IdOperacion   = Convert.ToInt32(row["IdOperacion"]),
            Fecha         = Convert.ToDateTime(row["Fecha"]),
            IdUsuario     = row["IdUsuario"]     != DBNull.Value ? (int?)Convert.ToInt32(row["IdUsuario"]) : null,
            NombreUsuario = row["NombreUsuario"] != DBNull.Value ? row["NombreUsuario"].ToString() : null,
            Accion        = row["Accion"].ToString(),
            Campo         = row["Campo"].ToString(),
            ValorAnterior = row["ValorAnterior"] != DBNull.Value ? row["ValorAnterior"].ToString() : null,
            ValorNuevo    = row["ValorNuevo"]    != DBNull.Value ? row["ValorNuevo"].ToString()    : null
        };
    }
}

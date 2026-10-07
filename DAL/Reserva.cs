using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Capa de Acceso a Datos — Reserva. Opera sobre la tabla [Reserva].
    /// La creación y cancelación ajustan el CupoDisponible de la Experiencia
    /// de forma ATÓMICA (misma transacción), para no dejar cupos inconsistentes.
    /// </summary>
    public class Reserva : BaseDAL<BE.Reserva>
    {
        // T07 — Dígito Verificador de esta tabla (fuente única).
        public const  string   DV_Tabla    = "Reserva";
        public const  string   DV_Pk       = "IdReserva";
        public static readonly string[] DV_Columnas = { "IdCliente", "IdExperiencia", "Estado", "CantidadInvitados" };

        public void RecalcularDV()
        {
            try { new DigitoVerificador().RecalcularTabla(DV_Tabla, DV_Pk, DV_Columnas); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("[DAL.Reserva.RecalcularDV] " + ex.Message); }
        }

        private const string SELECT_BASE =
            "SELECT r.IdReserva, r.IdCliente, r.IdExperiencia, r.IdEmpleado, r.Estado, r.FechaReserva, " +
            "       r.CantidadInvitados, r.FechaCancelacion, r.MotivoCancelacion, " +
            "       c.Nombre + ' ' + c.Apellido AS NombreCliente, e.Nombre AS NombreExperiencia, " +
            "       (CAST(e.Fecha AS DATETIME) + CAST(e.HoraInicio AS DATETIME)) AS FechaHoraExperiencia, " +
            "       e.DuracionMinutos AS DuracionExperiencia, " +
            "       emp.Nombre + ' ' + emp.Apellido AS NombreEmpleado " +
            "FROM Reserva r " +
            "LEFT JOIN Cliente     c   ON c.IdCliente      = r.IdCliente " +
            "LEFT JOIN Experiencia e   ON e.IdExperiencia  = r.IdExperiencia " +
            "LEFT JOIN Empleado    emp ON emp.IdEmpleado   = r.IdEmpleado ";

        public override List<BE.Reserva> ObtenerTodos()
        {
            var lista = new List<BE.Reserva>();
            try
            {
                DataTable tabla = acceso.Leer(SELECT_BASE + "ORDER BY r.FechaReserva DESC", null);
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener reservas.", ex); }
            return lista;
        }

        public override BE.Reserva ObtenerPorId(int idReserva)
        {
            DataTable tabla = acceso.Leer(SELECT_BASE + "WHERE r.IdReserva = @Id",
                new[] { new SqlParameter("@Id", idReserva) });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        public List<BE.Reserva> ObtenerPorCliente(int idCliente)
        {
            var lista = new List<BE.Reserva>();
            DataTable tabla = acceso.Leer(
                SELECT_BASE + "WHERE r.IdCliente = @IdCliente ORDER BY r.FechaReserva DESC",
                new[] { new SqlParameter("@IdCliente", idCliente) });
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>
        /// Reservas activas (Pendiente/Confirmada) del cliente con el horario de su experiencia,
        /// para validar solapamiento de horarios (regla 2).
        /// </summary>
        public List<BE.Reserva> ObtenerActivasConHorario(int idCliente)
        {
            var lista = new List<BE.Reserva>();
            DataTable tabla = acceso.Leer(
                SELECT_BASE +
                "WHERE r.IdCliente = @IdCliente AND r.Estado IN (@Pend, @Conf) " +
                "ORDER BY FechaHoraExperiencia",
                new[]
                {
                    new SqlParameter("@IdCliente", idCliente),
                    new SqlParameter("@Pend", (object)(int)BE.EstadoReserva.Pendiente),
                    new SqlParameter("@Conf", (object)(int)BE.EstadoReserva.Confirmada)
                });
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        // ── Fidelización / recomendaciones ──────────────────────────────────────

        /// <summary>Cantidad de experiencias a las que el cliente efectivamente asistió (fidelidad).</summary>
        public int ContarAsistencias(int idCliente)
        {
            var dt = acceso.Leer(
                "SELECT COUNT(*) AS Total FROM Reserva WHERE IdCliente=@Id AND Estado=@Asistio",
                new[]
                {
                    new SqlParameter("@Id", idCliente),
                    new SqlParameter("@Asistio", (object)(int)BE.EstadoReserva.Asistio)
                });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Total"]) : 0;
        }

        /// <summary>
        /// Historial de categorías del cliente: IdCategoria → cantidad de reservas no canceladas
        /// (Asistió/Confirmada) de esa categoría. Insumo para puntuar las recomendaciones.
        /// </summary>
        public Dictionary<int, int> ContarPorCategoriaDelHistorial(int idCliente)
        {
            var mapa = new Dictionary<int, int>();
            DataTable dt = acceso.Leer(
                "SELECT e.IdCategoria, COUNT(*) AS Cant " +
                "FROM Reserva r JOIN Experiencia e ON e.IdExperiencia = r.IdExperiencia " +
                "WHERE r.IdCliente=@Id AND r.Estado IN (@Asistio, @Conf) " +
                "GROUP BY e.IdCategoria",
                new[]
                {
                    new SqlParameter("@Id", idCliente),
                    new SqlParameter("@Asistio", (object)(int)BE.EstadoReserva.Asistio),
                    new SqlParameter("@Conf", (object)(int)BE.EstadoReserva.Confirmada)
                });
            foreach (DataRow row in dt.Rows)
                mapa[Convert.ToInt32(row["IdCategoria"])] = Convert.ToInt32(row["Cant"]);
            return mapa;
        }

        /// <summary>Reservas Asistió del cliente que todavía NO fueron calificadas (seguimiento post-experiencia).</summary>
        public List<BE.Reserva> ObtenerAsistidasSinCalificar(int idCliente)
        {
            var lista = new List<BE.Reserva>();
            DataTable tabla = acceso.Leer(
                SELECT_BASE +
                "WHERE r.IdCliente=@Id AND r.Estado=@Asistio " +
                "AND NOT EXISTS (SELECT 1 FROM Calificacion c WHERE c.IdReserva = r.IdReserva) " +
                "ORDER BY r.FechaReserva DESC",
                new[]
                {
                    new SqlParameter("@Id", idCliente),
                    new SqlParameter("@Asistio", (object)(int)BE.EstadoReserva.Asistio)
                });
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>True si el cliente ya asistió antes a esa experiencia (regla 5).</summary>
        public bool ExisteAsistenciaPrevia(int idCliente, int idExperiencia)
        {
            var dt = acceso.Leer(
                "SELECT COUNT(*) AS Total FROM Reserva " +
                "WHERE IdCliente=@IdCliente AND IdExperiencia=@IdExp AND Estado=@Asistio",
                new[]
                {
                    new SqlParameter("@IdCliente", idCliente),
                    new SqlParameter("@IdExp", idExperiencia),
                    new SqlParameter("@Asistio", (object)(int)BE.EstadoReserva.Asistio)
                });
            return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Total"]) > 0;
        }

        /// <summary>
        /// Crea la reserva y descuenta el cupo de la experiencia en la MISMA transacción.
        /// Si el cupo ya no alcanza, revierte y lanza excepción. Devuelve el Id generado.
        ///
        /// Opcionalmente, dentro de la MISMA transacción atómica:
        ///   • <paramref name="idSuscripcionAConsumir"/> &gt; 0 → descuenta el cupo mensual del plan
        ///     (ReservasConsumidasMes + 1), para que una reserva no pueda quedar creada sin consumir
        ///     el beneficio del cliente.
        ///   • <paramref name="historial"/> → graba los registros de historial de la operación CREAR
        ///     (se les estampa el Id recién generado), de modo que la reserva y su historial sean
        ///     indivisibles: o se graban ambos, o ninguno.
        /// </summary>
        public int CrearConCupo(BE.Reserva reserva, int lugares,
                                int idSuscripcionAConsumir = 0,
                                List<BE.ReservaHistorial> historial = null)
        {
            int idNuevo = 0;
            acceso.EjecutarTransaccion((conn, tx) =>
            {
                using (var cupo = new SqlCommand(
                    "UPDATE Experiencia SET CupoDisponible = CupoDisponible - @Lugares " +
                    "WHERE IdExperiencia = @IdExp AND CupoDisponible >= @Lugares", conn, tx))
                {
                    cupo.Parameters.AddWithValue("@Lugares", lugares);
                    cupo.Parameters.AddWithValue("@IdExp", reserva.IdExperiencia);
                    if (cupo.ExecuteNonQuery() == 0)
                        throw new BE.AppException("err.bll.reserva.sin_cupo",
                            "La experiencia ya no tiene cupos suficientes.");
                }

                // Estado automático: si el cupo llegó a 0, la experiencia pasa a Completa.
                using (var est = new SqlCommand(
                    "UPDATE Experiencia SET Estado = @Completa " +
                    "WHERE IdExperiencia = @IdExp AND CupoDisponible <= 0 AND Estado = @Programada", conn, tx))
                {
                    est.Parameters.AddWithValue("@Completa", (int)BE.EstadoExperiencia.Completa);
                    est.Parameters.AddWithValue("@Programada", (int)BE.EstadoExperiencia.Programada);
                    est.Parameters.AddWithValue("@IdExp", reserva.IdExperiencia);
                    est.ExecuteNonQuery();
                }

                using (var ins = new SqlCommand(
                    "INSERT INTO Reserva (IdCliente, IdExperiencia, IdEmpleado, Estado, FechaReserva, CantidadInvitados) " +
                    "VALUES (@IdCliente, @IdExp, @IdEmpleado, @Estado, @Fecha, @Invitados); " +
                    "SELECT CAST(SCOPE_IDENTITY() AS INT)", conn, tx))
                {
                    ins.Parameters.AddWithValue("@IdCliente", reserva.IdCliente);
                    ins.Parameters.AddWithValue("@IdExp", reserva.IdExperiencia);
                    // IdEmpleado es FK nullable: 0 = "sin empleado" → NULL (evita violar la FK).
                    ins.Parameters.AddWithValue("@IdEmpleado", reserva.IdEmpleado > 0 ? (object)reserva.IdEmpleado : DBNull.Value);
                    ins.Parameters.AddWithValue("@Estado", (int)reserva.Estado);
                    ins.Parameters.AddWithValue("@Fecha", reserva.FechaReserva);
                    ins.Parameters.AddWithValue("@Invitados", reserva.CantidadInvitados);
                    idNuevo = (int)ins.ExecuteScalar();
                }

                // Consumo del cupo mensual del plan — en la MISMA transacción.
                if (idSuscripcionAConsumir > 0)
                {
                    using (var cons = new SqlCommand(
                        "UPDATE Suscripcion SET ReservasConsumidasMes = ReservasConsumidasMes + 1 " +
                        "WHERE IdSuscripcion = @IdSus", conn, tx))
                    {
                        cons.Parameters.AddWithValue("@IdSus", idSuscripcionAConsumir);
                        cons.ExecuteNonQuery();
                    }
                }

                // Historial de la operación CREAR — en la MISMA transacción (se estampa el Id generado).
                if (historial != null && historial.Count > 0)
                {
                    foreach (var h in historial) h.IdReserva = idNuevo;
                    ReservaHistorial.InsertarEnTransaccion(conn, tx, historial);
                }
            });
            RecalcularDV();
            return idNuevo;
        }

        public void Confirmar(int idReserva)
        {
            acceso.Escribir("UPDATE Reserva SET Estado=@Estado WHERE IdReserva=@Id",
                new[] { new SqlParameter("@Estado", (object)(int)BE.EstadoReserva.Confirmada), new SqlParameter("@Id", idReserva) });
            RecalcularDV();
        }

        /// <summary>Registra Asistio o NoAsistio.</summary>
        public void RegistrarAsistencia(int idReserva, BE.EstadoReserva estado)
        {
            acceso.Escribir("UPDATE Reserva SET Estado=@Estado WHERE IdReserva=@Id",
                new[] { new SqlParameter("@Estado", (int)estado), new SqlParameter("@Id", idReserva) });
            RecalcularDV();
        }

        /// <summary>
        /// Cancela la reserva. Si <paramref name="liberarCupo"/> es true (cancelación
        /// anticipada), devuelve los lugares al cupo de la experiencia en la misma transacción.
        /// </summary>
        public void Cancelar(int idReserva, int idExperiencia, int lugares, string motivo, bool liberarCupo)
        {
            acceso.EjecutarTransaccion((conn, tx) =>
            {
                using (var upd = new SqlCommand(
                    "UPDATE Reserva SET Estado=@Estado, FechaCancelacion=@Fecha, MotivoCancelacion=@Motivo " +
                    "WHERE IdReserva=@Id", conn, tx))
                {
                    upd.Parameters.AddWithValue("@Estado", (int)BE.EstadoReserva.Cancelada);
                    upd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                    upd.Parameters.AddWithValue("@Motivo", (object)motivo ?? DBNull.Value);
                    upd.Parameters.AddWithValue("@Id", idReserva);
                    upd.ExecuteNonQuery();
                }
                if (liberarCupo)
                {
                    using (var cupo = new SqlCommand(
                        "UPDATE Experiencia SET CupoDisponible = " +
                        "CASE WHEN CupoDisponible + @Lugares <= CupoMaximo THEN CupoDisponible + @Lugares ELSE CupoMaximo END " +
                        "WHERE IdExperiencia=@IdExp", conn, tx))
                    {
                        cupo.Parameters.AddWithValue("@Lugares", lugares);
                        cupo.Parameters.AddWithValue("@IdExp", idExperiencia);
                        cupo.ExecuteNonQuery();
                    }
                    // Al liberarse un cupo, si estaba Completa vuelve a Programada.
                    using (var est = new SqlCommand(
                        "UPDATE Experiencia SET Estado = @Programada " +
                        "WHERE IdExperiencia=@IdExp AND Estado = @Completa AND CupoDisponible > 0", conn, tx))
                    {
                        est.Parameters.AddWithValue("@Programada", (int)BE.EstadoExperiencia.Programada);
                        est.Parameters.AddWithValue("@Completa", (int)BE.EstadoExperiencia.Completa);
                        est.Parameters.AddWithValue("@IdExp", idExperiencia);
                        est.ExecuteNonQuery();
                    }
                }
            });
            RecalcularDV();
        }

        private BE.Reserva Mapear(DataRow row) => new BE.Reserva
        {
            IdReserva            = Convert.ToInt32(row["IdReserva"]),
            IdCliente            = Convert.ToInt32(row["IdCliente"]),
            IdExperiencia        = Convert.ToInt32(row["IdExperiencia"]),
            IdEmpleado           = row["IdEmpleado"] != DBNull.Value ? Convert.ToInt32(row["IdEmpleado"]) : 0,
            Estado               = (BE.EstadoReserva)Convert.ToInt32(row["Estado"]),
            FechaReserva         = Convert.ToDateTime(row["FechaReserva"]),
            CantidadInvitados    = Convert.ToInt32(row["CantidadInvitados"]),
            FechaCancelacion     = row["FechaCancelacion"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["FechaCancelacion"]) : null,
            MotivoCancelacion    = row["MotivoCancelacion"] != DBNull.Value ? row["MotivoCancelacion"].ToString() : null,
            NombreCliente        = row["NombreCliente"] != DBNull.Value ? row["NombreCliente"].ToString() : null,
            NombreExperiencia    = row["NombreExperiencia"] != DBNull.Value ? row["NombreExperiencia"].ToString() : null,
            NombreEmpleado       = row["NombreEmpleado"] != DBNull.Value ? row["NombreEmpleado"].ToString() : null,
            FechaHoraExperiencia = row["FechaHoraExperiencia"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["FechaHoraExperiencia"]) : null,
            DuracionExperienciaMinutos = row.Table.Columns.Contains("DuracionExperiencia") && row["DuracionExperiencia"] != DBNull.Value
                                            ? Convert.ToInt32(row["DuracionExperiencia"]) : 0
        };

        /// <summary>
        /// Restaura de forma atómica los campos indicados de una reserva (rollback desde historial).
        /// Solo revierte campos versionados: Estado, CantidadInvitados, MotivoCancelacion, FechaCancelacion.
        /// El cupo no se reconcilia aquí (es recomputable); el DV se recalcula al final.
        /// </summary>
        public void RestaurarOperacionAtomica(int idReserva, List<(string Campo, string Valor)> cambios)
        {
            acceso.EjecutarTransaccion((conn, tx) =>
            {
                foreach (var c in cambios)
                {
                    string col;
                    object val;
                    switch (c.Campo)
                    {
                        case "Estado":            col = "Estado";            val = c.Valor == null ? (object)DBNull.Value : (int)Enum.Parse(typeof(BE.EstadoReserva), c.Valor); break;
                        case "CantidadInvitados": col = "CantidadInvitados"; val = c.Valor == null ? (object)0 : Convert.ToInt32(c.Valor); break;
                        case "MotivoCancelacion": col = "MotivoCancelacion"; val = (object)c.Valor ?? DBNull.Value; break;
                        case "FechaCancelacion":  col = "FechaCancelacion";  val = string.IsNullOrEmpty(c.Valor) ? (object)DBNull.Value : Convert.ToDateTime(c.Valor); break;
                        default: continue; // campo no restaurable (ej. marcas de fecha informativas)
                    }
                    using (var cmd = new SqlCommand($"UPDATE Reserva SET [{col}] = @Valor WHERE IdReserva = @Id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@Valor", val);
                        cmd.Parameters.AddWithValue("@Id", idReserva);
                        cmd.ExecuteNonQuery();
                    }
                }
            });
            RecalcularDV();
        }
    }
}

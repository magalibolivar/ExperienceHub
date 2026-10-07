using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Capa de Acceso a Datos — Suscripcion. Opera sobre la tabla [Suscripcion],
    /// con JOIN a [PlanSuscripcion] para traer los beneficios del plan.
    /// </summary>
    public class Suscripcion
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        private const string SELECT_BASE =
            "SELECT s.IdSuscripcion, s.IdCliente, s.IdPlan, s.FechaInicio, s.FechaVencimiento, " +
            "       s.Estado, s.ReservasConsumidasMes, s.PeriodoConsumo, " +
            "       p.Nombre AS NombrePlan, p.ReservasMensuales, p.AnticipacionMaximaDias, p.AccesoPremium " +
            "FROM Suscripcion s " +
            "INNER JOIN PlanSuscripcion p ON p.IdPlan = s.IdPlan ";

        /// <summary>Suscripción más reciente del cliente (la vigente si la hay). Null si no tiene.</summary>
        public BE.Suscripcion ObtenerVigentePorCliente(int idCliente)
        {
            DataTable tabla = acceso.Leer(
                SELECT_BASE + "WHERE s.IdCliente = @IdCliente ORDER BY s.FechaInicio DESC",
                new[] { new SqlParameter("@IdCliente", idCliente) });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        /// <summary>Todas las suscripciones con los datos del plan (para análisis, CU-ANA-06).</summary>
        public List<BE.Suscripcion> ObtenerTodas()
        {
            var lista = new List<BE.Suscripcion>();
            DataTable tabla = acceso.Leer(SELECT_BASE + "ORDER BY s.FechaInicio DESC", null);
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        public BE.Suscripcion ObtenerPorId(int idSuscripcion)
        {
            DataTable tabla = acceso.Leer(
                SELECT_BASE + "WHERE s.IdSuscripcion = @Id",
                new[] { new SqlParameter("@Id", idSuscripcion) });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        public int Alta(BE.Suscripcion s)
        {
            DataTable t = acceso.Leer(
                "INSERT INTO Suscripcion (IdCliente, IdPlan, FechaInicio, FechaVencimiento, Estado, ReservasConsumidasMes, PeriodoConsumo) " +
                "VALUES (@IdCliente, @IdPlan, @FechaInicio, @FechaVencimiento, @Estado, @Consumo, @Periodo); " +
                "SELECT SCOPE_IDENTITY() AS IdNuevo",
                new[]
                {
                    new SqlParameter("@IdCliente", s.IdCliente),
                    new SqlParameter("@IdPlan", s.IdPlan),
                    new SqlParameter("@FechaInicio", s.FechaInicio.Date),
                    new SqlParameter("@FechaVencimiento", (object)s.FechaVencimiento ?? DBNull.Value),
                    new SqlParameter("@Estado", (int)s.Estado),
                    new SqlParameter("@Consumo", s.ReservasConsumidasMes),
                    new SqlParameter("@Periodo", BE.Suscripcion.InicioDeMes(DateTime.Today))
                });
            return t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[0]["IdNuevo"]) : 0;
        }

        /// <summary>Renueva: cambia plan/vencimiento, reactiva y reinicia el consumo del período en curso.</summary>
        public void Renovar(int idSuscripcion, int idPlan, DateTime? nuevoVencimiento)
        {
            acceso.Escribir(
                "UPDATE Suscripcion SET IdPlan=@IdPlan, FechaVencimiento=@Vto, " +
                "Estado=@Activa, ReservasConsumidasMes=0, PeriodoConsumo=@Periodo WHERE IdSuscripcion=@Id",
                new[]
                {
                    new SqlParameter("@IdPlan", idPlan),
                    new SqlParameter("@Vto", (object)nuevoVencimiento ?? DBNull.Value),
                    new SqlParameter("@Activa", (object)(int)BE.EstadoSuscripcion.Activa),
                    new SqlParameter("@Periodo", BE.Suscripcion.InicioDeMes(DateTime.Today)),
                    new SqlParameter("@Id", idSuscripcion)
                });
        }

        /// <summary>
        /// Tarea de SISTEMA (sin sesión): mantiene el ciclo de vida por fecha.
        ///  (a) Vence las suscripciones Activas cuyo FechaVencimiento ya pasó (Activa → Vencida).
        ///  (b) Reinicia el cupo mensual (ReservasConsumidasMes=0) de las que entraron en un mes
        ///      nuevo, re-sellando PeriodoConsumo al mes actual.
        /// Devuelve la cantidad total de filas afectadas.
        /// </summary>
        public int ActualizarVigenciaYConsumo()
        {
            DateTime inicioMes = BE.Suscripcion.InicioDeMes(DateTime.Today);
            int filas = acceso.Escribir(
                "UPDATE Suscripcion SET Estado=@Vencida " +
                "WHERE Estado=@Activa AND FechaVencimiento IS NOT NULL AND FechaVencimiento < CAST(GETDATE() AS DATE)",
                new[]
                {
                    new SqlParameter("@Vencida", (object)(int)BE.EstadoSuscripcion.Vencida),
                    new SqlParameter("@Activa",  (object)(int)BE.EstadoSuscripcion.Activa)
                });
            filas += acceso.Escribir(
                "UPDATE Suscripcion SET ReservasConsumidasMes=0, PeriodoConsumo=@Periodo " +
                "WHERE PeriodoConsumo IS NULL OR PeriodoConsumo < @Periodo",
                new[] { new SqlParameter("@Periodo", inicioMes) });
            return filas;
        }

        /// <summary>Suscripciones Activas que vencen dentro de los próximos <paramref name="dias"/> días.</summary>
        public List<BE.Suscripcion> ObtenerPorVencer(int dias)
        {
            var lista = new List<BE.Suscripcion>();
            DataTable t = acceso.Leer(
                SELECT_BASE +
                "WHERE s.Estado=@Activa AND s.FechaVencimiento IS NOT NULL " +
                "AND s.FechaVencimiento >= CAST(GETDATE() AS DATE) " +
                "AND s.FechaVencimiento <= DATEADD(day, @Dias, CAST(GETDATE() AS DATE)) " +
                "ORDER BY s.FechaVencimiento",
                new[]
                {
                    new SqlParameter("@Activa", (object)(int)BE.EstadoSuscripcion.Activa),
                    new SqlParameter("@Dias", dias)
                });
            foreach (DataRow row in t.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>Suscripciones vencidas (por estado o por fecha pasada).</summary>
        public List<BE.Suscripcion> ObtenerVencidas()
        {
            var lista = new List<BE.Suscripcion>();
            DataTable t = acceso.Leer(
                SELECT_BASE +
                "WHERE s.Estado=@Vencida " +
                "OR (s.Estado=@Activa AND s.FechaVencimiento IS NOT NULL AND s.FechaVencimiento < CAST(GETDATE() AS DATE)) " +
                "ORDER BY s.FechaVencimiento",
                new[]
                {
                    new SqlParameter("@Vencida", (object)(int)BE.EstadoSuscripcion.Vencida),
                    new SqlParameter("@Activa",  (object)(int)BE.EstadoSuscripcion.Activa)
                });
            foreach (DataRow row in t.Rows) lista.Add(Mapear(row));
            return lista;
        }

        public void CambiarEstado(int idSuscripcion, BE.EstadoSuscripcion estado)
        {
            acceso.Escribir("UPDATE Suscripcion SET Estado=@Estado WHERE IdSuscripcion=@Id",
                new[] { new SqlParameter("@Estado", (int)estado), new SqlParameter("@Id", idSuscripcion) });
        }

        /// <summary>Cantidad de suscripciones activas asociadas a un plan (para proteger la baja del plan).</summary>
        public int ContarActivasPorPlan(int idPlan)
        {
            var dt = acceso.Leer(
                "SELECT COUNT(*) AS Total FROM Suscripcion WHERE IdPlan=@IdPlan AND Estado=@Activa",
                new[] { new SqlParameter("@IdPlan", idPlan), new SqlParameter("@Activa", (object)(int)BE.EstadoSuscripcion.Activa) });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Total"]) : 0;
        }

        public void IncrementarConsumo(int idSuscripcion)
        {
            acceso.Escribir(
                "UPDATE Suscripcion SET ReservasConsumidasMes = ReservasConsumidasMes + 1 WHERE IdSuscripcion=@Id",
                new[] { new SqlParameter("@Id", idSuscripcion) });
        }

        public void DecrementarConsumo(int idSuscripcion)
        {
            acceso.Escribir(
                "UPDATE Suscripcion SET ReservasConsumidasMes = " +
                "CASE WHEN ReservasConsumidasMes > 0 THEN ReservasConsumidasMes - 1 ELSE 0 END " +
                "WHERE IdSuscripcion=@Id",
                new[] { new SqlParameter("@Id", idSuscripcion) });
        }

        private BE.Suscripcion Mapear(DataRow row) => new BE.Suscripcion
        {
            IdSuscripcion          = Convert.ToInt32(row["IdSuscripcion"]),
            IdCliente              = Convert.ToInt32(row["IdCliente"]),
            IdPlan                 = Convert.ToInt32(row["IdPlan"]),
            FechaInicio            = Convert.ToDateTime(row["FechaInicio"]),
            FechaVencimiento       = row["FechaVencimiento"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["FechaVencimiento"]) : null,
            Estado                 = (BE.EstadoSuscripcion)Convert.ToInt32(row["Estado"]),
            ReservasConsumidasMes  = Convert.ToInt32(row["ReservasConsumidasMes"]),
            PeriodoConsumo         = row.Table.Columns.Contains("PeriodoConsumo") && row["PeriodoConsumo"] != DBNull.Value
                                        ? (DateTime?)Convert.ToDateTime(row["PeriodoConsumo"]) : null,
            NombrePlan             = row["NombrePlan"].ToString(),
            ReservasDelPlan        = Convert.ToInt32(row["ReservasMensuales"]),
            AnticipacionMaxDelPlan = Convert.ToInt32(row["AnticipacionMaximaDias"]),
            AccesoPremiumDelPlan   = Convert.ToBoolean(row["AccesoPremium"])
        };
    }
}

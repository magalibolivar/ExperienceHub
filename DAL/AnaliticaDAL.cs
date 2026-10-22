using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// PN03 — Acceso a datos de ANALÍTICA (solo lectura). Agrega los KPIs del período con consultas
    /// GROUP BY (sin N+1). El período se define por la fecha de realización de la experiencia; los
    /// filtros opcionales (ciudad/categoría/organizador) acotan el universo de experiencias.
    /// Estados Reserva: 1 Confirmada, 2 Cancelada, 3 Asistió, 4 No asistió. ListaEspera: 0 Esperando.
    /// </summary>
    public class AnaliticaDAL
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        // Filtro común de experiencias del período (fecha + filtros opcionales).
        private static string FiltroExp(string alias)
            => $"(CAST({alias}.Fecha AS DATETIME)) >= @Desde AND (CAST({alias}.Fecha AS DATETIME)) < @Hasta " +
               $"AND (@Ciudad IS NULL OR {alias}.IdCiudad = @Ciudad) " +
               $"AND (@Categoria IS NULL OR {alias}.IdCategoria = @Categoria) " +
               $"AND (@Organizador IS NULL OR {alias}.IdOrganizador = @Organizador)";

        private SqlParameter[] Params(DateTime desde, DateTime hasta, int? ciudad, int? categoria, int? organizador)
            => new[]
            {
                new SqlParameter("@Desde", desde.Date),
                new SqlParameter("@Hasta", hasta.Date.AddDays(1)),
                new SqlParameter("@Ciudad", (object)ciudad ?? DBNull.Value),
                new SqlParameter("@Categoria", (object)categoria ?? DBNull.Value),
                new SqlParameter("@Organizador", (object)organizador ?? DBNull.Value)
            };

        public List<BE.KpiExperiencia> KpisExperiencia(DateTime d, DateTime h, int? ci, int? ca, int? o)
        {
            var lista = new List<BE.KpiExperiencia>();
            var dt = acceso.Leer(
                "SELECT e.IdExperiencia, e.Nombre, e.CupoMaximo, " +
                " ISNULL((SELECT SUM(1 + r.CantidadInvitados) FROM Reserva r WHERE r.IdExperiencia=e.IdExperiencia AND r.Estado IN (1,3,4)),0) AS Reservados, " +
                " ISNULL((SELECT COUNT(*) FROM ListaEspera le WHERE le.IdExperiencia=e.IdExperiencia AND le.Estado=0),0) AS EnEspera " +
                "FROM Experiencia e WHERE " + FiltroExp("e") + " ORDER BY e.Fecha",
                Params(d, h, ci, ca, o));
            foreach (DataRow row in dt.Rows)
                lista.Add(new BE.KpiExperiencia
                {
                    IdExperiencia = Convert.ToInt32(row["IdExperiencia"]),
                    Nombre = row["Nombre"].ToString(),
                    CupoMaximo = Convert.ToInt32(row["CupoMaximo"]),
                    LugaresReservados = Convert.ToInt32(row["Reservados"]),
                    EnEspera = Convert.ToInt32(row["EnEspera"])
                });
            return lista;
        }

        public List<BE.KpiOrganizador> KpisOrganizador(DateTime d, DateTime h, int? ci, int? ca, int? o)
        {
            var lista = new List<BE.KpiOrganizador>();
            var dt = acceso.Leer(
                "SELECT org.IdOrganizador, org.Nombre, " +
                " ISNULL(AVG(CAST(cal.Puntaje AS DECIMAL(5,2))),0) AS CalifProm, " +
                " COUNT(cal.IdCalificacion) AS Califs, " +
                " SUM(CASE WHEN r.Estado=3 THEN 1 ELSE 0 END) AS Asis, " +
                " SUM(CASE WHEN r.Estado=4 THEN 1 ELSE 0 END) AS Inasis " +
                "FROM Organizador org " +
                "JOIN Experiencia e ON e.IdOrganizador=org.IdOrganizador AND " + FiltroExp("e") + " " +
                "LEFT JOIN Reserva r ON r.IdExperiencia=e.IdExperiencia AND r.Estado IN (3,4) " +
                "LEFT JOIN Calificacion cal ON cal.IdReserva=r.IdReserva " +
                "GROUP BY org.IdOrganizador, org.Nombre ORDER BY org.Nombre",
                Params(d, h, ci, ca, o));
            foreach (DataRow row in dt.Rows)
                lista.Add(new BE.KpiOrganizador
                {
                    IdOrganizador = Convert.ToInt32(row["IdOrganizador"]),
                    Nombre = row["Nombre"].ToString(),
                    CalificacionPromedio = Convert.ToDecimal(row["CalifProm"]),
                    Calificaciones = Convert.ToInt32(row["Califs"]),
                    Asistencias = Convert.ToInt32(row["Asis"]),
                    Inasistencias = Convert.ToInt32(row["Inasis"])
                });
            return lista;
        }

        public List<BE.KpiPlan> KpisPlan(DateTime d, DateTime h, int? ci, int? ca, int? o)
        {
            var lista = new List<BE.KpiPlan>();
            int meses = Math.Max(1, ((h.Year - d.Year) * 12) + h.Month - d.Month + 1);
            var dt = acceso.Leer(
                "SELECT p.IdPlan, p.Nombre, p.ReservasMensuales, " +
                " (SELECT COUNT(*) FROM Suscripcion s WHERE s.IdPlan=p.IdPlan) AS Subs, " +
                " ISNULL((SELECT COUNT(*) FROM Reserva r JOIN Suscripcion s2 ON s2.IdSuscripcion=r.IdSuscripcion " +
                "         JOIN Experiencia e ON e.IdExperiencia=r.IdExperiencia " +
                "         WHERE s2.IdPlan=p.IdPlan AND r.Estado IN (1,3,4) AND " + FiltroExp("e") + "),0) AS Usadas " +
                "FROM PlanSuscripcion p WHERE p.Estado=1 ORDER BY p.Nombre",
                Params(d, h, ci, ca, o));
            foreach (DataRow row in dt.Rows)
            {
                int subs = Convert.ToInt32(row["Subs"]);
                int resMes = Convert.ToInt32(row["ReservasMensuales"]);
                lista.Add(new BE.KpiPlan
                {
                    IdPlan = Convert.ToInt32(row["IdPlan"]),
                    Nombre = row["Nombre"].ToString(),
                    Suscripciones = subs,
                    ReservasUsadas = Convert.ToInt32(row["Usadas"]),
                    ReservasPermitidas = subs * resMes * meses
                });
            }
            return lista;
        }

        /// <summary>KPIs globales del período (demanda insatisfecha, cancelaciones, asistencia, comercial).</summary>
        public void Globales(BE.TableroPeriodo t, DateTime d, DateTime h, int? ci, int? ca, int? o)
        {
            var dt = acceso.Leer(
                "SELECT " +
                " ISNULL((SELECT COUNT(*) FROM ListaEspera le JOIN Experiencia e ON e.IdExperiencia=le.IdExperiencia " +
                "         WHERE le.Estado=0 AND " + FiltroExp("e") + "),0) AS EnEspera, " +
                " ISNULL((SELECT COUNT(*) FROM ListaEspera le JOIN Experiencia e ON e.IdExperiencia=le.IdExperiencia " +
                "         WHERE le.MotivoRetiro='Vencida' AND " + FiltroExp("e") + "),0) AS Vencidas, " +
                " ISNULL((SELECT COUNT(*) FROM Reserva r JOIN Experiencia e ON e.IdExperiencia=r.IdExperiencia " +
                "         WHERE r.Estado=2 AND r.CancelacionATiempo=1 AND " + FiltroExp("e") + "),0) AS CancATiempo, " +
                " ISNULL((SELECT COUNT(*) FROM Reserva r JOIN Experiencia e ON e.IdExperiencia=r.IdExperiencia " +
                "         WHERE r.Estado=2 AND ISNULL(r.CancelacionATiempo,0)=0 AND " + FiltroExp("e") + "),0) AS CancTardia, " +
                " ISNULL((SELECT COUNT(*) FROM Reserva r JOIN Experiencia e ON e.IdExperiencia=r.IdExperiencia " +
                "         WHERE r.Estado=4 AND " + FiltroExp("e") + "),0) AS Inasis",
                Params(d, h, ci, ca, o));
            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                t.EnEsperaTotal       = Convert.ToInt32(row["EnEspera"]);
                t.OfertasVencidas     = Convert.ToInt32(row["Vencidas"]);
                t.CancelacionesATiempo= Convert.ToInt32(row["CancATiempo"]);
                t.CancelacionesTardias= Convert.ToInt32(row["CancTardia"]);
                t.Inasistencias       = Convert.ToInt32(row["Inasis"]);
            }

            // Comercial: altas/renovaciones/ingresos por contrataciones pagadas en el período (por FechaPago).
            var dc = acceso.Leer(
                "SELECT " +
                " SUM(CASE WHEN Tipo=0 THEN 1 ELSE 0 END) AS Altas, " +
                " SUM(CASE WHEN Tipo=1 THEN 1 ELSE 0 END) AS Renov, " +
                " ISNULL(SUM(CASE WHEN ImporteTotal>0 THEN ImporteTotal ELSE Importe END),0) AS Ingresos " +
                "FROM Contratacion WHERE FechaPago IS NOT NULL AND FechaPago >= @Desde AND FechaPago < @Hasta",
                new[] { new SqlParameter("@Desde", d.Date), new SqlParameter("@Hasta", h.Date.AddDays(1)) });
            if (dc.Rows.Count > 0 && dc.Rows[0]["Altas"] != DBNull.Value)
            {
                t.Altas         = Convert.ToInt32(dc.Rows[0]["Altas"]);
                t.Renovaciones  = Convert.ToInt32(dc.Rows[0]["Renov"]);
                t.IngresosPlanes= Convert.ToDecimal(dc.Rows[0]["Ingresos"]);
            }
        }
    }
}

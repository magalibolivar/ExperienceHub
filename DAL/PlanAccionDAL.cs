using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// PN03 — Acceso a datos del plan de acción: PlanAccion, RecomendacionPlan y KpiSnapshot.
    /// Estas tablas NO llevan dígito verificador (son de planificación/decisión, no maestras).
    /// </summary>
    public class PlanAccionDAL
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        // ── PlanAccion ─────────────────────────────────────────────────────────
        public int CrearPlan(BE.PlanAccion p)
        {
            var dt = acceso.Leer(
                "INSERT INTO PlanAccion (Titulo, PeriodoDesde, PeriodoHasta, FiltroCiudad, FiltroCategoria, " +
                "FiltroOrganizador, Estado, IdGerente, FechaCreacion) " +
                "VALUES (@Tit, @Desde, @Hasta, @Ci, @Ca, @Org, @Est, @Ger, GETDATE()); SELECT CAST(SCOPE_IDENTITY() AS INT)",
                new[]
                {
                    new SqlParameter("@Tit", p.Titulo), new SqlParameter("@Desde", p.PeriodoDesde.Date),
                    new SqlParameter("@Hasta", p.PeriodoHasta.Date),
                    new SqlParameter("@Ci", (object)p.FiltroCiudad ?? DBNull.Value),
                    new SqlParameter("@Ca", (object)p.FiltroCategoria ?? DBNull.Value),
                    new SqlParameter("@Org", (object)p.FiltroOrganizador ?? DBNull.Value),
                    new SqlParameter("@Est", (int)p.Estado),
                    new SqlParameter("@Ger", (object)p.IdGerente ?? DBNull.Value)
                });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
        }

        public void CambiarEstadoPlan(int idPlan, BE.EstadoPlanAccion estado, bool cerrar = false)
        {
            acceso.Escribir(
                "UPDATE PlanAccion SET Estado=@Est" + (cerrar ? ", FechaCierre=GETDATE()" : "") + " WHERE IdPlanAccion=@Id",
                new[] { new SqlParameter("@Est", (int)estado), new SqlParameter("@Id", idPlan) });
        }

        public List<BE.PlanAccion> ObtenerPlanes()
        {
            var lista = new List<BE.PlanAccion>();
            var dt = acceso.Leer("SELECT * FROM PlanAccion ORDER BY FechaCreacion DESC", null);
            foreach (DataRow r in dt.Rows) lista.Add(MapearPlan(r));
            return lista;
        }

        public BE.PlanAccion ObtenerPlan(int id)
        {
            var dt = acceso.Leer("SELECT * FROM PlanAccion WHERE IdPlanAccion=@Id", new[] { new SqlParameter("@Id", id) });
            return dt.Rows.Count == 0 ? null : MapearPlan(dt.Rows[0]);
        }

        private static BE.PlanAccion MapearPlan(DataRow r) => new BE.PlanAccion
        {
            IdPlanAccion = Convert.ToInt32(r["IdPlanAccion"]),
            Titulo = r["Titulo"].ToString(),
            PeriodoDesde = Convert.ToDateTime(r["PeriodoDesde"]),
            PeriodoHasta = Convert.ToDateTime(r["PeriodoHasta"]),
            FiltroCiudad = r["FiltroCiudad"] != DBNull.Value ? (int?)Convert.ToInt32(r["FiltroCiudad"]) : null,
            FiltroCategoria = r["FiltroCategoria"] != DBNull.Value ? (int?)Convert.ToInt32(r["FiltroCategoria"]) : null,
            FiltroOrganizador = r["FiltroOrganizador"] != DBNull.Value ? (int?)Convert.ToInt32(r["FiltroOrganizador"]) : null,
            Estado = (BE.EstadoPlanAccion)Convert.ToInt32(r["Estado"]),
            IdGerente = r["IdGerente"] != DBNull.Value ? (int?)Convert.ToInt32(r["IdGerente"]) : null,
            FechaCreacion = Convert.ToDateTime(r["FechaCreacion"]),
            FechaCierre = r["FechaCierre"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["FechaCierre"]) : null
        };

        // ── RecomendacionPlan ──────────────────────────────────────────────────
        public int AgregarRecomendacion(BE.RecomendacionPlan rec)
        {
            var dt = acceso.Leer(
                "INSERT INTO RecomendacionPlan (IdPlanAccion, Tipo, IdEntidad, NombreEntidad, Evidencia, " +
                "AccionSugerida, Decision, Ejecutada) VALUES (@P, @T, @E, @N, @Ev, @A, 0, 0); SELECT CAST(SCOPE_IDENTITY() AS INT)",
                new[]
                {
                    new SqlParameter("@P", rec.IdPlanAccion), new SqlParameter("@T", (int)rec.Tipo),
                    new SqlParameter("@E", (object)rec.IdEntidad ?? DBNull.Value),
                    new SqlParameter("@N", (object)rec.NombreEntidad ?? DBNull.Value),
                    new SqlParameter("@Ev", rec.Evidencia ?? ""), new SqlParameter("@A", (int)rec.AccionSugerida)
                });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
        }

        public List<BE.RecomendacionPlan> ObtenerRecomendaciones(int idPlan)
        {
            var lista = new List<BE.RecomendacionPlan>();
            var dt = acceso.Leer("SELECT * FROM RecomendacionPlan WHERE IdPlanAccion=@P ORDER BY IdRecomendacion",
                new[] { new SqlParameter("@P", idPlan) });
            foreach (DataRow r in dt.Rows)
                lista.Add(new BE.RecomendacionPlan
                {
                    IdRecomendacion = Convert.ToInt32(r["IdRecomendacion"]),
                    IdPlanAccion = Convert.ToInt32(r["IdPlanAccion"]),
                    Tipo = (BE.TipoRecomendacion)Convert.ToInt32(r["Tipo"]),
                    IdEntidad = r["IdEntidad"] != DBNull.Value ? (int?)Convert.ToInt32(r["IdEntidad"]) : null,
                    NombreEntidad = r["NombreEntidad"] != DBNull.Value ? r["NombreEntidad"].ToString() : null,
                    Evidencia = r["Evidencia"].ToString(),
                    AccionSugerida = (BE.AccionSugerida)Convert.ToInt32(r["AccionSugerida"]),
                    Decision = (BE.DecisionRecomendacion)Convert.ToInt32(r["Decision"]),
                    MotivoDecision = r["MotivoDecision"] != DBNull.Value ? r["MotivoDecision"].ToString() : null,
                    Ejecutada = Convert.ToBoolean(r["Ejecutada"]),
                    FechaEjecucion = r["FechaEjecucion"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["FechaEjecucion"]) : null
                });
            return lista;
        }

        public void Decidir(int idRec, BE.DecisionRecomendacion decision, string motivo)
        {
            acceso.Escribir("UPDATE RecomendacionPlan SET Decision=@D, MotivoDecision=@M WHERE IdRecomendacion=@Id",
                new[] { new SqlParameter("@D", (int)decision), new SqlParameter("@M", (object)motivo ?? DBNull.Value), new SqlParameter("@Id", idRec) });
        }

        public void MarcarEjecutada(int idRec)
        {
            acceso.Escribir("UPDATE RecomendacionPlan SET Ejecutada=1, FechaEjecucion=GETDATE() WHERE IdRecomendacion=@Id",
                new[] { new SqlParameter("@Id", idRec) });
        }

        // ── KpiSnapshot ────────────────────────────────────────────────────────
        public void GuardarSnapshot(int idPlan, BE.MomentoKpi momento, string clave, decimal valor)
        {
            acceso.Escribir(
                "INSERT INTO KpiSnapshot (IdPlanAccion, Momento, Clave, Valor, Fecha) VALUES (@P, @M, @C, @V, GETDATE())",
                new[]
                {
                    new SqlParameter("@P", idPlan), new SqlParameter("@M", (int)momento),
                    new SqlParameter("@C", clave), new SqlParameter("@V", valor)
                });
        }

        public List<BE.KpiSnapshot> ObtenerSnapshots(int idPlan)
        {
            var lista = new List<BE.KpiSnapshot>();
            var dt = acceso.Leer("SELECT * FROM KpiSnapshot WHERE IdPlanAccion=@P ORDER BY Momento, Clave",
                new[] { new SqlParameter("@P", idPlan) });
            foreach (DataRow r in dt.Rows)
                lista.Add(new BE.KpiSnapshot
                {
                    IdSnapshot = Convert.ToInt32(r["IdSnapshot"]), IdPlanAccion = Convert.ToInt32(r["IdPlanAccion"]),
                    Momento = (BE.MomentoKpi)Convert.ToInt32(r["Momento"]), Clave = r["Clave"].ToString(),
                    Valor = Convert.ToDecimal(r["Valor"]), Fecha = Convert.ToDateTime(r["Fecha"])
                });
            return lista;
        }
    }
}

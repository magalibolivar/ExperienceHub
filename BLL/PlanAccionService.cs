using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>
    /// PN03 — Ciclo de vida del Plan de acción (Borrador → Aprobado → En ejecución → Cerrado).
    /// El Gerente crea el plan con las recomendaciones generadas, aprueba/descarta cada una con motivo,
    /// y el Coordinador ejecuta las aprobadas reutilizando la BLL existente (Experiencia/Organizador).
    /// Al cerrar compara los KPIs "Antes" (período analizado) con "Después" (período de igual duración
    /// desde la fecha de ejecución), vía KpiSnapshot.
    /// </summary>
    public class PlanAccionService
    {
        private readonly DAL.PlanAccionDAL _dal = new DAL.PlanAccionDAL();
        private readonly Analitica         _analitica = new Analitica();
        private readonly Experiencia       _bllExp = new Experiencia();
        private readonly Organizador       _bllOrg = new Organizador();
        private readonly Servicios.Bitacora _bitacora = new Servicios.Bitacora();
        private const string MODULO = "Planificación";

        // ── Crear plan (Borrador) a partir del tablero + recomendaciones generadas ──
        public int CrearPlan(string titulo, BE.TableroPeriodo tablero, List<BE.RecomendacionGenerada> recs,
                             int? ciudad, int? categoria, int? organizador, int? idGerente)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanAccion);
            var plan = new BE.PlanAccion
            {
                Titulo = titulo, PeriodoDesde = tablero.Desde, PeriodoHasta = tablero.Hasta,
                FiltroCiudad = ciudad, FiltroCategoria = categoria, FiltroOrganizador = organizador,
                Estado = BE.EstadoPlanAccion.Borrador, IdGerente = idGerente
            };
            int idPlan = _dal.CrearPlan(plan);
            foreach (var r in recs ?? new List<BE.RecomendacionGenerada>())
                _dal.AgregarRecomendacion(new BE.RecomendacionPlan
                {
                    IdPlanAccion = idPlan, Tipo = r.Tipo, IdEntidad = r.IdEntidad,
                    NombreEntidad = r.NombreEntidad, Evidencia = r.Evidencia, AccionSugerida = r.AccionSugerida
                });
            // Snapshot "Antes" = KPIs del período analizado.
            GuardarSnapshotTablero(idPlan, BE.MomentoKpi.Antes, tablero);
            _bitacora.Registrar(MODULO, $"Plan de acción #{idPlan} '{titulo}' creado con {(recs?.Count ?? 0)} recomendación(es)", BE.Criticidad.Media);
            return idPlan;
        }

        public List<BE.PlanAccion> ObtenerPlanes() { PermisosAccion.Exigir(BE.Patentes.PlanAccion); return _dal.ObtenerPlanes(); }
        public BE.PlanAccion ObtenerPlan(int id)   { PermisosAccion.Exigir(BE.Patentes.PlanAccion); return _dal.ObtenerPlan(id); }
        public List<BE.RecomendacionPlan> ObtenerRecomendaciones(int idPlan)
        { PermisosAccion.Exigir(BE.Patentes.PlanAccion); return _dal.ObtenerRecomendaciones(idPlan); }

        // ── Decisión del Gerente ─────────────────────────────────────────────────
        public void Decidir(int idRecomendacion, bool aprobar, string motivo)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanAccionAprobar);
            _dal.Decidir(idRecomendacion, aprobar ? BE.DecisionRecomendacion.Aprobada : BE.DecisionRecomendacion.Descartada, motivo);
            _bitacora.Registrar(MODULO, $"Recomendación #{idRecomendacion} {(aprobar ? "aprobada" : "descartada")} — {motivo}", BE.Criticidad.Baja);
        }

        public void AprobarPlan(int idPlan)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanAccionAprobar);
            var p = _dal.ObtenerPlan(idPlan);
            if (p == null || !p.PuedeAprobar()) throw new BE.AppException("err.bll.plan.estado", "El plan no está en Borrador.");
            _dal.CambiarEstadoPlan(idPlan, BE.EstadoPlanAccion.Aprobado);
            _bitacora.Registrar(MODULO, $"Plan de acción #{idPlan} aprobado", BE.Criticidad.Media);
        }

        // ── Ejecución (Coordinador) — reutiliza la BLL existente ─────────────────
        /// <summary>Ejecuta la acción sugerida de una recomendación aprobada. Parámetros opcionales según
        /// la acción (nueva fecha/hora para ProgramarEdicion, cantidad para AmpliarCupo).</summary>
        public void EjecutarRecomendacion(BE.RecomendacionPlan rec, DateTime? nuevaFecha = null,
                                          TimeSpan? nuevaHora = null, int cupoExtra = 0)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanAccionEjecutar);
            if (rec == null || !rec.PuedeEjecutar())
                throw new BE.AppException("err.bll.plan.rec_estado", "La recomendación no está aprobada o ya fue ejecutada.");

            switch (rec.AccionSugerida)
            {
                case BE.AccionSugerida.ProgramarEdicion:
                    _bllExp.ProgramarEdicion(MODULO, rec.IdEntidad ?? 0,
                        nuevaFecha ?? DateTime.Today.AddDays(30), nuevaHora ?? new TimeSpan(20, 0, 0));
                    break;
                case BE.AccionSugerida.AmpliarCupo:
                    _bllExp.AmpliarCupo(MODULO, rec.IdEntidad ?? 0, cupoExtra > 0 ? cupoExtra : 2);
                    break;
                case BE.AccionSugerida.CancelarExperiencia:
                    var exp = _bllExp.ObtenerPorId(rec.IdEntidad ?? 0);
                    if (exp != null) _bllExp.CancelarExperiencia(MODULO, exp, "PN03: baja por ocupación insuficiente");
                    break;
                case BE.AccionSugerida.SuspenderOrganizador:
                    _bllOrg.Baja(MODULO, rec.IdEntidad ?? 0);   // baja lógica = suspensión
                    break;
                case BE.AccionSugerida.RevisarPlan:
                case BE.AccionSugerida.NoReprogramar:
                    // Tareas de decisión (sin efecto en catálogo): se marcan ejecutadas al registrarlas.
                    break;
            }
            _dal.MarcarEjecutada(rec.IdRecomendacion);
            _bitacora.Registrar(MODULO, $"Recomendación #{rec.IdRecomendacion} ejecutada ({rec.AccionSugerida})", BE.Criticidad.Media);
        }

        public void MarcarEnEjecucion(int idPlan)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanAccionEjecutar);
            _dal.CambiarEstadoPlan(idPlan, BE.EstadoPlanAccion.EnEjecucion);
        }

        // ── Cierre + informe comparativo ─────────────────────────────────────────
        /// <summary>Cierra el plan y guarda el snapshot "Después": KPIs de un período de IGUAL duración
        /// que el analizado, contado desde HOY (fecha de cierre/ejecución de las acciones).</summary>
        public void CerrarPlan(int idPlan)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanAccionAprobar);
            var p = _dal.ObtenerPlan(idPlan);
            if (p == null || !p.PuedeCerrar()) throw new BE.AppException("err.bll.plan.estado", "El plan no está En ejecución.");

            int dias = Math.Max(1, (int)(p.PeriodoHasta.Date - p.PeriodoDesde.Date).TotalDays);
            var despDesde = DateTime.Today;
            var despHasta = DateTime.Today.AddDays(dias);
            var tableroDespues = _analitica.CalcularTablero(despDesde, despHasta, p.FiltroCiudad, p.FiltroCategoria, p.FiltroOrganizador);
            GuardarSnapshotTablero(idPlan, BE.MomentoKpi.Despues, tableroDespues);

            _dal.CambiarEstadoPlan(idPlan, BE.EstadoPlanAccion.Cerrado, cerrar: true);
            _bitacora.Registrar(MODULO, $"Plan de acción #{idPlan} cerrado (informe antes/después generado)", BE.Criticidad.Media);
        }

        public List<BE.KpiSnapshot> ObtenerComparativo(int idPlan)
        { PermisosAccion.Exigir(BE.Patentes.PlanAccion); return _dal.ObtenerSnapshots(idPlan); }

        // KPIs globales agregados que se congelan como snapshot (claves estables para comparar).
        private void GuardarSnapshotTablero(int idPlan, BE.MomentoKpi momento, BE.TableroPeriodo t)
        {
            _dal.GuardarSnapshot(idPlan, momento, "EnEspera",             t.EnEsperaTotal);
            _dal.GuardarSnapshot(idPlan, momento, "OfertasVencidas",      t.OfertasVencidas);
            _dal.GuardarSnapshot(idPlan, momento, "CancelacionesATiempo", t.CancelacionesATiempo);
            _dal.GuardarSnapshot(idPlan, momento, "CancelacionesTardias", t.CancelacionesTardias);
            _dal.GuardarSnapshot(idPlan, momento, "Inasistencias",        t.Inasistencias);
            _dal.GuardarSnapshot(idPlan, momento, "Altas",                t.Altas);
            _dal.GuardarSnapshot(idPlan, momento, "Renovaciones",         t.Renovaciones);
            _dal.GuardarSnapshot(idPlan, momento, "IngresosPlanes",       t.IngresosPlanes);
        }
    }
}

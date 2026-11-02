using System;

namespace BE
{
    /// <summary>PN03 — Plan de acción que agrupa las recomendaciones aprobadas de un período.</summary>
    public class PlanAccion
    {
        public int      IdPlanAccion { get; set; }
        public string   Titulo       { get; set; }
        public DateTime PeriodoDesde { get; set; }
        public DateTime PeriodoHasta { get; set; }
        public int?     FiltroCiudad      { get; set; }
        public int?     FiltroCategoria   { get; set; }
        public int?     FiltroOrganizador { get; set; }
        public EstadoPlanAccion Estado { get; set; } = EstadoPlanAccion.Borrador;
        public int?     IdGerente    { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaCierre  { get; set; }

        public bool PuedeAprobar()  => Estado == EstadoPlanAccion.Borrador;
        public bool PuedeEjecutar() => Estado == EstadoPlanAccion.Aprobado;
        public bool PuedeCerrar()   => Estado == EstadoPlanAccion.EnEjecucion;

        // ── Período de observación (Antes/Después) — SIN columnas nuevas: se derivan de datos ya
        // persistidos (PeriodoDesde/Hasta/FechaCierre). El cierre del plan ya NO captura el snapshot
        // "Después" de inmediato (eso medía un período FUTURO sin datos reales); el "Después" se
        // evalúa más tarde, cuando transcurrió una ventana de observación de igual duración que el
        // período Antes, contada desde el cierre. "Evaluado" se determina por la existencia de un
        // snapshot (ver PlanAccionService.TieneEvaluacion) — no agrega estado nuevo al enum.

        /// <summary>Duración (días) del período analizado — también la duración de la ventana de
        /// observación posterior al cierre, para comparar períodos de igual longitud.</summary>
        public int DuracionPeriodoDias => Math.Max(1, (int)(PeriodoHasta.Date - PeriodoDesde.Date).TotalDays);

        /// <summary>Fecha en la que el plan queda en condiciones de evaluarse (cierre + duración del
        /// período analizado). Null si el plan todavía no se cerró.</summary>
        public DateTime? FechaEvaluacionPrevista => FechaCierre?.Date.AddDays(DuracionPeriodoDias);

        /// <summary>¿Ya transcurrió la ventana de observación completa? NO implica que ya se evaluó
        /// (eso depende de si existe un snapshot "Después" persistido). Recibe la fecha actual como
        /// parámetro — función pura, testeable sin depender del reloj del sistema operativo.</summary>
        public bool PeriodoObservacionCumplido(DateTime ahora)
            => Estado == EstadoPlanAccion.Cerrado && FechaEvaluacionPrevista.HasValue && ahora.Date >= FechaEvaluacionPrevista.Value;
    }
}

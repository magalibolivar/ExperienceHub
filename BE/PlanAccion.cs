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
    }
}

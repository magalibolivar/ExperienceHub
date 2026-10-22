using System;

namespace BE
{
    /// <summary>
    /// PN03 — Valor de un KPI congelado para un plan de acción. "Antes" = KPIs del período analizado;
    /// "Después" = KPIs de un período de igual duración desde la fecha de ejecución (ver BLL.Analitica).
    /// Permite el informe comparativo al cerrar el plan.
    /// </summary>
    public class KpiSnapshot
    {
        public int        IdSnapshot   { get; set; }
        public int        IdPlanAccion { get; set; }
        public MomentoKpi Momento      { get; set; }
        public string     Clave        { get; set; }
        public decimal    Valor        { get; set; }
        public DateTime   Fecha        { get; set; }
    }
}

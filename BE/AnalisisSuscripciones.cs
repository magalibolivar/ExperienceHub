using System.Collections.Generic;

namespace BE
{
    /// <summary>CU-ANA-06 — Comportamiento de las suscripciones de un plan.</summary>
    public class FilaSuscripcionPlan
    {
        public string Plan        { get; set; }
        public int    Total       { get; set; }
        public int    Activas     { get; set; }
        public int    Suspendidas { get; set; }
        public int    Vencidas    { get; set; }
        public int    PorVencer   { get; set; }
        /// <summary>Consumo promedio del saldo mensual 0..1.</summary>
        public double ConsumoPromedio { get; set; }

        public string ConsumoTexto => (ConsumoPromedio * 100).ToString("0") + "%";
    }

    /// <summary>
    /// CU-ANA-06 — Resultado del análisis del comportamiento de suscripciones. Resultado DERIVADO
    /// (no persiste): lo arma BLL.AnalisisSuscripciones a partir de Suscripcion + PlanSuscripcion.
    /// </summary>
    public class ResumenSuscripciones
    {
        public int    Total       { get; set; }
        public int    Activas     { get; set; }
        public int    Suspendidas { get; set; }
        public int    Vencidas    { get; set; }
        public int    PorVencer   { get; set; }
        public double ConsumoPromedio { get; set; }

        public List<FilaSuscripcionPlan> PorPlan { get; set; } = new List<FilaSuscripcionPlan>();
    }
}

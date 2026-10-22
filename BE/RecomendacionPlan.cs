using System;

namespace BE
{
    /// <summary>PN03 — Recomendación dentro de un plan de acción, con su evidencia y decisión del Gerente.</summary>
    public class RecomendacionPlan
    {
        public int      IdRecomendacion { get; set; }
        public int      IdPlanAccion    { get; set; }
        public TipoRecomendacion Tipo   { get; set; }
        public int?     IdEntidad       { get; set; }
        public string   NombreEntidad   { get; set; }
        public string   Evidencia       { get; set; }
        public AccionSugerida AccionSugerida { get; set; }
        public DecisionRecomendacion Decision { get; set; } = DecisionRecomendacion.Pendiente;
        public string   MotivoDecision  { get; set; }
        public bool     Ejecutada       { get; set; }
        public DateTime? FechaEjecucion { get; set; }

        public bool PuedeDecidir()  => Decision == DecisionRecomendacion.Pendiente;
        public bool PuedeEjecutar() => Decision == DecisionRecomendacion.Aprobada && !Ejecutada;
    }
}

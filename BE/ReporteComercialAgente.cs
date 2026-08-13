namespace BE
{
    /// <summary>
    /// CU-ANA-07 (PdN 8) — Fila del reporte comercial por Agente de Reservas. Resultado DERIVADO
    /// (no persiste): lo arma BLL.AnalisisComercial agrupando las reservas por el empleado que las
    /// registró. Terminología de ExperienceHub: "Agente de Reservas" (no "vendedor").
    /// </summary>
    public class ReporteComercialAgente
    {
        public int    IdEmpleado        { get; set; }
        public string Agente            { get; set; }

        /// <summary>Total de reservas registradas por el agente en el período.</summary>
        public int    Reservas          { get; set; }
        /// <summary>Reservas efectivas (Confirmada / Asistió / No asistió): no canceladas.</summary>
        public int    Efectivas         { get; set; }
        public int    Canceladas        { get; set; }
        /// <summary>Reservas con asistencia registrada (Asistió).</summary>
        public int    Asistidas         { get; set; }
        /// <summary>Clientes distintos atendidos por el agente.</summary>
        public int    ClientesAtendidos { get; set; }

        /// <summary>Tasa de cancelación 0..1 = Canceladas / Reservas.</summary>
        public double TasaCancelacion   { get; set; }

        // ── Presentación (para la grilla) ─────────────────────────────────────
        public string TasaCancelacionTexto => (TasaCancelacion * 100).ToString("0") + "%";
    }
}

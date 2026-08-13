namespace BE
{
    /// <summary>
    /// DTO que la BLL devuelve con el estado comercial de un cliente para crear reservas.
    /// No contiene lógica — es solo transporte de datos hacia la GUI.
    /// </summary>
    public class EstadoComercialCliente
    {
        // true si el cliente puede avanzar a reservar una experiencia
        public bool PuedeProceder { get; set; }

        // Código de bloqueo: "SIN_SUSCRIPCION" | "SUSCRIPCION_VENCIDA" | "SIN_RESERVAS" | null si puede proceder
        public string MotivoBloqueo { get; set; }

        // Datos de la suscripción para mostrar en el resumen de la GUI
        public string NombrePlan             { get; set; }
        public int    ReservasConsumidasMes  { get; set; }
        public int    ReservasMensuales      { get; set; }
        public System.DateTime FechaAlta        { get; set; }
        public System.DateTime? FechaVencimiento { get; set; }

        // Beneficios visibles del plan
        public bool AccesoPremium     { get; set; }
        public int  InvitadosMaximos  { get; set; }

        // Resultado de la validación de disponibilidad mensual
        public bool SuperaLimite       { get; set; }
        public int  ReservasDisponibles { get; set; }

        // Alerta proactiva: suscripción vigente pero vence pronto
        public bool SuscripcionProximaAVencer { get; set; }
        public int  DiasHastaVencimiento      { get; set; }
    }
}

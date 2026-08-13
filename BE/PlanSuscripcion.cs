namespace BE
{
    /// <summary>
    /// Entidad — Plan de Suscripción.
    /// Define los beneficios del cliente suscripto. Mapea la tabla [PlanSuscripcion].
    /// </summary>
    public class PlanSuscripcion
    {
        public int     IdPlan  { get; set; }
        public string  Nombre  { get; set; }
        public decimal Precio  { get; set; }
        public bool    Estado  { get; set; } = true;

        // ── Beneficios ────────────────────────────────────────────────────────

        /// <summary>Cantidad de reservas que el cliente puede hacer por mes.</summary>
        public int  ReservasMensuales      { get; set; }

        /// <summary>Anticipación máxima (en días) con la que puede reservarse una experiencia.</summary>
        public int  AnticipacionMaximaDias { get; set; }

        /// <summary>Habilita reservar experiencias marcadas como premium.</summary>
        public bool AccesoPremium          { get; set; }

        /// <summary>Otorga prioridad al entrar en la lista de espera.</summary>
        public bool PrioridadListaEspera   { get; set; }

        /// <summary>Cantidad de invitados que el titular puede sumar por reserva.</summary>
        public int  CantidadInvitados      { get; set; }

        // ── Comportamiento ────────────────────────────────────────────────────

        /// <summary>True si el plan permite hacer una reserva más dado el consumo actual del mes.</summary>
        public bool PermiteMasReservas(int consumidasEsteMes)
            => consumidasEsteMes < ReservasMensuales;

        /// <summary>Reservas que restan en el mes dado el consumo actual.</summary>
        public int ReservasRestantes(int consumidasEsteMes)
            => System.Math.Max(0, ReservasMensuales - consumidasEsteMes);

        /// <summary>True si el plan admite la cantidad de invitados solicitada.</summary>
        public bool AdmiteInvitados(int invitados)
            => invitados >= 0 && invitados <= CantidadInvitados;
    }
}

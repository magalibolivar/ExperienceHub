namespace BE
{
    /// <summary>
    /// Tipos de evento registrables en la BitacoraNegocio.
    /// Separa los eventos de negocio de los eventos de seguridad (Bitacora del sistema).
    /// </summary>
    public enum TipoEventoNegocio
    {
        // ── Reservas ──────────────────────────────────────────────────────────
        Reserva = 0,
        Confirmacion = 1,
        Cancelacion = 2,
        Asistencia = 3,
        Inasistencia = 4,

        // ── Experiencias ──────────────────────────────────────────────────────
        AltaExperiencia = 5,
        ModificacionExperiencia = 6,
        CambioEstadoExperiencia = 7,

        // ── Clientes ──────────────────────────────────────────────────────────
        AltaCliente = 8,
        ModificacionCliente = 9,
        BajaCliente = 10,

        // ── Suscripciones ─────────────────────────────────────────────────────
        RenovacionSuscripcion = 11,
        SuspensionSuscripcion = 12,

        // ── Lista de espera y calificaciones ──────────────────────────────────
        IngresoListaEspera = 13,
        PromocionListaEspera = 14,
        Calificacion = 15,

        // ── Genérico ──────────────────────────────────────────────────────────
        Reactivacion = 16
    }
}

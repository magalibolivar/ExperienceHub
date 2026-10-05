namespace BE
{
    /// <summary>
    /// Estado de una contratación de suscripción (PN01 — Comercialización de la suscripción).
    /// Ciclo: PendienteDePago → Pagada → Formalizada; o PendienteDePago → Cancelada
    /// (al agotarse los intentos de pago).
    /// </summary>
    public enum EstadoContratacion
    {
        /// <summary>Venta la registró; a la espera de que Caja confirme el cobro.</summary>
        PendienteDePago = 0,
        /// <summary>Caja confirmó el cobro y se emitió el comprobante.</summary>
        Pagada = 1,
        /// <summary>Se canceló tras agotar el máximo de intentos de pago.</summary>
        Cancelada = 2,
        /// <summary>Venta formalizó la suscripción vigente a partir del pago.</summary>
        Formalizada = 3
    }
}

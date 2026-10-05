using System;

namespace BE
{
    /// <summary>
    /// Entidad — Contratación de una suscripción (PN01 — Comercialización de la suscripción).
    /// Representa la venta en curso: Venta la crea PendienteDePago, Caja la cobra (y emite el
    /// comprobante en el mismo paso), y Venta la formaliza como suscripción vigente.
    /// Mapea la tabla [Contratacion].
    /// </summary>
    public class Contratacion
    {
        public int               IdContratacion    { get; set; }
        public int               IdCliente         { get; set; }
        public int               IdPlan            { get; set; }
        /// <summary>Importe a cobrar (precio del plan al momento de contratar).</summary>
        public decimal           Importe           { get; set; }

        public EstadoContratacion Estado           { get; set; } = EstadoContratacion.PendienteDePago;
        /// <summary>Intentos de pago fallidos acumulados.</summary>
        public int               IntentosPago      { get; set; }

        /// <summary>Medio con el que se cobró (null hasta que se concreta el pago).</summary>
        public MedioPago?        Medio             { get; set; }
        public DateTime?         FechaPago         { get; set; }

        /// <summary>Número de comprobante emitido al cobrar (CU02-CAJ).</summary>
        public string           NumeroComprobante  { get; set; }
        public DateTime?        FechaComprobante   { get; set; }

        /// <summary>Suscripción generada al formalizar (null mientras no se formaliza).</summary>
        public int?             IdSuscripcion      { get; set; }
        public DateTime         FechaAlta          { get; set; }

        // Datos por JOIN (no persisten)
        public string           NombreCliente      { get; set; }
        public string           NombrePlan         { get; set; }

        // ── Comportamiento (decisiones PURAS, sin BD ni sesión) ────────────────

        /// <summary>Solo se cobra una contratación que siga pendiente de pago.</summary>
        public bool PuedeCobrarse() => Estado == EstadoContratacion.PendienteDePago;

        /// <summary>Solo se formaliza una contratación ya pagada.</summary>
        public bool PuedeFormalizarse() => Estado == EstadoContratacion.Pagada;

        /// <summary>
        /// Estado resultante tras registrar un intento de pago fallido: si al sumar este intento
        /// se alcanza el máximo permitido, la contratación se cancela; si no, sigue pendiente.
        /// </summary>
        public static EstadoContratacion EstadoTrasIntentoFallido(int intentosPrevios, int maxIntentos)
            => (intentosPrevios + 1) >= maxIntentos
                ? EstadoContratacion.Cancelada
                : EstadoContratacion.PendienteDePago;

        /// <summary>
        /// Número de comprobante único a partir del identificador de la contratación y la fecha
        /// de cobro (CU02-CAJ). Formato: CMP-000042-20261002143015.
        /// </summary>
        public static string GenerarNumeroComprobante(int idContratacion, DateTime fechaCobro)
            => $"CMP-{idContratacion:D6}-{fechaCobro:yyyyMMddHHmmss}";
    }
}

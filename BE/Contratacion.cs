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
        /// <summary>Importe BASE a cobrar (precio del plan al momento de contratar, sin financiación).</summary>
        public decimal           Importe           { get; set; }

        public EstadoContratacion Estado           { get; set; } = EstadoContratacion.PendienteDePago;
        /// <summary>Intentos de pago fallidos acumulados.</summary>
        public int               IntentosPago      { get; set; }

        /// <summary>Medio con el que se cobró (null hasta que se concreta el pago).</summary>
        public MedioPago?        Medio             { get; set; }
        public DateTime?         FechaPago         { get; set; }

        // ── Financiación en cuotas (solo Tarjeta) — se SELLAN al cobrar ──────────
        /// <summary>Cantidad de cuotas elegidas al cobrar (1 = pago único; >1 solo con Tarjeta).</summary>
        public int               Cuotas            { get; set; } = 1;
        /// <summary>
        /// Recargo por financiación APLICADO al cobrar, en porcentaje (ej. 20.00 = 20%).
        /// Se persiste para no recalcular: si mañana cambian las tasas, el comprobante histórico
        /// conserva el valor con el que realmente se cobró.
        /// </summary>
        public decimal           RecargoPorcentaje { get; set; }
        /// <summary>Total financiado cobrado (Importe base + recargo), sellado al cobrar.</summary>
        public decimal           ImporteTotal      { get; set; }

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

        // ── Financiación en cuotas (reglas PURAS, sin BD ni sesión) ──────────────
        // Tabla de recargo por financiación, en porcentaje. Documentada acá como única fuente de
        // verdad (si se quisiera parametrizar por BD/config, este es el único punto a tocar).
        //   1 cuota  → 0%   (pago único, sin interés)
        //   3 cuotas → 10%
        //   6 cuotas → 20%
        //  12 cuotas → 40%
        public const decimal RECARGO_3_CUOTAS  = 10m;
        public const decimal RECARGO_6_CUOTAS  = 20m;
        public const decimal RECARGO_12_CUOTAS = 40m;

        /// <summary>
        /// Cantidades de cuotas ofrecidas según el medio de pago: solo Tarjeta financia; Efectivo
        /// y Transferencia son siempre pago único (1 cuota).
        /// </summary>
        public static int[] CuotasPermitidas(MedioPago medio)
            => medio == MedioPago.Tarjeta ? new[] { 1, 3, 6, 12 } : new[] { 1 };

        /// <summary>¿La cantidad de cuotas es válida para ese medio de pago?</summary>
        public static bool CuotasValidas(MedioPago medio, int cuotas)
            => System.Array.IndexOf(CuotasPermitidas(medio), cuotas) >= 0;

        /// <summary>Recargo por financiación (en %) correspondiente a la cantidad de cuotas.</summary>
        public static decimal RecargoPorCuotas(int cuotas)
        {
            switch (cuotas)
            {
                case 3:  return RECARGO_3_CUOTAS;
                case 6:  return RECARGO_6_CUOTAS;
                case 12: return RECARGO_12_CUOTAS;
                default: return 0m;   // 1 cuota (o valor no financiable) → sin recargo
            }
        }

        /// <summary>Total a cobrar (importe base + recargo por financiación), redondeado a 2 decimales.</summary>
        public static decimal ImporteConRecargo(decimal importeBase, int cuotas)
            => System.Math.Round(importeBase * (1m + RecargoPorCuotas(cuotas) / 100m), 2);

        /// <summary>Importe de cada cuota (total financiado dividido en la cantidad de cuotas).</summary>
        public static decimal ImportePorCuota(decimal importeBase, int cuotas)
            => System.Math.Round(ImporteConRecargo(importeBase, cuotas) / System.Math.Max(1, cuotas), 2);
    }
}

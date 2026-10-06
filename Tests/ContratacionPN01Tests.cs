using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN01 — Comercialización de la suscripción. Prueba la lógica PURA de la contratación
    /// (sin BD ni sesión):
    ///   • guardas de estado (cobrar / formalizar)
    ///   • regla de los 3 intentos de pago → cancelación
    ///   • generación del número de comprobante (CU02-CAJ)
    /// </summary>
    [TestClass]
    public class ContratacionPN01Tests
    {
        private static BE.Contratacion Con(BE.EstadoContratacion estado, int intentos = 0)
            => new BE.Contratacion { IdContratacion = 42, Estado = estado, IntentosPago = intentos };

        // ── Guardas de estado ───────────────────────────────────────────────────
        [TestMethod]
        public void Pendiente_PuedeCobrarse_NoFormalizarse()
        {
            var c = Con(BE.EstadoContratacion.PendienteDePago);
            Assert.IsTrue(c.PuedeCobrarse());
            Assert.IsFalse(c.PuedeFormalizarse());
        }

        [TestMethod]
        public void Pagada_PuedeFormalizarse_NoCobrarse()
        {
            var c = Con(BE.EstadoContratacion.Pagada);
            Assert.IsFalse(c.PuedeCobrarse());
            Assert.IsTrue(c.PuedeFormalizarse());
        }

        [TestMethod]
        public void CanceladaYFormalizada_NiCobro_NiFormalizacion()
        {
            foreach (var e in new[] { BE.EstadoContratacion.Cancelada, BE.EstadoContratacion.Formalizada })
            {
                var c = Con(e);
                Assert.IsFalse(c.PuedeCobrarse(),     $"No debería cobrarse en estado {e}.");
                Assert.IsFalse(c.PuedeFormalizarse(), $"No debería formalizarse en estado {e}.");
            }
        }

        // ── Regla de los 3 intentos ──────────────────────────────────────────────
        [TestMethod]
        public void PrimerYSegundoIntentoFallido_SiguePendiente()
        {
            Assert.AreEqual(BE.EstadoContratacion.PendienteDePago,
                BE.Contratacion.EstadoTrasIntentoFallido(intentosPrevios: 0, maxIntentos: 3));
            Assert.AreEqual(BE.EstadoContratacion.PendienteDePago,
                BE.Contratacion.EstadoTrasIntentoFallido(intentosPrevios: 1, maxIntentos: 3));
        }

        [TestMethod]
        public void TercerIntentoFallido_Cancela()
        {
            Assert.AreEqual(BE.EstadoContratacion.Cancelada,
                BE.Contratacion.EstadoTrasIntentoFallido(intentosPrevios: 2, maxIntentos: 3));
        }

        // ── Comprobante ──────────────────────────────────────────────────────────
        [TestMethod]
        public void NumeroComprobante_TieneFormatoEsperado()
        {
            var fecha = new DateTime(2026, 10, 2, 14, 30, 15);
            string nro = BE.Contratacion.GenerarNumeroComprobante(42, fecha);
            Assert.AreEqual("CMP-000042-20261002143015", nro);
        }

        [TestMethod]
        public void NumeroComprobante_DependeDelIdYLaFecha()
        {
            var fecha = new DateTime(2026, 1, 1, 0, 0, 0);
            Assert.AreNotEqual(
                BE.Contratacion.GenerarNumeroComprobante(1, fecha),
                BE.Contratacion.GenerarNumeroComprobante(2, fecha));
        }

        // ── Cuotas en tarjeta (financiación) ─────────────────────────────────────
        [TestMethod]
        public void CuotasPermitidas_SoloTarjetaFinancia()
        {
            CollectionAssert.AreEqual(new[] { 1, 3, 6, 12 }, BE.Contratacion.CuotasPermitidas(BE.MedioPago.Tarjeta));
            CollectionAssert.AreEqual(new[] { 1 },           BE.Contratacion.CuotasPermitidas(BE.MedioPago.Efectivo));
            CollectionAssert.AreEqual(new[] { 1 },           BE.Contratacion.CuotasPermitidas(BE.MedioPago.Transferencia));
        }

        [TestMethod]
        public void CuotasValidas_SegunMedio()
        {
            Assert.IsTrue(BE.Contratacion.CuotasValidas(BE.MedioPago.Tarjeta, 6));
            Assert.IsTrue(BE.Contratacion.CuotasValidas(BE.MedioPago.Efectivo, 1));
            Assert.IsFalse(BE.Contratacion.CuotasValidas(BE.MedioPago.Tarjeta, 5),  "5 no es un plan de cuotas ofrecido.");
            Assert.IsFalse(BE.Contratacion.CuotasValidas(BE.MedioPago.Efectivo, 3), "Efectivo no financia.");
            Assert.IsFalse(BE.Contratacion.CuotasValidas(BE.MedioPago.Transferencia, 12));
        }

        [TestMethod]
        public void RecargoPorCuotas_TablaDocumentada()
        {
            Assert.AreEqual(0m,  BE.Contratacion.RecargoPorCuotas(1));
            Assert.AreEqual(10m, BE.Contratacion.RecargoPorCuotas(3));
            Assert.AreEqual(20m, BE.Contratacion.RecargoPorCuotas(6));
            Assert.AreEqual(40m, BE.Contratacion.RecargoPorCuotas(12));
        }

        [TestMethod]
        public void ImporteConRecargo_AplicaElPorcentaje()
        {
            Assert.AreEqual(1000m, BE.Contratacion.ImporteConRecargo(1000m, 1));   // sin recargo
            Assert.AreEqual(1100m, BE.Contratacion.ImporteConRecargo(1000m, 3));   // +10%
            Assert.AreEqual(1200m, BE.Contratacion.ImporteConRecargo(1000m, 6));   // +20%
            Assert.AreEqual(1400m, BE.Contratacion.ImporteConRecargo(1000m, 12));  // +40%
        }

        [TestMethod]
        public void ImportePorCuota_DivideElTotalFinanciado()
        {
            // 1000 + 20% = 1200, en 6 cuotas = 200 c/u.
            Assert.AreEqual(200m, BE.Contratacion.ImportePorCuota(1000m, 6));
            // Pago único: la "cuota" es el total.
            Assert.AreEqual(1000m, BE.Contratacion.ImportePorCuota(1000m, 1));
        }
    }
}

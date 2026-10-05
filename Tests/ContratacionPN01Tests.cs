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
    }
}

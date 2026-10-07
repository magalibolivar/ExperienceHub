using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Ciclo de vida de la suscripción — lógica PURA (sin BD): vigencia por fecha, transiciones
    /// de estado y reinicio del consumo mensual.
    /// </summary>
    [TestClass]
    public class SuscripcionCicloVidaTests
    {
        private static readonly DateTime HOY = new DateTime(2026, 10, 7);

        private static BE.Suscripcion Sus(BE.EstadoSuscripcion estado, DateTime? vence)
            => new BE.Suscripcion { Estado = estado, FechaVencimiento = vence };

        // ── Vigencia por fecha ───────────────────────────────────────────────────
        [TestMethod]
        public void ActivaConVencimientoPasado_EstaVencida()
        {
            var s = Sus(BE.EstadoSuscripcion.Activa, HOY.AddDays(-1));
            Assert.IsTrue(s.EstaVencida(HOY));
            Assert.AreEqual(BE.EstadoSuscripcion.Vencida, s.EstadoVigenciaCalculado(HOY));
        }

        [TestMethod]
        public void ActivaVigente_NoVencida()
        {
            var s = Sus(BE.EstadoSuscripcion.Activa, HOY.AddDays(5));
            Assert.IsFalse(s.EstaVencida(HOY));
            Assert.AreEqual(BE.EstadoSuscripcion.Activa, s.EstadoVigenciaCalculado(HOY));
        }

        [TestMethod]
        public void SinVencimiento_NuncaVence()
        {
            var s = Sus(BE.EstadoSuscripcion.Activa, null);
            Assert.IsFalse(s.EstaVencida(HOY));
        }

        // ── Transiciones de estado ───────────────────────────────────────────────
        [TestMethod]
        public void SoloActiva_SePuedeSuspender()
        {
            Assert.IsTrue(Sus(BE.EstadoSuscripcion.Activa, null).PuedeSuspender());
            Assert.IsFalse(Sus(BE.EstadoSuscripcion.Suspendida, null).PuedeSuspender());
            Assert.IsFalse(Sus(BE.EstadoSuscripcion.Vencida, null).PuedeSuspender());
        }

        [TestMethod]
        public void SoloSuspendida_SePuedeReactivar()
        {
            Assert.IsTrue(Sus(BE.EstadoSuscripcion.Suspendida, null).PuedeReactivar());
            Assert.IsFalse(Sus(BE.EstadoSuscripcion.Activa, null).PuedeReactivar());
            Assert.IsFalse(Sus(BE.EstadoSuscripcion.Vencida, null).PuedeReactivar());
        }

        // ── Reinicio mensual del consumo ─────────────────────────────────────────
        [TestMethod]
        public void SinPeriodoSellado_DebeReiniciar()
        {
            Assert.IsTrue(BE.Suscripcion.DebeReiniciarConsumo(HOY, null));
        }

        [TestMethod]
        public void PeriodoDelMismoMes_NoReinicia()
        {
            Assert.IsFalse(BE.Suscripcion.DebeReiniciarConsumo(HOY, new DateTime(2026, 10, 1)));
        }

        [TestMethod]
        public void PeriodoDeMesAnterior_Reinicia()
        {
            Assert.IsTrue(BE.Suscripcion.DebeReiniciarConsumo(HOY, new DateTime(2026, 9, 1)));
        }

        // ── Alertas de vencimiento ───────────────────────────────────────────────
        [TestMethod]
        public void ProximaAVencer_DentroDeLaVentana()
        {
            // vence en 3 días → entra en la ventana de 7
            var s = new BE.Suscripcion { Estado = BE.EstadoSuscripcion.Activa, FechaVencimiento = DateTime.Today.AddDays(3) };
            Assert.IsTrue(s.ProximaAVencer(7));
            Assert.IsFalse(s.ProximaAVencer(2));
        }
    }
}

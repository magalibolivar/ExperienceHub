using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN02 — Lista de espera. Prueba la lógica PURA de la entrada (sin BD ni sesión):
    ///   • guardas del ciclo de la oferta (confirmar / rechazar solo si fue Ofrecida)
    ///   • vencimiento de la oferta según el plazo de vigencia
    /// </summary>
    [TestClass]
    public class ListaEsperaReglasTests
    {
        private const int HORAS = BLL.ListaEspera.HORAS_VIGENCIA_OFERTA;

        private static BE.ListaEspera Entrada(BE.EstadoListaEspera estado, DateTime? fechaOferta = null)
            => new BE.ListaEspera { Estado = estado, FechaOferta = fechaOferta };

        // ── Guardas de confirmar / rechazar ──────────────────────────────────────
        [TestMethod]
        public void SoloSeConfirmaORechazaUnaEntradaOfrecida()
        {
            Assert.IsTrue(Entrada(BE.EstadoListaEspera.Ofrecido).PuedeConfirmarse());
            Assert.IsTrue(Entrada(BE.EstadoListaEspera.Ofrecido).PuedeRechazarse());

            foreach (var e in new[] { BE.EstadoListaEspera.Esperando, BE.EstadoListaEspera.Confirmado, BE.EstadoListaEspera.Retirado })
            {
                Assert.IsFalse(Entrada(e).PuedeConfirmarse(), $"No debería confirmarse en estado {e}.");
                Assert.IsFalse(Entrada(e).PuedeRechazarse(),  $"No debería rechazarse en estado {e}.");
            }
        }

        // ── Vencimiento de la oferta ─────────────────────────────────────────────
        [TestMethod]
        public void OfertaDentroDelPlazo_NoVence()
        {
            var ahora = new DateTime(2026, 10, 6, 12, 0, 0);
            var entrada = Entrada(BE.EstadoListaEspera.Ofrecido, ahora.AddHours(-(HORAS - 1)));
            Assert.IsFalse(entrada.OfertaVencida(HORAS, ahora));
        }

        [TestMethod]
        public void OfertaPasadoElPlazo_Vence()
        {
            var ahora = new DateTime(2026, 10, 6, 12, 0, 0);
            var entrada = Entrada(BE.EstadoListaEspera.Ofrecido, ahora.AddHours(-(HORAS + 1)));
            Assert.IsTrue(entrada.OfertaVencida(HORAS, ahora));
        }

        [TestMethod]
        public void EntradaSinOferta_NuncaVence()
        {
            var ahora = DateTime.Now;
            Assert.IsFalse(Entrada(BE.EstadoListaEspera.Esperando).OfertaVencida(HORAS, ahora),
                "Una entrada en espera (sin FechaOferta) no puede estar vencida.");
            Assert.IsFalse(Entrada(BE.EstadoListaEspera.Ofrecido, null).OfertaVencida(HORAS, ahora),
                "Sin FechaOferta no hay plazo que vencer.");
        }
    }
}

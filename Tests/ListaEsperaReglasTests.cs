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
        // Plazo de vigencia de la oferta usado en estas pruebas puras. Las entradas de prueba no
        // tienen FechaHoraExperiencia, así que el margen antes del inicio (2º parámetro de
        // OfertaVencida) nunca se activa: solo se ejercita el vencimiento por plazo.
        private const int HORAS  = 24;
        private const int MARGEN = 24;

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
            Assert.IsFalse(entrada.OfertaVencida(HORAS, MARGEN, ahora));
        }

        [TestMethod]
        public void OfertaPasadoElPlazo_Vence()
        {
            var ahora = new DateTime(2026, 10, 6, 12, 0, 0);
            var entrada = Entrada(BE.EstadoListaEspera.Ofrecido, ahora.AddHours(-(HORAS + 1)));
            Assert.IsTrue(entrada.OfertaVencida(HORAS, MARGEN, ahora));
        }

        [TestMethod]
        public void EntradaSinOferta_NuncaVence()
        {
            var ahora = DateTime.Now;
            Assert.IsFalse(Entrada(BE.EstadoListaEspera.Esperando).OfertaVencida(HORAS, MARGEN, ahora),
                "Una entrada en espera (sin FechaOferta) no puede estar vencida.");
            Assert.IsFalse(Entrada(BE.EstadoListaEspera.Ofrecido, null).OfertaVencida(HORAS, MARGEN, ahora),
                "Sin FechaOferta no hay plazo que vencer.");
        }

        // ── Regla 5 — margen mínimo antes del inicio de la experiencia ──────────

        private static BE.ListaEspera EntradaConExp(DateTime? fechaOferta, DateTime? inicioExp)
            => new BE.ListaEspera
            {
                Estado = BE.EstadoListaEspera.Ofrecido,
                FechaOferta = fechaOferta,
                FechaHoraExperiencia = inicioExp
            };

        [TestMethod]
        public void OfertaVence_SiFaltaMenosDelMargenAntesDelInicio_AunqueElPlazoNoSeCumplio()
        {
            var ahora = new DateTime(2026, 10, 6, 12, 0, 0);
            // Oferta recién hecha (dentro del plazo de 24 h) pero la experiencia arranca en 10 h
            // (falta menos que el margen de 24 h) → debe vencer igual para liberar el cupo a tiempo.
            var entrada = EntradaConExp(ahora, ahora.AddHours(10));
            Assert.IsTrue(entrada.OfertaVencida(HORAS, MARGEN, ahora),
                "Debe vencer por acercarse al inicio aunque no se haya cumplido el plazo de respuesta.");
        }

        [TestMethod]
        public void OfertaNoVence_SiDentroDelPlazoYConMargenSuficiente()
        {
            var ahora = new DateTime(2026, 10, 6, 12, 0, 0);
            var entrada = EntradaConExp(ahora.AddHours(-1), ahora.AddHours(72));  // plazo OK + margen OK
            Assert.IsFalse(entrada.OfertaVencida(HORAS, MARGEN, ahora));
        }

        [TestMethod]
        public void HayMargenParaOferta_SegunProximidadAlInicio()
        {
            var ahora = new DateTime(2026, 10, 6, 12, 0, 0);
            Assert.IsTrue(EntradaConExp(null, ahora.AddHours(48)).HayMargenParaOferta(MARGEN, ahora),
                "Con 48 h hasta el inicio (margen 24 h) hay margen para ofrecer.");
            Assert.IsFalse(EntradaConExp(null, ahora.AddHours(10)).HayMargenParaOferta(MARGEN, ahora),
                "Con 10 h hasta el inicio (< margen 24 h) NO hay margen: el cupo queda para reserva directa.");
            Assert.IsTrue(EntradaConExp(null, null).HayMargenParaOferta(MARGEN, ahora),
                "Sin fecha de inicio conocida no se puede descartar: se permite ofrecer.");
        }
    }
}

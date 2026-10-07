using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Fidelización / recomendaciones — lógica PURA (sin BD): clasificación de fidelidad y
    /// scoring/orden de las recomendaciones.
    /// </summary>
    [TestClass]
    public class RecomendacionTests
    {
        // ── Nivel de fidelidad por umbrales ──────────────────────────────────────
        [TestMethod]
        public void Fidelidad_PorUmbrales()
        {
            Assert.AreEqual(BE.NivelFidelidad.Nuevo,     BE.Fidelidad.Clasificar(0));
            Assert.AreEqual(BE.NivelFidelidad.Ocasional, BE.Fidelidad.Clasificar(2));
            Assert.AreEqual(BE.NivelFidelidad.Frecuente, BE.Fidelidad.Clasificar(3));
            Assert.AreEqual(BE.NivelFidelidad.Frecuente, BE.Fidelidad.Clasificar(5));
            Assert.AreEqual(BE.NivelFidelidad.VIP,       BE.Fidelidad.Clasificar(6));
            Assert.AreEqual(BE.NivelFidelidad.VIP,       BE.Fidelidad.Clasificar(20));
        }

        // ── Scoring: afinidad de categoría pesa más que el puntaje ───────────────
        [TestMethod]
        public void Puntuar_AfinidadDeCategoriaPesaMas()
        {
            // 1 asistencia en la categoría (×2) vs una experiencia 5★ sin afinidad (×1)
            double conAfinidad = BE.ScoringRecomendacion.Puntuar(asistenciasEnCategoria: 1, puntajePromedio: 0);
            double soloPuntaje = BE.ScoringRecomendacion.Puntuar(asistenciasEnCategoria: 0, puntajePromedio: 1.9);
            Assert.IsTrue(conAfinidad > soloPuntaje);
        }

        [TestMethod]
        public void Puntuar_MejorPuntaje_DesempataSinAfinidad()
        {
            double a = BE.ScoringRecomendacion.Puntuar(0, 4.5);
            double b = BE.ScoringRecomendacion.Puntuar(0, 3.0);
            Assert.IsTrue(a > b);
        }

        // ── Motivo legible ───────────────────────────────────────────────────────
        [TestMethod]
        public void Motivo_SegunOrigen()
        {
            Assert.AreEqual("Te suele gustar Cata", BE.ScoringRecomendacion.Motivo(2, "Cata", 3.0));
            Assert.AreEqual("Muy bien calificada por otros clientes", BE.ScoringRecomendacion.Motivo(0, "Teatro", 4.5));
            Assert.AreEqual("Para descubrir algo nuevo", BE.ScoringRecomendacion.Motivo(0, "Teatro", 2.0));
        }

        // ── TopN: ordena desc y recorta ──────────────────────────────────────────
        [TestMethod]
        public void TopN_OrdenaYRecorta()
        {
            var lista = new List<BE.Recomendacion>
            {
                new BE.Recomendacion { Score = 1.0, Motivo = "a" },
                new BE.Recomendacion { Score = 9.0, Motivo = "b" },
                new BE.Recomendacion { Score = 5.0, Motivo = "c" },
            };
            var top2 = BE.ScoringRecomendacion.TopN(lista, 2);
            Assert.AreEqual(2, top2.Count);
            Assert.AreEqual("b", top2[0].Motivo);   // score 9 primero
            Assert.AreEqual("c", top2[1].Motivo);   // score 5 segundo
        }
    }
}

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Servicios.Serializacion;

namespace Tests
{
    /// <summary>
    /// A03 — Serialización/deserialización de la vista de análisis (BE.ConfiguracionInforme vía
    /// Servicios.Serializacion.SerializadorInformes). Pruebas PURAS en memoria/archivo temporal:
    /// NO tocan la BD. Verifican el round-trip (serializar → deserializar → igualdad), el manejo
    /// controlado de contenido corrupto y la persistencia a disco.
    /// </summary>
    [TestClass]
    public class SerializadorInformesTests
    {
        private static BE.ConfiguracionInforme Muestra() => new BE.ConfiguracionInforme
        {
            Nombre = "Trimestre premium - Palermo",
            PeriodoDesde = new DateTime(2026, 1, 1),
            PeriodoHasta = new DateTime(2026, 3, 31),
            FiltroCiudad = 7,
            FiltroCategoria = null,
            FiltroOrganizador = 3,
            Umbrales = new BE.UmbralesRecomendacion
            { ListaEsperaAlta = 8, OcupacionBaja = 35m, CalifBaja = 2.8m, InasistenciaAlta = 25m, PlanSubutilizado = 20m },
            GraficosVisibles = { "evolucion_mensual", "ocupacion", "estados" }
        };

        [TestMethod]
        public void RoundTrip_ConservaTodosLosCampos()
        {
            var original = Muestra();
            string xml = SerializadorInformes.Serializar(original);
            var vuelta = SerializadorInformes.Deserializar(xml);

            Assert.AreEqual(original.Nombre, vuelta.Nombre);
            Assert.AreEqual(original.PeriodoDesde, vuelta.PeriodoDesde);
            Assert.AreEqual(original.PeriodoHasta, vuelta.PeriodoHasta);
            Assert.AreEqual(original.FiltroCiudad, vuelta.FiltroCiudad);
            Assert.AreEqual(original.FiltroCategoria, vuelta.FiltroCategoria, "El filtro nulo debe volver nulo.");
            Assert.AreEqual(original.FiltroOrganizador, vuelta.FiltroOrganizador);
            Assert.AreEqual(original.Umbrales.ListaEsperaAlta, vuelta.Umbrales.ListaEsperaAlta);
            Assert.AreEqual(original.Umbrales.OcupacionBaja, vuelta.Umbrales.OcupacionBaja);
            Assert.AreEqual(original.Umbrales.CalifBaja, vuelta.Umbrales.CalifBaja);
            CollectionAssert.AreEqual(original.GraficosVisibles, vuelta.GraficosVisibles);
        }

        [TestMethod]
        public void Serializar_ProduceXmlLegible()
        {
            string xml = SerializadorInformes.Serializar(Muestra());
            StringAssert.Contains(xml, "ConfiguracionInforme");
            StringAssert.Contains(xml, "Palermo");
        }

        [TestMethod]
        [ExpectedException(typeof(BE.AppException))]
        public void Deserializar_Corrupto_LanzaAppException()
        {
            SerializadorInformes.Deserializar("<<< esto no es XML válido >>>");
        }

        [TestMethod]
        [ExpectedException(typeof(BE.AppException))]
        public void Deserializar_Vacio_LanzaAppException()
        {
            SerializadorInformes.Deserializar("   ");
        }

        [TestMethod]
        public void GuardarYCargar_ArchivoTemporal_Reconstruye()
        {
            var original = Muestra();
            original.Nombre = "test_" + Guid.NewGuid().ToString("N");
            string ruta = null;
            try
            {
                ruta = SerializadorInformes.Guardar(original);
                Assert.IsTrue(File.Exists(ruta), "El archivo de la vista debe existir.");
                var vuelta = SerializadorInformes.Cargar(ruta);
                Assert.AreEqual(original.Nombre, vuelta.Nombre);
                Assert.AreEqual(original.FiltroOrganizador, vuelta.FiltroOrganizador);
                Assert.IsTrue(vuelta.GuardadoEn > DateTime.MinValue, "Guardar debe sellar GuardadoEn.");
            }
            finally
            {
                if (ruta != null && File.Exists(ruta)) File.Delete(ruta);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(BE.AppException))]
        public void Cargar_RutaInexistente_LanzaAppException()
        {
            SerializadorInformes.Cargar(Path.Combine(Path.GetTempPath(), "no_existe_" + Guid.NewGuid().ToString("N") + ".xml"));
        }
    }
}

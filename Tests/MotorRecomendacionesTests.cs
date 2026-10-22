using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN03 — Motor de recomendaciones (BLL.MotorRecomendaciones): decisión PURA, sin BD.
    /// Verifica que cada regla DISPARA en/pasado su umbral y NO dispara del lado inocuo,
    /// y que la recomendación trae su acción sugerida y evidencia.
    /// </summary>
    [TestClass]
    public class MotorRecomendacionesTests
    {
        private static BE.UmbralesRecomendacion U() => new BE.UmbralesRecomendacion
        { ListaEsperaAlta = 5, OcupacionBaja = 40m, CalifBaja = 3.0m, InasistenciaAlta = 30m, PlanSubutilizado = 25m };

        private static List<BE.RecomendacionGenerada> Gen(BE.TableroPeriodo t) => BLL.MotorRecomendaciones.Generar(t, U());

        // ── ListaEsperaAlta ──────────────────────────────────────────────────────
        [TestMethod]
        public void ListaEsperaAlta_EnUmbral_Dispara_ProgramarEdicion()
        {
            var t = new BE.TableroPeriodo { Experiencias = { new BE.KpiExperiencia { IdExperiencia = 1, Nombre = "Cata", CupoMaximo = 10, LugaresReservados = 10, EnEspera = 5 } } };
            var r = Gen(t).Where(x => x.Tipo == BE.TipoRecomendacion.ListaEsperaAlta).ToList();
            Assert.AreEqual(1, r.Count, "5 en espera (umbral 5) debe disparar.");
            Assert.AreEqual(BE.AccionSugerida.ProgramarEdicion, r[0].AccionSugerida);
            StringAssert.Contains(r[0].Evidencia, "5");
        }

        [TestMethod]
        public void ListaEsperaAlta_PorDebajo_NoDispara()
        {
            var t = new BE.TableroPeriodo { Experiencias = { new BE.KpiExperiencia { IdExperiencia = 1, Nombre = "Cata", CupoMaximo = 10, LugaresReservados = 10, EnEspera = 4 } } };
            Assert.IsFalse(Gen(t).Any(x => x.Tipo == BE.TipoRecomendacion.ListaEsperaAlta), "4 en espera (< 5) no debe disparar.");
        }

        // ── OcupacionBaja ────────────────────────────────────────────────────────
        [TestMethod]
        public void OcupacionBaja_PorDebajoDelUmbral_Dispara_Cancelar()
        {
            // 3/10 = 30% < 40%
            var t = new BE.TableroPeriodo { Experiencias = { new BE.KpiExperiencia { IdExperiencia = 2, Nombre = "Trekking", CupoMaximo = 10, LugaresReservados = 3, EnEspera = 0 } } };
            var r = Gen(t).Where(x => x.Tipo == BE.TipoRecomendacion.OcupacionBaja).ToList();
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(BE.AccionSugerida.CancelarExperiencia, r[0].AccionSugerida);
        }

        [TestMethod]
        public void OcupacionBaja_EnUmbralOArriba_NoDispara()
        {
            // 4/10 = 40% NO es < 40
            var t = new BE.TableroPeriodo { Experiencias = { new BE.KpiExperiencia { IdExperiencia = 2, Nombre = "Trekking", CupoMaximo = 10, LugaresReservados = 4, EnEspera = 0 } } };
            Assert.IsFalse(Gen(t).Any(x => x.Tipo == BE.TipoRecomendacion.OcupacionBaja), "40% no es < 40%.");
        }

        // ── OrganizadorRevisar ───────────────────────────────────────────────────
        [TestMethod]
        public void Organizador_CalificacionBaja_Dispara_Suspender()
        {
            var t = new BE.TableroPeriodo { Organizadores = { new BE.KpiOrganizador { IdOrganizador = 3, Nombre = "Org A", CalificacionPromedio = 2.5m, Calificaciones = 4, Asistencias = 10, Inasistencias = 0 } } };
            var r = Gen(t).Where(x => x.Tipo == BE.TipoRecomendacion.OrganizadorRevisar).ToList();
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(BE.AccionSugerida.SuspenderOrganizador, r[0].AccionSugerida);
        }

        [TestMethod]
        public void Organizador_InasistenciaAlta_Dispara()
        {
            // 4 inasist / 10 = 40% > 30%
            var t = new BE.TableroPeriodo { Organizadores = { new BE.KpiOrganizador { IdOrganizador = 3, Nombre = "Org B", CalificacionPromedio = 4.5m, Calificaciones = 6, Asistencias = 6, Inasistencias = 4 } } };
            Assert.AreEqual(1, Gen(t).Count(x => x.Tipo == BE.TipoRecomendacion.OrganizadorRevisar));
        }

        [TestMethod]
        public void Organizador_BuenoYPuntual_NoDispara()
        {
            var t = new BE.TableroPeriodo { Organizadores = { new BE.KpiOrganizador { IdOrganizador = 3, Nombre = "Org C", CalificacionPromedio = 4.6m, Calificaciones = 8, Asistencias = 9, Inasistencias = 1 } } };
            Assert.IsFalse(Gen(t).Any(x => x.Tipo == BE.TipoRecomendacion.OrganizadorRevisar), "Buena calif + baja inasistencia no dispara.");
        }

        [TestMethod]
        public void Organizador_SinCalificaciones_NoDisparaPorCalif()
        {
            // Sin calificaciones (0) no debe interpretarse como "calificación baja".
            var t = new BE.TableroPeriodo { Organizadores = { new BE.KpiOrganizador { IdOrganizador = 3, Nombre = "Org D", CalificacionPromedio = 0m, Calificaciones = 0, Asistencias = 5, Inasistencias = 0 } } };
            Assert.IsFalse(Gen(t).Any(x => x.Tipo == BE.TipoRecomendacion.OrganizadorRevisar));
        }

        // ── PlanSubutilizado ─────────────────────────────────────────────────────
        [TestMethod]
        public void Plan_UsoBajo_Dispara_RevisarPlan()
        {
            // 10/100 = 10% < 25%
            var t = new BE.TableroPeriodo { Planes = { new BE.KpiPlan { IdPlan = 7, Nombre = "Básico", Suscripciones = 10, ReservasUsadas = 10, ReservasPermitidas = 100 } } };
            var r = Gen(t).Where(x => x.Tipo == BE.TipoRecomendacion.PlanSubutilizado).ToList();
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(BE.AccionSugerida.RevisarPlan, r[0].AccionSugerida);
        }

        [TestMethod]
        public void Plan_UsoSuficiente_NoDispara()
        {
            // 30/100 = 30% >= 25%
            var t = new BE.TableroPeriodo { Planes = { new BE.KpiPlan { IdPlan = 7, Nombre = "Plus", Suscripciones = 10, ReservasUsadas = 30, ReservasPermitidas = 100 } } };
            Assert.IsFalse(Gen(t).Any(x => x.Tipo == BE.TipoRecomendacion.PlanSubutilizado));
        }

        // ── Integración de reglas: tablero vacío no genera nada ──────────────────
        [TestMethod]
        public void TableroVacio_NoGeneraRecomendaciones()
        {
            Assert.AreEqual(0, Gen(new BE.TableroPeriodo()).Count);
        }
    }
}

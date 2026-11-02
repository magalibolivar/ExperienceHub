using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN03 — Ciclo Antes/Después del plan de acción (BE.PlanAccion). Cubre el bug corregido: el
    /// "Después" NO debe medirse contra un período futuro sin datos; debe esperar a que transcurra
    /// una ventana de observación real desde el cierre. Son funciones PURAS (reciben "ahora" como
    /// parámetro) — testeables sin BD y sin depender del reloj del sistema operativo.
    /// </summary>
    [TestClass]
    public class PlanAccionCicloTests
    {
        private static BE.PlanAccion Plan(DateTime desde, DateTime hasta, BE.EstadoPlanAccion estado, DateTime? cierre = null)
            => new BE.PlanAccion { PeriodoDesde = desde, PeriodoHasta = hasta, Estado = estado, FechaCierre = cierre };

        [TestMethod]
        public void DuracionPeriodoDias_CalculaDiferenciaEnDias()
        {
            var p = Plan(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), BE.EstadoPlanAccion.Borrador);
            Assert.AreEqual(30, p.DuracionPeriodoDias);
        }

        [TestMethod]
        public void DuracionPeriodoDias_MismaFecha_DaMinimoUnDia()
        {
            var p = Plan(new DateTime(2026, 1, 1), new DateTime(2026, 1, 1), BE.EstadoPlanAccion.Borrador);
            Assert.AreEqual(1, p.DuracionPeriodoDias, "Un período de 0 días no debe producir división por cero ni ventana vacía.");
        }

        [TestMethod]
        public void FechaEvaluacionPrevista_PlanNoCerrado_EsNull()
        {
            var p = Plan(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), BE.EstadoPlanAccion.EnEjecucion);
            Assert.IsNull(p.FechaEvaluacionPrevista, "Sin fecha de cierre no hay fecha de evaluación prevista.");
        }

        [TestMethod]
        public void FechaEvaluacionPrevista_SumaDuracionDelPeriodoAlCierre()
        {
            var cierre = new DateTime(2026, 6, 1);
            var p = Plan(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), BE.EstadoPlanAccion.Cerrado, cierre);
            Assert.AreEqual(cierre.AddDays(30), p.FechaEvaluacionPrevista);
        }

        [TestMethod]
        public void PeriodoObservacionCumplido_PlanNoCerrado_SiempreFalso()
        {
            // Aunque "ahora" sea muy posterior, un plan EnEjecucion nunca es evaluable: no hay fecha de cierre.
            var p = Plan(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), BE.EstadoPlanAccion.EnEjecucion);
            Assert.IsFalse(p.PeriodoObservacionCumplido(new DateTime(2030, 1, 1)));
        }

        [TestMethod]
        public void PeriodoObservacionCumplido_AntesDeLaFechaPrevista_EsFalso()
        {
            // ESTE es el bug corregido: el día siguiente al cierre NO debe considerarse evaluable
            // (antes el sistema medía "Después" ese mismo momento, sobre un período futuro sin datos).
            var cierre = new DateTime(2026, 6, 1);
            var p = Plan(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), BE.EstadoPlanAccion.Cerrado, cierre);
            Assert.IsFalse(p.PeriodoObservacionCumplido(cierre.AddDays(1)),
                "Al día siguiente del cierre la ventana de observación (30 días) no transcurrió todavía.");
        }

        [TestMethod]
        public void PeriodoObservacionCumplido_ExactoEnLaFechaPrevista_EsTrue()
        {
            var cierre = new DateTime(2026, 6, 1);
            var p = Plan(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), BE.EstadoPlanAccion.Cerrado, cierre);
            Assert.IsTrue(p.PeriodoObservacionCumplido(cierre.AddDays(30)));
        }

        [TestMethod]
        public void PeriodoObservacionCumplido_DespuesDeLaFechaPrevista_EsTrue()
        {
            var cierre = new DateTime(2026, 6, 1);
            var p = Plan(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), BE.EstadoPlanAccion.Cerrado, cierre);
            Assert.IsTrue(p.PeriodoObservacionCumplido(cierre.AddDays(45)));
        }

        [TestMethod]
        public void PuedeCerrar_SoloDesdeEnEjecucion()
        {
            Assert.IsTrue(Plan(DateTime.Today, DateTime.Today, BE.EstadoPlanAccion.EnEjecucion).PuedeCerrar());
            Assert.IsFalse(Plan(DateTime.Today, DateTime.Today, BE.EstadoPlanAccion.Borrador).PuedeCerrar());
            Assert.IsFalse(Plan(DateTime.Today, DateTime.Today, BE.EstadoPlanAccion.Aprobado).PuedeCerrar());
            Assert.IsFalse(Plan(DateTime.Today, DateTime.Today, BE.EstadoPlanAccion.Cerrado).PuedeCerrar());
        }
    }

    /// <summary>
    /// PN03 — Clasificación del resultado Antes/Después por indicador (BLL.EvaluacionKpi). Verifica que
    /// la dirección de "mejora" sea correcta por KPI: para algunos, bajar es mejor (demanda
    /// insatisfecha, inasistencias); para otros, subir es mejor (altas, ingresos). Antes de esta
    /// corrección se usaba el signo crudo del delta, que clasificaba "subió la lista de espera" como
    /// una mejora.
    /// </summary>
    [TestClass]
    public class EvaluacionKpiTests
    {
        [TestMethod]
        public void EnEspera_Bajar_EsMejora()
        {
            Assert.AreEqual(BLL.EvaluacionKpi.Resultado.Mejoro, BLL.EvaluacionKpi.Clasificar("EnEspera", -5m));
        }

        [TestMethod]
        public void EnEspera_Subir_EsDeterioro()
        {
            // Clave del bug: ANTES se interpretaba delta>0 como "mejora" sin importar el indicador.
            Assert.AreEqual(BLL.EvaluacionKpi.Resultado.Empeoro, BLL.EvaluacionKpi.Clasificar("EnEspera", 5m));
        }

        [TestMethod]
        public void Altas_Subir_EsMejora()
        {
            Assert.AreEqual(BLL.EvaluacionKpi.Resultado.Mejoro, BLL.EvaluacionKpi.Clasificar("Altas", 10m));
        }

        [TestMethod]
        public void Altas_Bajar_EsDeterioro()
        {
            Assert.AreEqual(BLL.EvaluacionKpi.Resultado.Empeoro, BLL.EvaluacionKpi.Clasificar("Altas", -10m));
        }

        [TestMethod]
        public void IngresosPlanes_Subir_EsMejora()
        {
            Assert.AreEqual(BLL.EvaluacionKpi.Resultado.Mejoro, BLL.EvaluacionKpi.Clasificar("IngresosPlanes", 1000m));
        }

        [TestMethod]
        public void SinVariacion_EsSinCambios()
        {
            Assert.AreEqual(BLL.EvaluacionKpi.Resultado.SinCambio, BLL.EvaluacionKpi.Clasificar("Inasistencias", 0m));
        }

        [TestMethod]
        public void CancelacionesATiempo_EsNeutroInformativo()
        {
            // Que suban las cancelaciones a tiempo no es necesariamente bueno ni malo (depende de si
            // subió también el volumen total de reservas) → se clasifica como neutro, no se opina.
            Assert.AreEqual(BLL.EvaluacionKpi.Resultado.Neutro, BLL.EvaluacionKpi.Clasificar("CancelacionesATiempo", 5m));
        }
    }
}

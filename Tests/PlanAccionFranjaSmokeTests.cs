using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN03 — Prueba de integración end-to-end del ciclo completo del plan de acción tal como lo
    /// recorre la franja de "Próxima acción" de PlanesAccionForm (GUI\Planificacion\PlanesAccionForm.cs,
    /// método ConstruirFranjaAccion → GUI.Planificacion.PlanificacionTextos.ProximaAccion):
    ///   Crear plan → Decidir (aprobar sin motivo / descartar con motivo) → Aprobar plan →
    ///   Marcar en ejecución → Ejecutar recomendación → Cerrar plan → Evaluar (rechazado: período
    ///   no cumplido). Ejercita BLL.PlanAccionService real contra la BD real (sin mockear nada).
    /// Tests no referencia GUI.csproj (evita acoplar el proyecto de pruebas a WinForms) — en cada
    /// paso se verifican directamente los MISMOS guards de BE que ProximaAccion lee
    /// (PuedeAprobar/PuedeEjecutar/PuedeCerrar/PeriodoObservacionCumplido/TieneEvaluacion): si estos
    /// guards están correctos, la franja —que es un wrapper fino sobre ellos— también lo está.
    /// No se tocó BLL.PlanAccionService ni ninguna regla de negocio para este rediseño.
    /// </summary>
    [TestClass]
    public class PlanAccionFranjaSmokeTests
    {
        private readonly DAL.Acceso _acceso = DAL.Acceso.GetInstance();

        [TestMethod]
        public void CicloCompleto_DelPlan_CoincideConLaFranjaDeProximaAccion()
        {
            if (!_acceso.VerificarConexion())
                Assert.Inconclusive("Sin conexión a ExperienceHubDB — se omite el test de integración.");

            Seguridad.SessionManager.Login(new BE.Usuario { Id = 1, Username = "smoketest.pn03", Perfil = "Administrador" });

            var svc = new BLL.PlanAccionService();
            int idPlan = 0;
            try
            {
                // ── 1) Crear plan con 2 recomendaciones (quedan Pendiente) ──────────────────
                var tablero = new BE.TableroPeriodo { Desde = DateTime.Today.AddDays(-30), Hasta = DateTime.Today };
                var recs = new List<BE.RecomendacionGenerada>
                {
                    new BE.RecomendacionGenerada { Tipo = BE.TipoRecomendacion.OcupacionBaja, AccionSugerida = BE.AccionSugerida.NoReprogramar, IdEntidad = 1, NombreEntidad = "SMOKE-A", Evidencia = "test" },
                    new BE.RecomendacionGenerada { Tipo = BE.TipoRecomendacion.PlanSubutilizado, AccionSugerida = BE.AccionSugerida.RevisarPlan, IdEntidad = 2, NombreEntidad = "SMOKE-B", Evidencia = "test" },
                };
                idPlan = svc.CrearPlan("SMOKE TEST — franja próxima acción", tablero, recs, null, null, null, 1);
                Assert.IsTrue(idPlan > 0, "CrearPlan debe devolver un Id válido.");

                var plan = svc.ObtenerPlan(idPlan);
                Assert.AreEqual(BE.EstadoPlanAccion.Borrador, plan.Estado);

                // La franja ("Próxima acción") muestra "Aprobar plan" exactamente cuando PuedeAprobar()
                // es true. DESDE EL VAMOS, aunque las 2 recomendaciones sigan Pendiente — PuedeAprobar()
                // no exige decidirlas (no se inventó esa restricción al rediseñar la UI).
                Assert.IsTrue(plan.PuedeAprobar(), "Con el plan en Borrador, la franja debe ofrecer Aprobar plan sin exigir decidir las recomendaciones primero.");

                // ── 2) Decidir: aprobar SIN motivo (1 clic, como ahora hace la GUI) + descartar CON motivo ──
                var pendientes = svc.ObtenerRecomendaciones(idPlan);
                Assert.AreEqual(2, pendientes.Count(r => r.Decision == BE.DecisionRecomendacion.Pendiente));
                var recA = pendientes.First(r => r.NombreEntidad == "SMOKE-A");
                var recB = pendientes.First(r => r.NombreEntidad == "SMOKE-B");

                svc.Decidir(recA.IdRecomendacion, true, null);   // Aprobar, sin motivo — no debe fallar
                svc.Decidir(recB.IdRecomendacion, false, "motivo de prueba");

                var decididas = svc.ObtenerRecomendaciones(idPlan);
                var decA = decididas.First(r => r.IdRecomendacion == recA.IdRecomendacion);
                var decB = decididas.First(r => r.IdRecomendacion == recB.IdRecomendacion);
                Assert.AreEqual(BE.DecisionRecomendacion.Aprobada, decA.Decision);
                Assert.IsNull(decA.MotivoDecision, "Aprobar sin motivo debe persistir NULL, no una cadena vacía forzada.");
                Assert.AreEqual(BE.DecisionRecomendacion.Descartada, decB.Decision);
                Assert.AreEqual("motivo de prueba", decB.MotivoDecision);

                // ── 3) Aprobar el plan ───────────────────────────────────────────────────────
                svc.AprobarPlan(idPlan);
                plan = svc.ObtenerPlan(idPlan);
                Assert.AreEqual(BE.EstadoPlanAccion.Aprobado, plan.Estado);
                Assert.IsFalse(plan.PuedeAprobar(), "Ya aprobado: PuedeAprobar debe pasar a false (sin doble aprobación).");
                Assert.IsTrue(plan.PuedeEjecutar(), "Aprobado → la franja debe ofrecer Marcar en ejecución.");

                // ── 4) Marcar en ejecución ───────────────────────────────────────────────────
                svc.MarcarEnEjecucion(idPlan);
                plan = svc.ObtenerPlan(idPlan);
                Assert.AreEqual(BE.EstadoPlanAccion.EnEjecucion, plan.Estado);

                // Con una tarea aprobada todavía sin ejecutar, la franja NO bloquea el cierre:
                // PuedeCerrar() ya es true (la BLL no exige ejecutar todas las tareas aprobadas antes
                // de cerrar) — la franja SÍ ofrece "Cerrar plan", pero su línea de contexto avisa que
                // queda 1 tarea aprobada sin ejecutar (ver ContextoFranja en PlanesAccionForm.cs).
                Assert.IsTrue(plan.PuedeCerrar());
                int aprobSinEjecutar = svc.ObtenerRecomendaciones(idPlan)
                    .Count(r => r.Decision == BE.DecisionRecomendacion.Aprobada && !r.Ejecutada);
                Assert.AreEqual(1, aprobSinEjecutar, "Debe haber exactamente 1 tarea aprobada sin ejecutar (la del motivo del aviso contextual).");

                // ── 5) Ejecutar la recomendación aprobada ────────────────────────────────────
                var aprobadaParaEjecutar = svc.ObtenerRecomendaciones(idPlan).First(r => r.IdRecomendacion == recA.IdRecomendacion);
                Assert.IsTrue(aprobadaParaEjecutar.PuedeEjecutar());
                svc.EjecutarRecomendacion(aprobadaParaEjecutar);
                var ejecutada = svc.ObtenerRecomendaciones(idPlan).First(r => r.IdRecomendacion == recA.IdRecomendacion);
                Assert.IsTrue(ejecutada.Ejecutada);

                // ── 6) Cerrar el plan — el snapshot "Después" NO debe existir todavía ───────────
                Assert.IsFalse(svc.TieneEvaluacion(idPlan), "Antes de cerrar no debe existir snapshot Después.");
                svc.CerrarPlan(idPlan);
                plan = svc.ObtenerPlan(idPlan);
                Assert.AreEqual(BE.EstadoPlanAccion.Cerrado, plan.Estado);
                Assert.IsFalse(svc.TieneEvaluacion(idPlan),
                    "Cerrar el plan NO debe registrar el snapshot Después (bug corregido en el diagrama/documentación: eso ocurre recién en EvaluarResultados).");

                // Con el período de observación todavía en curso, la franja NO debe ofrecer el botón
                // "Evaluar resultados" (solo el mensaje "evaluable a partir de...").
                Assert.IsFalse(plan.PeriodoObservacionCumplido(DateTime.Now),
                    "Con el período de observación en curso, la franja no debe ofrecer ningún botón de evaluación.");

                // ── 7) EvaluarResultados debe RECHAZAR antes de tiempo (la BLL ya lo garantizaba) ──
                try
                {
                    svc.EvaluarResultados(idPlan);
                    Assert.Fail("EvaluarResultados debería rechazar un plan cuyo período de observación no transcurrió.");
                }
                catch (BE.AppException ex)
                {
                    Assert.AreEqual("err.bll.plan.periodo_pendiente", ex.Clave);
                }
                Assert.IsFalse(svc.TieneEvaluacion(idPlan), "El intento rechazado no debe haber creado un snapshot Después.");
            }
            finally
            {
                if (idPlan > 0)
                {
                    Exec("DELETE FROM KpiSnapshot WHERE IdPlanAccion=@p", idPlan);
                    Exec("DELETE FROM RecomendacionPlan WHERE IdPlanAccion=@p", idPlan);
                    Exec("DELETE FROM PlanAccion WHERE IdPlanAccion=@p", idPlan);
                }
                Seguridad.SessionManager.Logout();
            }
        }

        private void Exec(string sql, int idPlan)
        {
            try { _acceso.Escribir(sql, new[] { new SqlParameter("@p", idPlan) }); } catch { }
        }
    }
}

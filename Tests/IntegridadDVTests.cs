using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BLL;

namespace Tests
{
    /// <summary>
    /// Criterio UNIFICADO de integridad DV (PN03, punto 2): sin auto-reparación silenciosa.
    ///   • discrepancia de DVH/DVV o tabla no verificable → COMPROMETIDO;
    ///   • DVH en NULL SIN marcador de re-sellado → COMPROMETIDO;
    ///   • con marcador (puesto por una migración) → re-sellado AUTORIZADO y marcador consumido;
    ///   • gate de login: no-Administrador con integridad comprometida queda bloqueado.
    /// Los tests puros no tocan BD; los de integración se omiten (Inconclusive) sin conexión y
    /// SIEMPRE re-sellan la tabla en finally para no dejar la base de desarrollo inconsistente.
    /// </summary>
    [TestClass]
    public class IntegridadDVTests
    {
        private readonly DAL.Acceso _acceso = DAL.Acceso.GetInstance();

        // ── Decisión pura EvaluarEstadoDV ───────────────────────────────────────

        [TestMethod]
        public void EvaluarEstadoDV_DvhAlterado_SinMarcador_EsComprometido()
        {
            // filas>0, no todos null, DVV presente, sin marcador, DVH no coincide
            var e = Configuracion.EvaluarEstadoDV(cantFilas: 3, todosDvhNull: false, dvvAusente: false,
                hayMarcadorReseal: false, dvhCoincide: false, dvvCoincide: true);
            Assert.AreEqual(Configuracion.EstadoVerificacionDV.Comprometido, e);
        }

        [TestMethod]
        public void EvaluarEstadoDV_DvhNull_SinMarcador_EsComprometido()
        {
            var e = Configuracion.EvaluarEstadoDV(cantFilas: 3, todosDvhNull: true, dvvAusente: true,
                hayMarcadorReseal: false, dvhCoincide: false, dvvCoincide: false);
            Assert.AreEqual(Configuracion.EstadoVerificacionDV.Comprometido, e);
        }

        [TestMethod]
        public void EvaluarEstadoDV_ConMarcador_EsPendienteReseal()
        {
            var e = Configuracion.EvaluarEstadoDV(cantFilas: 3, todosDvhNull: true, dvvAusente: true,
                hayMarcadorReseal: true, dvhCoincide: false, dvvCoincide: false);
            Assert.AreEqual(Configuracion.EstadoVerificacionDV.PendienteReseal, e);
        }

        [TestMethod]
        public void EvaluarEstadoDV_Coincide_EsIntegro()
        {
            var e = Configuracion.EvaluarEstadoDV(cantFilas: 3, todosDvhNull: false, dvvAusente: false,
                hayMarcadorReseal: false, dvhCoincide: true, dvvCoincide: true);
            Assert.AreEqual(Configuracion.EstadoVerificacionDV.Integro, e);
        }

        // ── Error de verificación → no cuenta como íntegra ──────────────────────

        [TestMethod]
        public void ErrorDeVerificacion_GenericoNoEsEsquema_NoCuentaComoIntegra()
        {
            // Un error de esquema sin migrar se tolera; cualquier otro error NO (→ no verificable).
            Assert.IsTrue(Configuracion.EsErrorEsquemaNoMigrado(new Exception("Invalid column name 'DVH'.")),
                "Columna DVH inexistente = base sin migrar (tolerado).");
            Assert.IsFalse(Configuracion.EsErrorEsquemaNoMigrado(new Exception("Timeout expirado / deadlock.")),
                "Un error genérico NO es 'sin migrar': la tabla no se puede dar por íntegra.");
        }

        // ── Gate de login ────────────────────────────────────────────────────────

        [TestMethod]
        public void PuedeIngresar_NoAdminConIntegridadComprometida_Bloqueado()
        {
            Assert.IsFalse(Configuracion.PuedeIngresar(integridadOk: false, esAdministrador: false),
                "Un no-Administrador NO debe ingresar con la integridad comprometida.");
            Assert.IsTrue(Configuracion.PuedeIngresar(integridadOk: false, esAdministrador: true),
                "El Administrador ingresa para reparar/restaurar.");
            Assert.IsTrue(Configuracion.PuedeIngresar(integridadOk: true, esAdministrador: false),
                "Con integridad OK, cualquiera ingresa.");
        }

        // ── Integración: detección real sobre la tabla Reserva ──────────────────

        [TestMethod]
        public void DvhAlterado_SinMarcador_DiagnosticoComprometido()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dvDAL = new DAL.DigitoVerificador();
            if (CantReservas() == 0) Assert.Inconclusive("Sin reservas para el test de integridad.");
            try
            {
                dvDAL.ConsumirMarcadorReseal("Reserva");                 // asegurar que NO hay marcador
                new DAL.Reserva().RecalcularDV();                        // partir de ÍNTEGRO
                _acceso.Escribir("UPDATE TOP (1) Reserva SET DVH = ISNULL(DVH,0) + 99999", null);  // corromper

                var diag = Configuracion.ObtenerDiagnostico();
                Assert.IsTrue(diag.TablasAdicionalesCorruptas.Any(t => t.StartsWith("Reserva")),
                    "Un DVH alterado sin marcador debe diagnosticarse COMPROMETIDO (sin auto-reparar).");
            }
            finally { new DAL.Reserva().RecalcularDV(); dvDAL.ConsumirMarcadorReseal("Reserva"); }
        }

        [TestMethod]
        public void DvhNull_SinMarcador_DiagnosticoComprometido()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dvDAL = new DAL.DigitoVerificador();
            if (CantReservas() == 0) Assert.Inconclusive("Sin reservas para el test de integridad.");
            try
            {
                dvDAL.ConsumirMarcadorReseal("Reserva");
                _acceso.Escribir("UPDATE Reserva SET DVH = NULL", null);  // todos en NULL
                dvDAL.EliminarDVV("Reserva");                             // sin DVV

                var diag = Configuracion.ObtenerDiagnostico();
                Assert.IsTrue(diag.TablasAdicionalesCorruptas.Any(t => t.StartsWith("Reserva")),
                    "DVH en NULL sin marcador debe ser COMPROMETIDO (no inicialización encubierta).");
            }
            finally { new DAL.Reserva().RecalcularDV(); dvDAL.ConsumirMarcadorReseal("Reserva"); }
        }

        [TestMethod]
        public void ConMarcador_Verificar_ReselaYConsumeMarcador()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dvDAL = new DAL.DigitoVerificador();
            if (CantReservas() == 0) Assert.Inconclusive("Sin reservas para el test de integridad.");
            // Baseline ÍNTEGRO: re-sellar las tablas protegidas (los DELETE crudos de la limpieza de
            // otros tests de integración dejan su DV desactualizado; el app las mantiene al escribir).
            ResellarTablasNegocio();
            try
            {
                // Simular el estado que deja una migración: DVH en NULL, sin DVV, CON marcador.
                _acceso.Escribir("UPDATE Reserva SET DVH = NULL", null);
                dvDAL.EliminarDVV("Reserva");
                dvDAL.MarcarReseal("Reserva");
                Assert.IsTrue(dvDAL.TieneMarcadorReseal("Reserva"), "Precondición: marcador presente.");

                bool ok = Configuracion.VerificarIntegridadDV(out _);

                Assert.IsTrue(ok, "Con marcador de re-sellado la verificación debe quedar ÍNTEGRA (re-sella).");
                Assert.IsFalse(dvDAL.TieneMarcadorReseal("Reserva"), "El marcador debe consumirse tras re-sellar.");
                Assert.IsNotNull(dvDAL.ObtenerDVV("Reserva"), "Tras re-sellar, la tabla debe tener su DVV.");
                var diag = Configuracion.ObtenerDiagnostico();
                Assert.IsFalse(diag.TablasAdicionalesCorruptas.Any(t => t.StartsWith("Reserva")),
                    "Tras el re-sellado autorizado, Reserva queda ÍNTEGRA.");
            }
            finally { new DAL.Reserva().RecalcularDV(); dvDAL.ConsumirMarcadorReseal("Reserva"); }
        }

        private int CantReservas()
        {
            try { return Convert.ToInt32(_acceso.Leer("SELECT COUNT(*) FROM Reserva", null).Rows[0][0]); }
            catch { return 0; }
        }

        // Re-sella el DV de las tablas de negocio protegidas (baseline ÍNTEGRO para el test del marcador).
        private static void ResellarTablasNegocio()
        {
            var dv = new DAL.DigitoVerificador();
            dv.RecalcularTabla(DAL.Cliente.DV_Tabla,     DAL.Cliente.DV_Pk,     DAL.Cliente.DV_Columnas);
            dv.RecalcularTabla(DAL.Empleado.DV_Tabla,    DAL.Empleado.DV_Pk,    DAL.Empleado.DV_Columnas);
            dv.RecalcularTabla(DAL.Experiencia.DV_Tabla, DAL.Experiencia.DV_Pk, DAL.Experiencia.DV_Columnas);
            dv.RecalcularTabla(DAL.Organizador.DV_Tabla, DAL.Organizador.DV_Pk, DAL.Organizador.DV_Columnas);
            dv.RecalcularTabla(DAL.Reserva.DV_Tabla,     DAL.Reserva.DV_Pk,     DAL.Reserva.DV_Columnas);
        }
    }
}

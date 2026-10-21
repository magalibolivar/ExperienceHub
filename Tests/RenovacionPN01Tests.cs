using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN01 — Renovación cobrada. Integración contra ExperienceHubDB (SQL real):
    ///   • renovación pagada extiende la suscripción un mes desde su vencimiento;
    ///   • renovación de una suscripción vencida arranca desde hoy;
    ///   • 3 rechazos cancelan la renovación SIN tocar la suscripción;
    ///   • no puede haber dos contrataciones en proceso para el mismo cliente.
    /// Limpia sus propios datos. Si no hay conexión, se marca Inconclusive.
    /// </summary>
    [TestClass]
    public class RenovacionPN01Tests
    {
        private readonly DAL.Acceso _acceso = DAL.Acceso.GetInstance();

        private int _idCli, _idSus;
        private const string MODULO = "TEST-RenovPN01";

        [TestInitialize]
        public void Setup()
        {
            if (!_acceso.VerificarConexion()) return;
            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario { Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador });
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Orden FK-seguro; desvincula bitácora (no se borra la auditoría).
            Exec("UPDATE BitacoraNegocio SET IdCliente=NULL WHERE IdCliente=@c", _idCli);
            Exec("DELETE FROM Contratacion WHERE IdCliente=@c", _idCli);
            Exec("DELETE FROM Suscripcion  WHERE IdCliente=@c", _idCli);
            Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", _idCli);
            Exec("DELETE FROM Cliente WHERE IdCliente=@c", _idCli);
            // Re-sella el DV de Cliente tras el borrado (su DVV quedó desactualizado).
            try { new DAL.DigitoVerificador().RecalcularTabla(DAL.Cliente.DV_Tabla, DAL.Cliente.DV_Pk, DAL.Cliente.DV_Columnas); } catch { }
            Seguridad.SessionManager.Logout();
        }

        // Crea cliente + suscripción Activa con el vencimiento dado. Devuelve ids por campos.
        private int CrearClienteConSus(DateTime? vencimiento, BE.EstadoSuscripcion estado, string dni)
        {
            var dalCli = new DAL.Cliente(); var dalSus = new DAL.Suscripcion();
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idPlan   = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            _idCli = dalCli.Alta(new BE.Cliente
            {
                Nombre = "TEST", Apellido = "RenovPN01", DNI = dni,
                Email = dni + "@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now,
                FechaNacimiento = DateTime.Today.AddYears(-30)
            });
            _idSus = dalSus.Alta(new BE.Suscripcion
            {
                IdCliente = _idCli, IdPlan = idPlan, FechaInicio = DateTime.Today.AddMonths(-1),
                FechaVencimiento = vencimiento, Estado = estado, ReservasConsumidasMes = 3
            });
            return idPlan;
        }

        [TestMethod]
        public void RenovacionPagada_ExtiendeUnMesDesdeVencimiento()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalSus = new DAL.Suscripcion();
            DateTime venc = DateTime.Today.AddDays(10);         // vigente
            int idPlan = CrearClienteConSus(venc, BE.EstadoSuscripcion.Activa, "77000001");
            var bll = new BLL.Contratacion();

            int idCon = bll.CrearRenovacion(MODULO, _idCli, idPlan);
            bll.RegistrarCobro(MODULO, idCon, BE.MedioPago.Efectivo, 1, pagoConcretado: true);
            bll.Formalizar(MODULO, idCon);

            var s = dalSus.ObtenerPorId(_idSus);
            Assert.AreEqual(venc.AddMonths(1).Date, s.FechaVencimiento.Value.Date,
                "Vigente: el vencimiento debe extenderse un mes desde el vencimiento anterior.");
            Assert.AreEqual(BE.EstadoSuscripcion.Activa, s.Estado);
            Assert.AreEqual(0, s.ReservasConsumidasMes, "La renovación reinicia el consumo del período.");
        }

        [TestMethod]
        public void RenovacionDeVencida_ArrancaDesdeHoy()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalSus = new DAL.Suscripcion();
            DateTime venc = DateTime.Today.AddDays(-5);          // ya vencida
            int idPlan = CrearClienteConSus(venc, BE.EstadoSuscripcion.Vencida, "77000002");
            var bll = new BLL.Contratacion();

            int idCon = bll.CrearRenovacion(MODULO, _idCli, idPlan);
            bll.RegistrarCobro(MODULO, idCon, BE.MedioPago.Efectivo, 1, pagoConcretado: true);
            bll.Formalizar(MODULO, idCon);

            var s = dalSus.ObtenerPorId(_idSus);
            Assert.AreEqual(DateTime.Today.AddMonths(1).Date, s.FechaVencimiento.Value.Date,
                "Vencida: el vencimiento debe arrancar desde hoy + 1 mes.");
            Assert.AreEqual(BE.EstadoSuscripcion.Activa, s.Estado, "Al formalizar la renovación la suscripción queda Activa.");
        }

        [TestMethod]
        public void TresRechazos_CancelanRenovacion_SinTocarSuscripcion()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalSus = new DAL.Suscripcion(); var dalCon = new DAL.Contratacion();
            DateTime venc = DateTime.Today.AddDays(10);
            int idPlan = CrearClienteConSus(venc, BE.EstadoSuscripcion.Activa, "77000003");
            var bll = new BLL.Contratacion();

            int idCon = bll.CrearRenovacion(MODULO, _idCli, idPlan);
            for (int i = 0; i < 3; i++)
                bll.RegistrarCobro(MODULO, idCon, BE.MedioPago.Efectivo, 1, pagoConcretado: false);

            Assert.AreEqual(BE.EstadoContratacion.Cancelada, dalCon.ObtenerPorId(idCon).Estado,
                "A los 3 rechazos la renovación debe quedar Cancelada.");
            var s = dalSus.ObtenerPorId(_idSus);
            Assert.AreEqual(venc.Date, s.FechaVencimiento.Value.Date, "La suscripción NO se toca si la renovación no se pagó.");
            Assert.AreEqual(BE.EstadoSuscripcion.Activa, s.Estado);
        }

        [TestMethod]
        public void NoSePermiteSegundaContratacionEnProceso()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            int idPlan = CrearClienteConSus(DateTime.Today.AddDays(10), BE.EstadoSuscripcion.Activa, "77000004");
            var bll = new BLL.Contratacion();

            bll.CrearRenovacion(MODULO, _idCli, idPlan);   // primera en proceso
            try
            {
                bll.CrearRenovacion(MODULO, _idCli, idPlan);   // segunda → debe fallar
                Assert.Fail("No debería permitir una segunda contratación en proceso.");
            }
            catch (BE.AppException ex) { StringAssert.Contains(ex.Clave, "en_proceso"); }
        }

        private void Exec(string sql, int val)
        {
            if (val <= 0) return;
            try { _acceso.Escribir(sql, new[] { new SqlParameter("@c", val) }); } catch { }
        }
    }
}

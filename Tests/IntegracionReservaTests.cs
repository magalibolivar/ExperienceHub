using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Prueba de INTEGRACIÓN contra ExperienceHubDB (SQL Server real).
    /// Verifica el descuento/restauración ATÓMICA de cupo y el estado automático de la
    /// experiencia (regla 1 y 6) usando el DAL real. Limpia sus propios datos al terminar.
    /// Requiere la BD migrada (01 -> 02 -> 05). Si no hay conexión, el test se marca Inconclusive.
    /// </summary>
    [TestClass]
    public class IntegracionReservaTests
    {
        private readonly DAL.Acceso _acceso = DAL.Acceso.GetInstance();

        [TestMethod]
        public void CupoAtomico_CrearReservaDescuenta_CancelarRestaura()
        {
            if (!_acceso.VerificarConexion())
                Assert.Inconclusive("Sin conexión a ExperienceHubDB — se omite el test de integración.");

            var dalExp = new DAL.Experiencia();
            var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion();
            var dalRes = new DAL.Reserva();

            // Catálogos ya sembrados por el script 05
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat    = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg    = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan   = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idCli = 0, idRes = 0, idSus = 0;
            try
            {
                // 1) Experiencia futura con cupo 2
                idExp = dalExp.Alta(new BE.Experiencia
                {
                    Nombre = "TEST-Cata Integracion", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg,
                    Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90,
                    CupoMaximo = 2, CupoDisponible = 2, EdadMinima = 0, Premium = false,
                    Estado = BE.EstadoExperiencia.Programada
                });

                // 2) Cliente + suscripción activa vigente
                idCli = dalCli.Alta(new BE.Cliente
                {
                    Nombre = "TEST", Apellido = "Integracion", DNI = "99999999",
                    Email = "test@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now,
                    FechaNacimiento = DateTime.Today.AddYears(-30)
                });
                idSus = dalSus.Alta(new BE.Suscripcion
                {
                    IdCliente = idCli, IdPlan = idPlan, FechaInicio = DateTime.Today,
                    FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa
                });

                // 3) Crear reserva (1 lugar) → cupo debe bajar a 1
                idRes = dalRes.CrearConCupo(new BE.Reserva
                {
                    IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Pendiente,
                    FechaReserva = DateTime.Now, CantidadInvitados = 0
                }, lugares: 1);

                Assert.AreEqual(1, dalExp.ObtenerPorId(idExp).CupoDisponible, "El cupo debió bajar a 1 tras reservar.");

                // 4) Cancelar liberando el cupo → vuelve a 2
                dalRes.Cancelar(idRes, idExp, lugares: 1, motivo: "test", liberarCupo: true);
                Assert.AreEqual(2, dalExp.ObtenerPorId(idExp).CupoDisponible, "El cupo debió restaurarse a 2 al cancelar a tiempo.");
            }
            finally
            {
                // Limpieza (orden respeta FKs)
                Exec("DELETE FROM ReservaHistorial WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSus);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idCli);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idCli);
            }
        }

        [TestMethod]
        public void EstadoPorFecha_FinalizaVencidas()
        {
            if (!_acceso.VerificarConexion())
                Assert.Inconclusive("Sin conexión a ExperienceHubDB — se omite el test de integración.");

            var dalExp = new DAL.Experiencia();
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat    = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg    = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;

            int idExp = 0;
            try
            {
                // Experiencia cuyo horario ya terminó (ayer)
                idExp = dalExp.Alta(new BE.Experiencia
                {
                    Nombre = "TEST-Vencida", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg,
                    Fecha = DateTime.Today.AddDays(-1), HoraInicio = TimeSpan.FromHours(10), DuracionMinutos = 60,
                    CupoMaximo = 5, CupoDisponible = 5, Estado = BE.EstadoExperiencia.Programada
                });

                new BLL.Experiencia().ActualizarEstadosPorFecha();

                Assert.AreEqual(BE.EstadoExperiencia.Finalizada, dalExp.ObtenerPorId(idExp).Estado,
                    "La experiencia vencida debió pasar a Finalizada.");
            }
            finally
            {
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
            }
        }

        private void Exec(string sql, string p, int val)
        {
            if (val <= 0) return;
            try { _acceso.Escribir(sql, new[] { new SqlParameter(p, val) }); } catch { }
        }
    }
}

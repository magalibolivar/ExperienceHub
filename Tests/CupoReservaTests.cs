using System;
using System.Linq;
using System.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Reglas de cupo/stock de la reserva (PN02):
    ///   • cupo del PLAN excedido (lógica pura, BE.Suscripcion);
    ///   • cupo/STOCK de la experiencia: no se puede reservar sin cupo (integración);
    ///   • concurrencia sobre el último lugar: solo una reserva lo toma, nunca queda negativo.
    /// </summary>
    [TestClass]
    public class CupoReservaTests
    {
        private readonly DAL.Acceso _acceso = DAL.Acceso.GetInstance();

        // ── Cupo del PLAN (puro, sin BD) ──────────────────────────────────────
        [TestMethod]
        public void CupoPlan_SinSaldoMensual_NoPuedeReservar()
        {
            var s = new BE.Suscripcion
            {
                Estado = BE.EstadoSuscripcion.Activa,
                FechaVencimiento = DateTime.Today.AddMonths(1),
                ReservasDelPlan = 1,
                ReservasConsumidasMes = 1
            };
            Assert.IsFalse(s.PuedeReservar(), "Sin saldo mensual no debe poder reservar.");
            Assert.AreEqual(0, s.ReservasRestantes());
        }

        // ── Cupo/STOCK de la experiencia: pedir más de lo disponible falla (integración) ──
        [TestMethod]
        public void CupoExperiencia_SinCupoSuficiente_Falla()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");

            var dalExp = new DAL.Experiencia();
            var dalRes = new DAL.Reserva();
            int idCat = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idCiu = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idOrg = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idCli = new DAL.Cliente().ObtenerTodos().First().IdCliente;

            int idExp = 0, idRes = 0;
            try
            {
                idExp = dalExp.Alta(new BE.Experiencia
                {
                    Nombre = "TEST-SinCupo", IdCategoria = idCat, IdCiudad = idCiu, IdOrganizador = idOrg,
                    Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 60,
                    CupoMaximo = 1, CupoDisponible = 1, Estado = BE.EstadoExperiencia.Programada
                });

                try
                {
                    idRes = dalRes.CrearConCupo(new BE.Reserva
                    {
                        IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Pendiente,
                        FechaReserva = DateTime.Now, CantidadInvitados = 1
                    }, lugares: 2);   // pide 2 con cupo 1
                    Assert.Fail("Debió lanzar sin_cupo al pedir 2 lugares con cupo 1.");
                }
                catch (BE.AppException ex) { StringAssert.Contains(ex.Clave, "sin_cupo"); }

                Assert.AreEqual(1, dalExp.ObtenerPorId(idExp).CupoDisponible, "El cupo no debió tocarse (rollback).");
            }
            finally
            {
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
            }
        }

        // ── Último lugar: dos reservas lo disputan; solo una gana, nunca queda negativo ──
        [TestMethod]
        public void UltimoCupo_DosReservas_SoloUnaGana_SinNegativo()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");

            var dalExp = new DAL.Experiencia();
            var dalRes = new DAL.Reserva();
            int idCat = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idCiu = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idOrg = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idCli = new DAL.Cliente().ObtenerTodos().First().IdCliente;

            int idExp = 0, idRes1 = 0, idRes2 = 0;
            try
            {
                idExp = dalExp.Alta(new BE.Experiencia
                {
                    Nombre = "TEST-UltimoCupo", IdCategoria = idCat, IdCiudad = idCiu, IdOrganizador = idOrg,
                    Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 60,
                    CupoMaximo = 1, CupoDisponible = 1, Estado = BE.EstadoExperiencia.Programada
                });

                idRes1 = dalRes.CrearConCupo(new BE.Reserva
                {
                    IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Pendiente,
                    FechaReserva = DateTime.Now, CantidadInvitados = 0
                }, lugares: 1);   // toma el último lugar

                bool segundaFallo = false;
                try
                {
                    idRes2 = dalRes.CrearConCupo(new BE.Reserva
                    {
                        IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Pendiente,
                        FechaReserva = DateTime.Now, CantidadInvitados = 0
                    }, lugares: 1);
                }
                catch (BE.AppException) { segundaFallo = true; }

                Assert.IsTrue(segundaFallo, "La segunda reserva sobre el último lugar debe fallar.");
                Assert.AreEqual(0, dalExp.ObtenerPorId(idExp).CupoDisponible, "Cupo exactamente 0, nunca negativo.");
            }
            finally
            {
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes1);
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes2);
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

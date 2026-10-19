using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN03 — Cancelación de reserva y reasignación de cupo por lista de espera.
    /// Cubre las reglas pedidas: 2 (devolución del plan sin doble conteo), 4 (revalidación al
    /// confirmar oferta) y 6 (cancelación de experiencia en cascada por el Coordinador).
    /// Los tests puros no tocan BD; los de integración se omiten (Inconclusive) sin conexión.
    /// </summary>
    [TestClass]
    public class CancelacionPN03Tests
    {
        private readonly DAL.Acceso _acceso = DAL.Acceso.GetInstance();

        // ── Regla 2 — no devolver la reserva del plan dos veces ─────────────────

        /// <summary>
        /// Guard de negocio: una reserva ya Cancelada NO puede volver a cancelarse, de modo que
        /// la BLL nunca ejecuta la devolución del beneficio del plan una segunda vez.
        /// </summary>
        [TestMethod]
        public void Reserva_Cancelada_NoPuedeCancelarseDeNuevo()
        {
            var r = new BE.Reserva { Estado = BE.EstadoReserva.Cancelada };
            Assert.IsFalse(r.PuedeCancelarse(), "Una reserva ya cancelada no debe poder cancelarse otra vez.");

            var pend = new BE.Reserva { Estado = BE.EstadoReserva.Pendiente };
            var conf = new BE.Reserva { Estado = BE.EstadoReserva.Confirmada };
            Assert.IsTrue(pend.PuedeCancelarse(), "Una reserva Pendiente sí debe poder cancelarse.");
            Assert.IsTrue(conf.PuedeCancelarse(), "Una reserva Confirmada sí debe poder cancelarse.");
        }

        /// <summary>
        /// Integración: cancelar a tiempo devuelve el consumo del plan exactamente UNA vez; una
        /// segunda devolución (hipotética) no deja el consumo en negativo (piso CASE WHEN > 0),
        /// por lo que el beneficio nunca se "devuelve dos veces".
        /// </summary>
        [TestMethod]
        public void CancelarATiempo_DevuelveConsumoUnaSolaVez_SinDoble()
        {
            if (!_acceso.VerificarConexion())
                Assert.Inconclusive("Sin conexión a ExperienceHubDB — se omite el test de integración.");

            var dalExp = new DAL.Experiencia();
            var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion();
            var dalRes = new DAL.Reserva();

            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat    = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg    = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan   = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idCli = 0, idRes = 0, idSus = 0;
            try
            {
                idExp = dalExp.Alta(new BE.Experiencia
                {
                    Nombre = "TEST-PN03 DevolucionPlan", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg,
                    Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90,
                    CupoMaximo = 3, CupoDisponible = 3, EdadMinima = 0, Premium = false,
                    Estado = BE.EstadoExperiencia.Programada
                });
                idCli = dalCli.Alta(new BE.Cliente
                {
                    Nombre = "TEST", Apellido = "PN03Dev", DNI = "99999990",
                    Email = "pn03dev@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now,
                    FechaNacimiento = DateTime.Today.AddYears(-30)
                });
                idSus = dalSus.Alta(new BE.Suscripcion
                {
                    IdCliente = idCli, IdPlan = idPlan, FechaInicio = DateTime.Today,
                    FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa,
                    ReservasConsumidasMes = 0
                });

                // Reserva que consume el beneficio mensual (ReservasConsumidasMes 0 -> 1) de forma atómica.
                idRes = dalRes.CrearConCupo(new BE.Reserva
                {
                    IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Pendiente,
                    FechaReserva = DateTime.Now, CantidadInvitados = 0
                }, lugares: 1, idSuscripcionAConsumir: idSus);

                Assert.AreEqual(1, dalSus.ObtenerPorId(idSus).ReservasConsumidasMes,
                    "Al reservar, el consumo del plan debió subir a 1.");

                // Cancelación a tiempo: devuelve el beneficio (1 -> 0) en la misma transacción.
                dalRes.Cancelar(idRes, idExp, lugares: 1, motivo: "test", liberarCupo: true,
                    idSuscripcionADevolver: idSus, historial: null);
                Assert.AreEqual(0, dalSus.ObtenerPorId(idSus).ReservasConsumidasMes,
                    "Tras cancelar a tiempo, el consumo del plan debió volver a 0 (devuelto una vez).");

                // Una segunda devolución no debe dejar el consumo en negativo (piso = 0).
                dalRes.Cancelar(idRes, idExp, lugares: 1, motivo: "test-2", liberarCupo: true,
                    idSuscripcionADevolver: idSus, historial: null);
                Assert.AreEqual(0, dalSus.ObtenerPorId(idSus).ReservasConsumidasMes,
                    "El consumo nunca debe quedar negativo: el beneficio no se 'devuelve dos veces'.");
            }
            finally
            {
                Exec("DELETE FROM ReservaHistorial WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSus);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idCli);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idCli);
            }
        }

        // ── Regla 4 — al confirmar la oferta se revalida; si falla, pasa al siguiente ────

        /// <summary>
        /// Integración: al confirmar la oferta, si el cliente NO pasa la revalidación de PN02 (acá,
        /// suscripción vencida), su entrada queda Rechazada (Retirado) y el cupo —aún retenido— se
        /// ofrece automáticamente al siguiente de la fila, que pasa a Ofrecido.
        /// </summary>
        [TestMethod]
        public void ConfirmarOferta_ClienteNoRevalida_RechazaYOfreceAlSiguiente()
        {
            if (!_acceso.VerificarConexion())
                Assert.Inconclusive("Sin conexión a ExperienceHubDB — se omite el test de integración.");

            var dalExp   = new DAL.Experiencia();
            var dalCli   = new DAL.Cliente();
            var dalSus   = new DAL.Suscripcion();
            var dalEsp   = new DAL.ListaEspera();
            var bllEspera = new BLL.ListaEspera();

            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat    = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg    = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan   = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idA = 0, idB = 0, idSusA = 0, idSusB = 0;

            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario
            {
                Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador
            });
            try
            {
                // Experiencia COMPLETA (cupo 0) y futura (2 meses → fuera del margen de 24 h).
                idExp = dalExp.Alta(new BE.Experiencia
                {
                    Nombre = "TEST-PN03 Revalida", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg,
                    Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90,
                    CupoMaximo = 1, CupoDisponible = 0, EdadMinima = 0, Premium = false,
                    Estado = BE.EstadoExperiencia.Completa
                });

                // Cliente A — suscripción VENCIDA (no revalida).
                idA = dalCli.Alta(new BE.Cliente
                {
                    Nombre = "TEST", Apellido = "ClienteA", DNI = "99999901",
                    Email = "a@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now,
                    FechaNacimiento = DateTime.Today.AddYears(-30)
                });
                idSusA = dalSus.Alta(new BE.Suscripcion
                {
                    IdCliente = idA, IdPlan = idPlan, FechaInicio = DateTime.Today.AddMonths(-3),
                    FechaVencimiento = DateTime.Today.AddDays(-1), Estado = BE.EstadoSuscripcion.Vencida
                });

                // Cliente B — suscripción activa y vigente (el siguiente de la fila).
                idB = dalCli.Alta(new BE.Cliente
                {
                    Nombre = "TEST", Apellido = "ClienteB", DNI = "99999902",
                    Email = "b@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now,
                    FechaNacimiento = DateTime.Today.AddYears(-30)
                });
                idSusB = dalSus.Alta(new BE.Suscripcion
                {
                    IdCliente = idB, IdPlan = idPlan, FechaInicio = DateTime.Today,
                    FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa
                });

                // A y B entran a la lista de espera (A primero).
                bllEspera.Ingresar("TEST", idExp, idA);
                bllEspera.Ingresar("TEST", idExp, idB);

                // Se le ofrece el cupo a A (simula la liberación de un cupo).
                var entradaA = dalEsp.ObtenerPrimeroEnEspera(idExp);
                Assert.AreEqual(idA, entradaA.IdCliente, "El primero de la fila debía ser A.");
                dalEsp.Ofrecer(entradaA.IdListaEspera, DateTime.Now);
                var ofertaA = dalEsp.ObtenerOfertaVigente(idExp);

                // Confirmar la oferta de A: A no revalida → debe lanzar y dejar A Retirado + B Ofrecido.
                try
                {
                    bllEspera.ConfirmarOferta("TEST", ofertaA);
                    Assert.Fail("Confirmar con suscripción vencida debió lanzar AppException.");
                }
                catch (BE.AppException) { /* esperado: A no cumple las condiciones de PN02 */ }

                var todas = dalEsp.ObtenerPorExperiencia(idExp);
                var finA = todas.First(x => x.IdCliente == idA);
                var finB = todas.First(x => x.IdCliente == idB);
                Assert.AreEqual(BE.EstadoListaEspera.Retirado, finA.Estado,
                    "A no revalidó → su entrada debió quedar Retirado (Rechazada).");
                Assert.AreEqual(BE.EstadoListaEspera.Ofrecido, finB.Estado,
                    "El cupo debió ofrecerse automáticamente a B (siguiente de la fila).");
            }
            finally
            {
                Exec("DELETE FROM ListaEspera WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSusA);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSusB);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idA);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idB);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idA);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idB);
                Seguridad.SessionManager.Logout();
            }
        }

        // ── Regla 6 — el Coordinador cancela la experiencia (cascada) ───────────

        /// <summary>
        /// Integración (CU01-COO): al cancelar una experiencia se cancelan TODAS sus reservas activas
        /// con devolución del beneficio del plan, se cierra la lista de espera (Retirado) y la
        /// experiencia queda Cancelada. Todo en una transacción.
        /// </summary>
        [TestMethod]
        public void CancelarExperiencia_CancelaReservasConDevolucion_YCierraListaEspera()
        {
            if (!_acceso.VerificarConexion())
                Assert.Inconclusive("Sin conexión a ExperienceHubDB — se omite el test de integración.");

            var dalExp = new DAL.Experiencia();
            var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion();
            var dalRes = new DAL.Reserva();
            var dalEsp = new DAL.ListaEspera();
            var bllExp = new BLL.Experiencia();

            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat    = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg    = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan   = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idA = 0, idB = 0, idC = 0, idSusA = 0, idSusB = 0, idResA = 0, idResB = 0;

            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario
            {
                Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador
            });
            try
            {
                idExp = dalExp.Alta(new BE.Experiencia
                {
                    Nombre = "TEST-PN03 Cascada", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg,
                    Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90,
                    CupoMaximo = 2, CupoDisponible = 2, EdadMinima = 0, Premium = false,
                    Estado = BE.EstadoExperiencia.Programada
                });

                idA = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "CasA", DNI = "99999911", Email = "ca@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });
                idB = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "CasB", DNI = "99999912", Email = "cb@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });
                idC = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "CasC", DNI = "99999913", Email = "cc@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });

                idSusA = dalSus.Alta(new BE.Suscripcion { IdCliente = idA, IdPlan = idPlan, FechaInicio = DateTime.Today, FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa, ReservasConsumidasMes = 0 });
                idSusB = dalSus.Alta(new BE.Suscripcion { IdCliente = idB, IdPlan = idPlan, FechaInicio = DateTime.Today, FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa, ReservasConsumidasMes = 0 });

                // A y B reservan (consumen plan). El cupo baja a 0 → experiencia Completa.
                idResA = dalRes.CrearConCupo(new BE.Reserva { IdCliente = idA, IdExperiencia = idExp, Estado = BE.EstadoReserva.Confirmada, FechaReserva = DateTime.Now }, 1, idSuscripcionAConsumir: idSusA);
                idResB = dalRes.CrearConCupo(new BE.Reserva { IdCliente = idB, IdExperiencia = idExp, Estado = BE.EstadoReserva.Pendiente,  FechaReserva = DateTime.Now }, 1, idSuscripcionAConsumir: idSusB);

                // C queda en lista de espera (la experiencia ya está completa).
                dalEsp.Agregar(idExp, idC);

                Assert.AreEqual(1, dalSus.ObtenerPorId(idSusA).ReservasConsumidasMes, "A debía tener 1 consumo antes de cancelar.");

                // Cancelación de la experiencia en cascada.
                bllExp.CancelarExperiencia("TEST", dalExp.ObtenerPorId(idExp), "El organizador suspendió la actividad.");

                Assert.AreEqual(BE.EstadoExperiencia.Cancelada, dalExp.ObtenerPorId(idExp).Estado, "La experiencia debió quedar Cancelada.");
                Assert.AreEqual(BE.EstadoReserva.Cancelada, dalRes.ObtenerPorId(idResA).Estado, "La reserva de A debió quedar Cancelada.");
                Assert.AreEqual(BE.EstadoReserva.Cancelada, dalRes.ObtenerPorId(idResB).Estado, "La reserva de B debió quedar Cancelada.");
                Assert.AreEqual(0, dalSus.ObtenerPorId(idSusA).ReservasConsumidasMes, "A debió recuperar el beneficio del plan.");
                Assert.AreEqual(0, dalSus.ObtenerPorId(idSusB).ReservasConsumidasMes, "B debió recuperar el beneficio del plan.");

                var cola = dalEsp.ObtenerPorExperiencia(idExp);
                Assert.IsTrue(cola.All(x => x.Estado == BE.EstadoListaEspera.Retirado),
                    "La lista de espera debió cerrarse (todas Retirado).");

                // El historial registró la cancelación de ambas reservas.
                var hist = _acceso.Leer("SELECT COUNT(*) AS T FROM ReservaHistorial WHERE IdReserva IN (@a,@b) AND Accion='CANCELAR'",
                    new[] { new SqlParameter("@a", idResA), new SqlParameter("@b", idResB) });
                Assert.IsTrue(Convert.ToInt32(hist.Rows[0]["T"]) >= 2, "Debió grabarse historial CANCELAR de ambas reservas.");
            }
            finally
            {
                Exec("DELETE FROM ReservaHistorial WHERE IdReserva IN (SELECT IdReserva FROM Reserva WHERE IdExperiencia=@e)", "@e", idExp);
                Exec("DELETE FROM ListaEspera WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM Reserva WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSusA);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSusB);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idA);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idB);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idC);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idA);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idB);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idC);
                Seguridad.SessionManager.Logout();
            }
        }

        // ── Punto 1 — confirmación atómica de la oferta ─────────────────────────

        /// <summary>Guard puro: una entrada ya Confirmada no puede volver a confirmarse (evita la
        /// doble confirmación aunque llegaran dos pedidos).</summary>
        [TestMethod]
        public void EntradaConfirmada_NoPuedeConfirmarseDeNuevo()
        {
            Assert.IsFalse(new BE.ListaEspera { Estado = BE.EstadoListaEspera.Confirmado }.PuedeConfirmarse());
            Assert.IsTrue(new BE.ListaEspera { Estado = BE.EstadoListaEspera.Ofrecido }.PuedeConfirmarse());
        }

        /// <summary>
        /// Integración: crear la reserva desde una oferta deja la entrada en Confirmado dentro de la
        /// MISMA transacción (no queda en Ofrecido con la reserva ya creada), y la reserva guarda la
        /// suscripción con la que se hizo.
        /// </summary>
        [TestMethod]
        public void ConfirmarOferta_Atomica_EntradaConfirmadaConLaReserva()
        {
            if (!_acceso.VerificarConexion())
                Assert.Inconclusive("Sin conexión a ExperienceHubDB — se omite el test de integración.");

            var dalExp = new DAL.Experiencia(); var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva(); var dalEsp = new DAL.ListaEspera();
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idCli = 0, idSus = 0, idRes = 0, idLe = 0;
            try
            {
                idExp = dalExp.Alta(new BE.Experiencia { Nombre = "TEST-PN03 Atomica", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg, Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90, CupoMaximo = 1, CupoDisponible = 1, Estado = BE.EstadoExperiencia.Programada });
                idCli = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "Atom", DNI = "99999920", Email = "at@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });
                idSus = dalSus.Alta(new BE.Suscripcion { IdCliente = idCli, IdPlan = idPlan, FechaInicio = DateTime.Today, FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa });
                idLe = dalEsp.Agregar(idExp, idCli);                        // entrada en la cola
                var entrada = dalEsp.ObtenerPorExperiencia(idExp).First(x => x.IdCliente == idCli);
                dalEsp.Ofrecer(entrada.IdListaEspera, DateTime.Now);        // Ofrecido

                idRes = dalRes.CrearConCupo(new BE.Reserva { IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Pendiente, FechaReserva = DateTime.Now },
                    1, idSuscripcionAConsumir: idSus, historial: null, idListaEsperaAConfirmar: entrada.IdListaEspera);

                var entradaFinal = dalEsp.ObtenerPorExperiencia(idExp).First(x => x.IdCliente == idCli);
                Assert.AreEqual(BE.EstadoListaEspera.Confirmado, entradaFinal.Estado, "La entrada debió quedar Confirmado en la misma transacción.");
                var r = dalRes.ObtenerPorId(idRes);
                Assert.AreEqual(idSus, r.IdSuscripcion, "La reserva debió guardar la suscripción con la que se hizo.");
                Assert.AreEqual(0, dalExp.ObtenerPorId(idExp).CupoDisponible, "El cupo debió consumirse.");
            }
            finally
            {
                Exec("DELETE FROM ReservaHistorial WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM ListaEspera WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSus);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idCli);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idCli);
            }
        }

        // ── Punto 2 — devolución a la suscripción correcta (casos a, b, c) ───────

        // Crea experiencia futura + cliente + suscripción (consumida por 1 reserva). Devuelve ids.
        private void SetupReservaConSub(out int idExp, out int idCli, out int idSus, out int idRes,
                                        int consumoInicialDummy = 0)
        {
            var dalExp = new DAL.Experiencia(); var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva();
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            idExp = dalExp.Alta(new BE.Experiencia { Nombre = "TEST-PN03 Dev2", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg, Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90, CupoMaximo = 5, CupoDisponible = 5, Estado = BE.EstadoExperiencia.Programada });
            idCli = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "Dev2", DNI = "99999930", Email = "d2@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });
            idSus = dalSus.Alta(new BE.Suscripcion { IdCliente = idCli, IdPlan = idPlan, FechaInicio = DateTime.Today, FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa, ReservasConsumidasMes = 0 });
            idRes = dalRes.CrearConCupo(new BE.Reserva { IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Confirmada, FechaReserva = DateTime.Now }, 1, idSuscripcionAConsumir: idSus);
        }

        private void LimpiarDev2(int idExp, int idCli, int idRes, params int[] subs)
        {
            Exec("DELETE FROM ReservaHistorial WHERE IdReserva=@r", "@r", idRes);
            Exec("DELETE FROM Reserva WHERE IdExperiencia=@e", "@e", idExp);
            foreach (var s in subs) Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", s);
            Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
            Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idCli);
            Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idCli);
            Seguridad.SessionManager.Logout();
        }

        /// <summary>2a — el cliente renovó: la devolución NO debe tocar la suscripción nueva (vigente);
        /// como la original ya no está vigente, no se devuelve a ninguna.</summary>
        [TestMethod]
        public void DevolucionPlan_ClienteRenovo_NoTocaLaSuscripcionNueva()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva();
            int idExp, idCli, idSus1, idRes, idSus2 = 0;
            SetupReservaConSub(out idExp, out idCli, out idSus1, out idRes);
            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario { Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador });
            try
            {
                // Renovación: la original (sub1) pasa a Vencida; nace sub2 vigente con consumo 5.
                dalSus.CambiarEstado(idSus1, BE.EstadoSuscripcion.Vencida);
                _acceso.Escribir("UPDATE Suscripcion SET FechaVencimiento=@v WHERE IdSuscripcion=@s",
                    new[] { new SqlParameter("@v", DateTime.Today.AddDays(-1)), new SqlParameter("@s", idSus1) });
                idSus2 = dalSus.Alta(new BE.Suscripcion { IdCliente = idCli, IdPlan = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan, FechaInicio = DateTime.Today, FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa, ReservasConsumidasMes = 5 });

                new BLL.Reserva().Cancelar("TEST", dalRes.ObtenerPorId(idRes), "cancela a tiempo");

                Assert.AreEqual(5, dalSus.ObtenerPorId(idSus2).ReservasConsumidasMes, "No debe tocar la suscripción NUEVA (vigente).");
                Assert.AreEqual(1, dalSus.ObtenerPorId(idSus1).ReservasConsumidasMes, "La original no está vigente → no se devuelve.");
            }
            finally { LimpiarDev2(idExp, idCli, idRes, idSus1, idSus2); }
        }

        /// <summary>2b — sin suscripción vigente: no se devuelve a ninguna y la cancelación no aborta.</summary>
        [TestMethod]
        public void DevolucionPlan_SinSuscripcionVigente_NoDevuelveNiAborta()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva();
            int idExp, idCli, idSus, idRes;
            SetupReservaConSub(out idExp, out idCli, out idSus, out idRes);
            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario { Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador });
            try
            {
                dalSus.CambiarEstado(idSus, BE.EstadoSuscripcion.Vencida);
                _acceso.Escribir("UPDATE Suscripcion SET FechaVencimiento=@v WHERE IdSuscripcion=@s",
                    new[] { new SqlParameter("@v", DateTime.Today.AddDays(-1)), new SqlParameter("@s", idSus) });

                new BLL.Reserva().Cancelar("TEST", dalRes.ObtenerPorId(idRes), "cancela a tiempo");  // no debe lanzar

                Assert.AreEqual(1, dalSus.ObtenerPorId(idSus).ReservasConsumidasMes, "Sin vigente no se devuelve (queda en 1).");
                Assert.AreEqual(BE.EstadoReserva.Cancelada, dalRes.ObtenerPorId(idRes).Estado, "La reserva igual se canceló.");
            }
            finally { LimpiarDev2(idExp, idCli, idRes, idSus); }
        }

        /// <summary>2c — consumo ya en 0: la devolución no baja de 0 (piso) y no falla.</summary>
        [TestMethod]
        public void DevolucionPlan_ConsumoEnCero_NoBajaDeCero()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva();
            int idExp, idCli, idSus, idRes;
            SetupReservaConSub(out idExp, out idCli, out idSus, out idRes);
            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario { Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador });
            try
            {
                // Forzar consumo a 0 (la suscripción sigue vigente).
                _acceso.Escribir("UPDATE Suscripcion SET ReservasConsumidasMes=0 WHERE IdSuscripcion=@s",
                    new[] { new SqlParameter("@s", idSus) });

                new BLL.Reserva().Cancelar("TEST", dalRes.ObtenerPorId(idRes), "cancela a tiempo");

                Assert.AreEqual(0, dalSus.ObtenerPorId(idSus).ReservasConsumidasMes, "El consumo no debe quedar negativo (piso 0).");
            }
            finally { LimpiarDev2(idExp, idCli, idRes, idSus); }
        }

        /// <summary>2 (NULL) — reserva previa a PN03 (sin IdSuscripcion): NO se devuelve a ninguna
        /// suscripción (ni a la vigente del cliente) y la cancelación no aborta.</summary>
        [TestMethod]
        public void DevolucionPlan_SinSuscripcionRegistrada_NoDevuelveNiUsaLaVigente()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalExp = new DAL.Experiencia(); var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva();
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idCli = 0, idSus = 0, idRes = 0;
            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario { Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador });
            try
            {
                idExp = dalExp.Alta(new BE.Experiencia { Nombre = "TEST-PN03 NullSub", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg, Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90, CupoMaximo = 5, CupoDisponible = 5, Estado = BE.EstadoExperiencia.Programada });
                idCli = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "NullSub", DNI = "99999940", Email = "ns@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });
                // El cliente SÍ tiene una suscripción vigente con consumo 3... pero la reserva NO la registró.
                idSus = dalSus.Alta(new BE.Suscripcion { IdCliente = idCli, IdPlan = idPlan, FechaInicio = DateTime.Today, FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa, ReservasConsumidasMes = 3 });
                // Reserva creada SIN consumir suscripción → IdSuscripcion queda NULL (simula pre-PN03).
                idRes = dalRes.CrearConCupo(new BE.Reserva { IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Confirmada, FechaReserva = DateTime.Now }, 1);
                Assert.IsNull(dalRes.ObtenerPorId(idRes).IdSuscripcion, "Precondición: la reserva no tiene suscripción registrada.");

                new BLL.Reserva().Cancelar("TEST", dalRes.ObtenerPorId(idRes), "cancela a tiempo");  // no debe lanzar

                Assert.AreEqual(3, dalSus.ObtenerPorId(idSus).ReservasConsumidasMes,
                    "No debe devolverse a la suscripción vigente del cliente (ese era el error que se corrigió).");
                Assert.AreEqual(BE.EstadoReserva.Cancelada, dalRes.ObtenerPorId(idRes).Estado, "La reserva igual se canceló.");
            }
            finally { LimpiarDev2(idExp, idCli, idRes, idSus); }
        }

        // ── Punto 4 — PN02: la creación de la reserva graba IdSuscripcion ───────

        /// <summary>PN02: al crear una reserva que consume el plan, se persiste la suscripción con
        /// la que se hizo (IdSuscripcion), insumo de la devolución correcta al cancelar.</summary>
        [TestMethod]
        public void CrearReserva_GrabaIdSuscripcion()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalExp = new DAL.Experiencia(); var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva();
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idCli = 0, idSus = 0, idRes = 0;
            try
            {
                idExp = dalExp.Alta(new BE.Experiencia { Nombre = "TEST-PN03 GrabaSub", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg, Fecha = DateTime.Today.AddMonths(2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90, CupoMaximo = 5, CupoDisponible = 5, Estado = BE.EstadoExperiencia.Programada });
                idCli = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "GrabaSub", DNI = "99999950", Email = "gs@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });
                idSus = dalSus.Alta(new BE.Suscripcion { IdCliente = idCli, IdPlan = idPlan, FechaInicio = DateTime.Today, FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa });

                idRes = dalRes.CrearConCupo(new BE.Reserva { IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Pendiente, FechaReserva = DateTime.Now }, 1, idSuscripcionAConsumir: idSus);

                Assert.AreEqual(idSus, dalRes.ObtenerPorId(idRes).IdSuscripcion,
                    "La reserva debe guardar la suscripción con la que se hizo.");
            }
            finally
            {
                Exec("DELETE FROM ReservaHistorial WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSus);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idCli);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idCli);
            }
        }

        // ── Punto 3 — IdSuscripcion en el DVH: la tabla queda ÍNTEGRA ────────────

        /// <summary>Tras incorporar IdSuscripcion al DVH y re-sellar (como hace la app al arrancar),
        /// la tabla Reserva queda ÍNTEGRA (el diagnóstico no la marca corrupta).</summary>
        [TestMethod]
        public void Integridad_Reserva_ConIdSuscripcionEnDVH_QuedaIntegro()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            // Re-sella Reserva con el formato nuevo (IdSuscripcion incluido), igual que el arranque.
            new DAL.DigitoVerificador().RecalcularTabla(DAL.Reserva.DV_Tabla, DAL.Reserva.DV_Pk, DAL.Reserva.DV_Columnas);

            var diag = BLL.Configuracion.ObtenerDiagnostico();
            Assert.IsFalse(diag.TablasAdicionalesCorruptas.Contains("Reserva"),
                "Reserva debe quedar ÍNTEGRA con IdSuscripcion en el DVH tras el recálculo.");
        }

        // ── Punto 3 — migración idempotente post-restauración ───────────────────

        /// <summary>
        /// La migración que corre tras restaurar un backup viejo (DAL.Migracion.AsegurarEsquemaActual)
        /// es IDEMPOTENTE: se puede ejecutar dos veces sin duplicar ni fallar, y deja los objetos que
        /// el binario actual necesita (columna IdSuscripcion, Parametro, patentes y mapeos).
        /// </summary>
        [TestMethod]
        public void Migracion_AsegurarEsquemaActual_EsIdempotente()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var mig = new DAL.Migracion();
            try
            {
                mig.AsegurarEsquemaActual();
                mig.AsegurarEsquemaActual();   // segunda corrida: no debe duplicar ni lanzar

                int Col(string sql) => Convert.ToInt32(_acceso.Leer(sql, null).Rows[0][0]);
                Assert.IsTrue(Col("SELECT CASE WHEN COL_LENGTH('Reserva','IdSuscripcion') IS NULL THEN 0 ELSE 1 END") == 1,
                    "Debe existir la columna Reserva.IdSuscripcion.");
                Assert.AreEqual(2, Col("SELECT COUNT(*) FROM Parametro WHERE Clave IN ('OfertaVencimientoHoras','OfertaMargenInicioHoras')"));
                Assert.AreEqual(2, Col("SELECT COUNT(*) FROM Permiso WHERE NombreMenu IN ('mnuReservasCancelar','mnuExperienciasCancelar')"));
                Assert.AreEqual(2, Col("SELECT COUNT(*) FROM ControlMapeado WHERE NombreControl IN ('btnCancelarReserva','btnCancelarExperiencia')"),
                    "No debe duplicar los mapeos de los botones.");
            }
            finally
            {
                // La migración resetea el DVH de Reserva: se re-sella para dejar la base consistente.
                try { new DAL.Reserva().RecalcularDV(); } catch { }
            }
        }

        // ── Regla 1 — no se cancela una reserva de experiencia EnCurso/Finalizada ─

        [TestMethod]
        public void Cancelar_ExperienciaFinalizada_Bloquea()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalExp = new DAL.Experiencia(); var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva();
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idCli = 0, idSus = 0, idRes = 0;
            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario { Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador });
            try
            {
                idExp = dalExp.Alta(new BE.Experiencia { Nombre = "TEST-PN03 Finalizada", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg, Fecha = DateTime.Today.AddDays(-2), HoraInicio = TimeSpan.FromHours(19), DuracionMinutos = 90, CupoMaximo = 5, CupoDisponible = 5, Estado = BE.EstadoExperiencia.Finalizada });
                idCli = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "Fin", DNI = "99999960", Email = "fin@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });
                idSus = dalSus.Alta(new BE.Suscripcion { IdCliente = idCli, IdPlan = idPlan, FechaInicio = DateTime.Today.AddMonths(-1), FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa });
                idRes = dalRes.CrearConCupo(new BE.Reserva { IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Confirmada, FechaReserva = DateTime.Now }, 1, idSuscripcionAConsumir: idSus);

                try
                {
                    new BLL.Reserva().Cancelar("TEST", dalRes.ObtenerPorId(idRes), "intento tardío");
                    Assert.Fail("Cancelar una reserva de una experiencia Finalizada debió lanzar AppException.");
                }
                catch (BE.AppException ex)
                {
                    StringAssert.Contains(ex.Clave, "cancelar_experiencia_iniciada");
                }
                Assert.AreEqual(BE.EstadoReserva.Confirmada, dalRes.ObtenerPorId(idRes).Estado, "La reserva no debió cancelarse.");
            }
            finally
            {
                Exec("DELETE FROM ReservaHistorial WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSus);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idCli);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idCli);
                Seguridad.SessionManager.Logout();
            }
        }

        // ── Regla 2 — cancelación TARDÍA: libera cupo pero NO devuelve el plan ───

        [TestMethod]
        public void CancelarTardia_LiberaCupo_PeroNoDevuelvePlan()
        {
            if (!_acceso.VerificarConexion()) Assert.Inconclusive("Sin conexión a ExperienceHubDB.");
            var dalExp = new DAL.Experiencia(); var dalCli = new DAL.Cliente();
            var dalSus = new DAL.Suscripcion(); var dalRes = new DAL.Reserva();
            int idCiudad = new DAL.Ciudad().ObtenerActivas().First().IdCiudad;
            int idCat = new DAL.Categoria().ObtenerActivas().First().IdCategoria;
            int idOrg = new DAL.Organizador().ObtenerActivos().First().IdOrganizador;
            int idPlan = new DAL.PlanSuscripcion().ObtenerActivos().First().IdPlan;

            int idExp = 0, idCli = 0, idSus = 0, idRes = 0;
            Seguridad.SessionManager.Logout();
            Seguridad.SessionManager.Login(new BE.Usuario { Id = 1, Username = "admin-test", Perfil = BE.Roles.Administrador });
            try
            {
                // Experiencia futura pero a MENOS de 48 h (cancelación tardía). Programada (no EnCurso).
                var inicio = DateTime.Now.AddHours(6);
                idExp = dalExp.Alta(new BE.Experiencia { Nombre = "TEST-PN03 Tardia", IdCategoria = idCat, IdCiudad = idCiudad, IdOrganizador = idOrg, Fecha = inicio.Date, HoraInicio = inicio.TimeOfDay, DuracionMinutos = 90, CupoMaximo = 3, CupoDisponible = 3, Estado = BE.EstadoExperiencia.Programada });
                idCli = dalCli.Alta(new BE.Cliente { Nombre = "TEST", Apellido = "Tard", DNI = "99999961", Email = "tard@test.com", IdCiudad = idCiudad, FechaAlta = DateTime.Now, FechaNacimiento = DateTime.Today.AddYears(-30) });
                idSus = dalSus.Alta(new BE.Suscripcion { IdCliente = idCli, IdPlan = idPlan, FechaInicio = DateTime.Today.AddMonths(-1), FechaVencimiento = DateTime.Today.AddMonths(1), Estado = BE.EstadoSuscripcion.Activa, ReservasConsumidasMes = 0 });
                idRes = dalRes.CrearConCupo(new BE.Reserva { IdCliente = idCli, IdExperiencia = idExp, Estado = BE.EstadoReserva.Confirmada, FechaReserva = DateTime.Now }, 1, idSuscripcionAConsumir: idSus);

                Assert.AreEqual(1, dalSus.ObtenerPorId(idSus).ReservasConsumidasMes, "Precondición: consumo en 1.");
                Assert.AreEqual(2, dalExp.ObtenerPorId(idExp).CupoDisponible, "Precondición: cupo en 2.");

                new BLL.Reserva().Cancelar("TEST", dalRes.ObtenerPorId(idRes), "cancela tardía");

                Assert.AreEqual(BE.EstadoReserva.Cancelada, dalRes.ObtenerPorId(idRes).Estado, "La reserva debió cancelarse.");
                Assert.AreEqual(1, dalSus.ObtenerPorId(idSus).ReservasConsumidasMes,
                    "Tardía: NO se devuelve el beneficio del plan (sigue en 1).");
                Assert.AreEqual(3, dalExp.ObtenerPorId(idExp).CupoDisponible,
                    "Tardía: el cupo SÍ se libera (vuelve a 3).");
            }
            finally
            {
                Exec("DELETE FROM ReservaHistorial WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Reserva WHERE IdReserva=@r", "@r", idRes);
                Exec("DELETE FROM Suscripcion WHERE IdSuscripcion=@s", "@s", idSus);
                Exec("DELETE FROM Experiencia WHERE IdExperiencia=@e", "@e", idExp);
                Exec("DELETE FROM ClienteInteres WHERE IdCliente=@c", "@c", idCli);
                Exec("DELETE FROM Cliente WHERE IdCliente=@c", "@c", idCli);
                Seguridad.SessionManager.Logout();
            }
        }

        private void Exec(string sql, string p, int val)
        {
            if (val <= 0) return;
            try { _acceso.Escribir(sql, new[] { new SqlParameter(p, val) }); } catch { }
        }
    }
}

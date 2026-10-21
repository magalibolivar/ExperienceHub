using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN02 — Armado de la reserva. Prueba la lógica PURA de los pasos previos y reglas del
    /// proceso, sin BD ni sesión:
    ///   • CU03-AGR → BE.SituacionCliente.Resolver (situación del cliente)
    ///   • CU01-DEP → BE.ResultadoDisponibilidad.Evaluar (disponibilidad real de cupo)
    /// </summary>
    [TestClass]
    public class ReservaPN02Tests
    {
        // ── Helpers de armado ──────────────────────────────────────────────────
        private static BE.Cliente ClienteConSuscripcion(
            BE.EstadoSuscripcion estado, DateTime? vencimiento, int delPlan, int consumidas)
            => new BE.Cliente
            {
                IdCliente = 7, Nombre = "Ada", Apellido = "Lovelace",
                Suscripcion = new BE.Suscripcion
                {
                    Estado = estado, FechaVencimiento = vencimiento,
                    NombrePlan = "Plus", ReservasDelPlan = delPlan, ReservasConsumidasMes = consumidas
                }
            };

        private static BE.Reserva Res(BE.EstadoReserva estado) => new BE.Reserva { Estado = estado };

        // ── CU03-AGR · Situación del cliente ────────────────────────────────────
        [TestMethod]
        public void Situacion_ClienteNull_EsInexistente()
        {
            var s = BE.SituacionCliente.Resolver(null);
            Assert.AreEqual(BE.EstadoSituacionCliente.ClienteInexistente, s.Estado);
            Assert.IsFalse(s.PuedeArmarReserva);
        }

        [TestMethod]
        public void Situacion_SinSuscripcion_EsSinPlan()
        {
            var s = BE.SituacionCliente.Resolver(new BE.Cliente { IdCliente = 1, Nombre = "Sin", Apellido = "Plan" });
            Assert.AreEqual(BE.EstadoSituacionCliente.SinPlan, s.Estado);
            Assert.IsFalse(s.PuedeArmarReserva);
        }

        [TestMethod]
        public void Situacion_SuscripcionVencida_EsVencida()
        {
            var cli = ClienteConSuscripcion(BE.EstadoSuscripcion.Activa, DateTime.Today.AddDays(-1), 4, 0);
            var s = BE.SituacionCliente.Resolver(cli);
            Assert.AreEqual(BE.EstadoSituacionCliente.SuscripcionVencida, s.Estado);
            Assert.IsFalse(s.PuedeArmarReserva);
            Assert.AreEqual(DateTime.Today.AddDays(-1), s.FechaVencimiento);
        }

        [TestMethod]
        public void Situacion_Vigente_EsOk_ConReservasRestantes()
        {
            var cli = ClienteConSuscripcion(BE.EstadoSuscripcion.Activa, DateTime.Today.AddDays(10), 4, 1);
            var s = BE.SituacionCliente.Resolver(cli);
            Assert.AreEqual(BE.EstadoSituacionCliente.Ok, s.Estado);
            Assert.IsTrue(s.PuedeArmarReserva);
            Assert.AreEqual(3, s.ReservasRestantes, "4 del plan - 1 consumida = 3 disponibles.");
            Assert.AreEqual("Plus", s.NombrePlan);
        }

        // ── CU01-DEP · Verificar disponibilidad ──────────────────────────────────
        [TestMethod]
        public void Disponibilidad_ProgramadaConCupo_EsDisponible()
        {
            var r = BE.ResultadoDisponibilidad.Evaluar(5, BE.EstadoExperiencia.Programada, cupoDisponible: 3, lugares: 2);
            Assert.IsTrue(r.Disponible);
            Assert.AreEqual(5, r.IdExperiencia);
        }

        [TestMethod]
        public void Disponibilidad_CupoInsuficiente_NoDisponible()
        {
            var r = BE.ResultadoDisponibilidad.Evaluar(5, BE.EstadoExperiencia.Programada, cupoDisponible: 1, lugares: 2);
            Assert.IsFalse(r.Disponible);
        }

        [TestMethod]
        public void Disponibilidad_NoProgramada_NoDisponible()
        {
            var r = BE.ResultadoDisponibilidad.Evaluar(5, BE.EstadoExperiencia.Completa, cupoDisponible: 9, lugares: 1);
            Assert.IsFalse(r.Disponible);
        }

        [TestMethod]
        public void Disponibilidad_LugaresNoPositivos_NoDisponible()
        {
            var r = BE.ResultadoDisponibilidad.Evaluar(5, BE.EstadoExperiencia.Programada, cupoDisponible: 9, lugares: 0);
            Assert.IsFalse(r.Disponible);
        }

        // NOTA: la antigua regla "reserva Pendiente bloqueante" (BLL.Reserva.TieneReservaPendienteBloqueante)
        // se eliminó al unificar crear+confirmar en una operación atómica (la reserva nace Confirmada).
        // Sus tests se retiraron en consecuencia.

        // ── Flujo de confirmación (máquina de estados) — respalda el paso 3 "Confirmación" ───────
        // La UI pide confirmación deliberada y, solo si la reserva está Pendiente, ejecuta la
        // transición Pendiente → Confirmada (BLL.Reserva.Confirmar) y luego ofrece el PDF.
        [TestMethod]
        public void Confirmacion_SoloReservaPendiente_SePuedeConfirmar()
        {
            Assert.IsTrue(Res(BE.EstadoReserva.Pendiente).PuedeConfirmarse());
            Assert.IsFalse(Res(BE.EstadoReserva.Confirmada).PuedeConfirmarse(), "Una reserva ya confirmada no se re-confirma.");
            Assert.IsFalse(Res(BE.EstadoReserva.Cancelada).PuedeConfirmarse());
            Assert.IsTrue(Res(BE.EstadoReserva.Pendiente).TransicionValida(BE.EstadoReserva.Confirmada));
        }

        [TestMethod]
        public void Confirmacion_PdfSoloParaConfirmada()
        {
            // El PDF de confirmación (actividad 16 de PN02) solo corresponde a una reserva CONFIRMADA:
            // no a Pendientes, Canceladas ni a los estados de seguimiento.
            Assert.AreEqual(BE.EstadoReserva.Confirmada, Res(BE.EstadoReserva.Confirmada).Estado);
            Assert.IsFalse(Res(BE.EstadoReserva.Pendiente).Estado == BE.EstadoReserva.Confirmada);
            Assert.IsFalse(Res(BE.EstadoReserva.Cancelada).Estado == BE.EstadoReserva.Confirmada);
        }

        [TestMethod]
        public void Confirmacion_ReservaCanceladaEsEstadoFinal()
        {
            var cancelada = Res(BE.EstadoReserva.Cancelada);
            Assert.IsFalse(cancelada.PuedeConfirmarse());
            Assert.IsFalse(cancelada.PuedeCancelarse());
            Assert.IsFalse(cancelada.TransicionValida(BE.EstadoReserva.Confirmada));
        }

        // ── Evaluador de compatibilidad (PN02) — decisión pura, sin BD ───────────────
        private static BE.Experiencia ExpBase(int cupo = 10) => new BE.Experiencia
        {
            IdExperiencia = 1, Nombre = "Cata de vinos", Premium = false, EdadMinima = 0,
            IdCiudad = 1, NombreCiudad = "CABA", CupoDisponible = cupo, CupoMaximo = 10,
            Fecha = DateTime.Today.AddDays(10), HoraInicio = new TimeSpan(20, 0, 0), DuracionMinutos = 120
        };
        private static BE.Suscripcion SusPremium(bool premium) => new BE.Suscripcion { AccesoPremiumDelPlan = premium };
        private static BE.PlanSuscripcion PlanInv(int invitados) => new BE.PlanSuscripcion { CantidadInvitados = invitados };

        // Compatible y con cupo → Disponible (caso principal).
        private static BE.CompatibilidadReserva EvalOk(BE.Experiencia e, int invitados = 0) =>
            BE.CompatibilidadReserva.Evaluar(e, SusPremium(true), PlanInv(5), 1, "CABA", 30, invitados,
                yaAsistio: false, nombreSolapada: null, yaEnCola: false, hayMargenOferta: true,
                permitirFueraDeCiudad: false, bloquearSiYaRealizada: true);

        [TestMethod]
        public void Compat_CompatibleConCupo_EsDisponible()
        {
            var r = EvalOk(ExpBase(cupo: 10), invitados: 2);
            Assert.AreEqual(BE.EstadoCompatibilidad.Disponible, r.Estado);
            Assert.IsTrue(r.EsReservable);
            Assert.AreEqual(3, r.LugaresPedidos);   // 1 titular + 2 invitados
        }

        [TestMethod]
        public void Compat_Premium_SinAcceso_EsIncompatible()
        {
            var e = ExpBase(); e.Premium = true;
            var r = BE.CompatibilidadReserva.Evaluar(e, SusPremium(false), PlanInv(5), 1, "CABA", 30, 0,
                false, null, false, true, false, true);
            Assert.AreEqual(BE.EstadoCompatibilidad.Incompatible, r.Estado);
            Assert.AreEqual("err.bll.reserva.premium", r.MotivoClave);
        }

        [TestMethod]
        public void Compat_EdadInsuficiente_EsIncompatible()
        {
            var e = ExpBase(); e.EdadMinima = 18;
            var r = BE.CompatibilidadReserva.Evaluar(e, SusPremium(true), PlanInv(5), 1, "CABA", 16, 0,
                false, null, false, true, false, true);
            Assert.AreEqual("err.bll.reserva.edad_minima", r.MotivoClave);
        }

        [TestMethod]
        public void Compat_InvitadosFueraDelPlan_EsIncompatible()
        {
            var r = BE.CompatibilidadReserva.Evaluar(ExpBase(), SusPremium(true), PlanInv(1), 1, "CABA", 30, 3,
                false, null, false, true, false, true);
            Assert.AreEqual("err.bll.reserva.invitados", r.MotivoClave);
        }

        [TestMethod]
        public void Compat_OtraCiudad_EsIncompatible()
        {
            var e = ExpBase(); e.IdCiudad = 2; e.NombreCiudad = "Córdoba";
            var r = BE.CompatibilidadReserva.Evaluar(e, SusPremium(true), PlanInv(5), 1, "CABA", 30, 0,
                false, null, false, true, permitirFueraDeCiudad: false, bloquearSiYaRealizada: true);
            Assert.AreEqual("err.bll.reserva.fuera_ciudad", r.MotivoClave);
            Assert.AreEqual("Córdoba", r.MotivoArgs[0]);
            Assert.AreEqual("CABA",    r.MotivoArgs[1]);
        }

        [TestMethod]
        public void Compat_YaRealizada_EsIncompatible()
        {
            var r = BE.CompatibilidadReserva.Evaluar(ExpBase(), SusPremium(true), PlanInv(5), 1, "CABA", 30, 0,
                yaAsistio: true, nombreSolapada: null, yaEnCola: false, hayMargenOferta: true,
                permitirFueraDeCiudad: false, bloquearSiYaRealizada: true);
            Assert.AreEqual("err.bll.reserva.ya_realizada", r.MotivoClave);
        }

        [TestMethod]
        public void Compat_Solapamiento_EsIncompatible()
        {
            var r = BE.CompatibilidadReserva.Evaluar(ExpBase(), SusPremium(true), PlanInv(5), 1, "CABA", 30, 0,
                false, nombreSolapada: "Otra experiencia", yaEnCola: false, hayMargenOferta: true,
                permitirFueraDeCiudad: false, bloquearSiYaRealizada: true);
            Assert.AreEqual("err.bll.reserva.solapamiento", r.MotivoClave);
        }

        [TestMethod]
        public void Compat_Precedencia_PremiumAntesQueEdad()
        {
            var e = ExpBase(); e.Premium = true; e.EdadMinima = 18;   // fallan ambas
            var r = BE.CompatibilidadReserva.Evaluar(e, SusPremium(false), PlanInv(5), 1, "CABA", 16, 0,
                false, null, false, true, false, true);
            Assert.AreEqual("err.bll.reserva.premium", r.MotivoClave, "Premium se evalúa antes que edad.");
        }

        [TestMethod]
        public void Compat_CupoParcialInsuficiente_EsSinLugar()
        {
            var r = EvalOk(ExpBase(cupo: 1), invitados: 2);   // pide 3 lugares, hay 1
            Assert.AreEqual(BE.EstadoCompatibilidad.SinLugar, r.Estado);
            Assert.AreEqual(1, r.LugaresDisponibles);
            Assert.AreEqual(3, r.LugaresPedidos);
        }

        [TestMethod]
        public void Compat_Completa_Compatible_EsListaEspera()
        {
            var r = EvalOk(ExpBase(cupo: 0), invitados: 0);
            Assert.AreEqual(BE.EstadoCompatibilidad.ListaEspera, r.Estado);
            Assert.IsTrue(r.PuedeListaEspera);
        }

        [TestMethod]
        public void Compat_Completa_YaEnCola_NoReofrece()
        {
            var r = BE.CompatibilidadReserva.Evaluar(ExpBase(cupo: 0), SusPremium(true), PlanInv(5), 1, "CABA", 30, 0,
                false, null, yaEnCola: true, hayMargenOferta: true, permitirFueraDeCiudad: false, bloquearSiYaRealizada: true);
            Assert.AreEqual(BE.EstadoCompatibilidad.YaEnCola, r.Estado);
            Assert.IsFalse(r.PuedeListaEspera);
        }

        [TestMethod]
        public void Compat_Completa_SinMargen_NoOfreceEncolar()
        {
            var r = BE.CompatibilidadReserva.Evaluar(ExpBase(cupo: 0), SusPremium(true), PlanInv(5), 1, "CABA", 30, 0,
                false, null, yaEnCola: false, hayMargenOferta: false, permitirFueraDeCiudad: false, bloquearSiYaRealizada: true);
            Assert.AreEqual(BE.EstadoCompatibilidad.ListaEsperaSinOferta, r.Estado);
            Assert.IsFalse(r.PuedeListaEspera);
        }
    }
}

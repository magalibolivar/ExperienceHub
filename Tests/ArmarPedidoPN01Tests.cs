using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// PN01 — Armar Pedido (orientado a experiencias). Prueba la lógica PURA de los pasos
    /// previos y reglas del proceso, sin BD ni sesión:
    ///   • CU03-VEN → BE.SituacionCliente.Resolver (estado comercial del cliente)
    ///   • CU01-DEP → BE.ResultadoDisponibilidad.Evaluar (disponibilidad real de cupo)
    ///   • Regla "pedido activo" → BLL.Reserva.TienePedidoActivoBloqueante
    /// </summary>
    [TestClass]
    public class ArmarPedidoPN01Tests
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

        // ── CU03-VEN · Situación del cliente ────────────────────────────────────
        [TestMethod]
        public void Situacion_ClienteNull_EsInexistente()
        {
            var s = BE.SituacionCliente.Resolver(null);
            Assert.AreEqual(BE.EstadoSituacionCliente.ClienteInexistente, s.Estado);
            Assert.IsFalse(s.PuedeArmarPedido);
        }

        [TestMethod]
        public void Situacion_SinSuscripcion_EsSinPlan()
        {
            var s = BE.SituacionCliente.Resolver(new BE.Cliente { IdCliente = 1, Nombre = "Sin", Apellido = "Plan" });
            Assert.AreEqual(BE.EstadoSituacionCliente.SinPlan, s.Estado);
            Assert.IsFalse(s.PuedeArmarPedido);
        }

        [TestMethod]
        public void Situacion_SuscripcionVencida_EsVencida()
        {
            var cli = ClienteConSuscripcion(BE.EstadoSuscripcion.Activa, DateTime.Today.AddDays(-1), 4, 0);
            var s = BE.SituacionCliente.Resolver(cli);
            Assert.AreEqual(BE.EstadoSituacionCliente.SuscripcionVencida, s.Estado);
            Assert.IsFalse(s.PuedeArmarPedido);
            Assert.AreEqual(DateTime.Today.AddDays(-1), s.FechaVencimiento);
        }

        [TestMethod]
        public void Situacion_Vigente_EsOk_ConCupoRestante()
        {
            var cli = ClienteConSuscripcion(BE.EstadoSuscripcion.Activa, DateTime.Today.AddDays(10), 4, 1);
            var s = BE.SituacionCliente.Resolver(cli);
            Assert.AreEqual(BE.EstadoSituacionCliente.Ok, s.Estado);
            Assert.IsTrue(s.PuedeArmarPedido);
            Assert.AreEqual(3, s.CupoDisponible, "4 del plan - 1 consumida = 3 disponibles.");
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

        // ── Regla "pedido activo" ────────────────────────────────────────────────
        [TestMethod]
        public void PedidoActivo_ConPendiente_Bloquea()
        {
            var activas = new List<BE.Reserva> { Res(BE.EstadoReserva.Confirmada), Res(BE.EstadoReserva.Pendiente) };
            Assert.IsTrue(BLL.Reserva.TienePedidoActivoBloqueante(activas, bloquearSiPendiente: true));
        }

        [TestMethod]
        public void PedidoActivo_SoloConfirmadas_NoBloquea()
        {
            var activas = new List<BE.Reserva> { Res(BE.EstadoReserva.Confirmada), Res(BE.EstadoReserva.Confirmada) };
            Assert.IsFalse(BLL.Reserva.TienePedidoActivoBloqueante(activas, bloquearSiPendiente: true));
        }

        [TestMethod]
        public void PedidoActivo_FlagApagado_NoBloquea()
        {
            var activas = new List<BE.Reserva> { Res(BE.EstadoReserva.Pendiente) };
            Assert.IsFalse(BLL.Reserva.TienePedidoActivoBloqueante(activas, bloquearSiPendiente: false));
        }

        [TestMethod]
        public void PedidoActivo_SinReservas_NoBloquea()
        {
            Assert.IsFalse(BLL.Reserva.TienePedidoActivoBloqueante(null, bloquearSiPendiente: true));
            Assert.IsFalse(BLL.Reserva.TienePedidoActivoBloqueante(new List<BE.Reserva>(), bloquearSiPendiente: true));
        }
    }
}

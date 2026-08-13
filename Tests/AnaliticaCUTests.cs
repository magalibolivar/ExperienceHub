using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Pruebas de los NÚCLEOS PUROS de los CU analíticos (PdN 3): ranking de experiencias, detección
    /// de abandono, cancelaciones y comportamiento de suscripciones. Sin BD: se pasan datos armados.
    /// </summary>
    [TestClass]
    public class AnaliticaCUTests
    {
        private static readonly DateTime HOY = new DateTime(2026, 6, 1);

        // ── CU-ANA-03 — Popularidad y ocupación ───────────────────────────────
        [TestMethod]
        public void Experiencias_RankingOcupacionYSegmento()
        {
            var exps = new List<BE.Experiencia>
            {
                new BE.Experiencia { IdExperiencia = 1, Nombre = "Cata",  CupoMaximo = 10, NombreCategoria = "Gastro", NombreCiudad = "CABA", Fecha = HOY },
                new BE.Experiencia { IdExperiencia = 2, Nombre = "Tour",  CupoMaximo = 10, NombreCategoria = "Cultura", NombreCiudad = "CABA", Fecha = HOY },
            };
            var reservas = new List<BE.Reserva>();
            for (int i = 0; i < 8; i++) reservas.Add(new BE.Reserva { IdExperiencia = 1, Estado = BE.EstadoReserva.Confirmada });
            reservas.Add(new BE.Reserva { IdExperiencia = 1, Estado = BE.EstadoReserva.Cancelada });   // no cuenta como ocupada
            reservas.Add(new BE.Reserva { IdExperiencia = 2, Estado = BE.EstadoReserva.Confirmada });   // 1/10 = baja demanda

            var res = BLL.AnalisisExperiencias.Construir(exps, reservas, null, null);

            Assert.AreEqual(2, res.Count);
            Assert.AreEqual("Cata", res[0].Experiencia);          // más reservas primero
            Assert.AreEqual(8, res[0].Reservas);
            Assert.AreEqual(1, res[0].Canceladas);
            Assert.AreEqual(0.8, res[0].Ocupacion, 0.0001);
            Assert.AreEqual("Alta demanda", res[0].Segmento);
            Assert.AreEqual("Baja demanda", res[1].Segmento);      // Tour 0.1 < 0.25
        }

        [TestMethod]
        public void Experiencias_ListaEsperaFuerzaAltaDemanda()
        {
            var exps = new List<BE.Experiencia> { new BE.Experiencia { IdExperiencia = 1, Nombre = "X", CupoMaximo = 10, Fecha = HOY } };
            var reservas = new List<BE.Reserva> { new BE.Reserva { IdExperiencia = 1, Estado = BE.EstadoReserva.Confirmada } };
            var espera = new Dictionary<int, int> { { 1, 3 } };    // 1/10 pero con cola → alta demanda

            var res = BLL.AnalisisExperiencias.Construir(exps, reservas, espera, null);
            Assert.AreEqual("Alta demanda", res[0].Segmento);
            Assert.AreEqual(3, res[0].EnEspera);
        }

        // ── CU-ANA-04 — Riesgo de abandono ────────────────────────────────────
        [TestMethod]
        public void Abandono_ReglaExigeAmbasCondiciones()
        {
            var clientes = new List<BE.Cliente>
            {
                new BE.Cliente { IdCliente = 1, Nombre = "A", Apellido = "A" }, // nunca reservó + sin suscripción → Alto
                new BE.Cliente { IdCliente = 2, Nombre = "B", Apellido = "B" }, // inactivo pero suscripción sana → NO
                new BE.Cliente { IdCliente = 3, Nombre = "C", Apellido = "C" }, // activo reciente + vencida → NO
                new BE.Cliente { IdCliente = 4, Nombre = "D", Apellido = "D" }, // inactivo + por vencer → Bajo
            };
            var reservas = new List<BE.Reserva>
            {
                new BE.Reserva { IdCliente = 2, Estado = BE.EstadoReserva.Confirmada, FechaReserva = HOY.AddDays(-100) },
                new BE.Reserva { IdCliente = 3, Estado = BE.EstadoReserva.Confirmada, FechaReserva = HOY.AddDays(-5)   },
                new BE.Reserva { IdCliente = 4, Estado = BE.EstadoReserva.Confirmada, FechaReserva = HOY.AddDays(-70)  },
            };
            var subs = new Dictionary<int, BE.Suscripcion>
            {
                { 1, null },
                { 2, new BE.Suscripcion { Estado = BE.EstadoSuscripcion.Activa, FechaVencimiento = HOY.AddDays(400) } },
                { 3, new BE.Suscripcion { Estado = BE.EstadoSuscripcion.Activa, FechaVencimiento = HOY.AddDays(-1)  } },
                { 4, new BE.Suscripcion { Estado = BE.EstadoSuscripcion.Activa, FechaVencimiento = HOY.AddDays(10), NombrePlan = "Basic" } },
            };

            var res = BLL.AnalisisAbandono.Evaluar(clientes, reservas, subs, 60, 15, HOY);

            Assert.AreEqual(2, res.Count, "Solo A y D cumplen AMBAS condiciones.");
            Assert.AreEqual("Alto", res[0].Nivel);          // A ordenado primero
            Assert.AreEqual(1, res[0].IdCliente);
            Assert.AreEqual(-1, res[0].DiasSinReservar);    // nunca reservó
            Assert.AreEqual("Bajo", res[1].Nivel);          // D
            Assert.AreEqual(70, res[1].DiasSinReservar);
            Assert.IsTrue(res[1].Motivos.Count >= 2);
        }

        // ── CU-ANA-05 — Cancelaciones ─────────────────────────────────────────
        [TestMethod]
        public void Cancelaciones_TasasYMotivos()
        {
            var exps = new List<BE.Experiencia>
            {
                new BE.Experiencia { IdExperiencia = 1, NombreCategoria = "Gastro" },
                new BE.Experiencia { IdExperiencia = 2, NombreCategoria = "Cultura" },
            };
            var reservas = new List<BE.Reserva>
            {
                new BE.Reserva { IdExperiencia = 1, NombreExperiencia = "Cata", Estado = BE.EstadoReserva.Confirmada, FechaReserva = HOY },
                new BE.Reserva { IdExperiencia = 1, NombreExperiencia = "Cata", Estado = BE.EstadoReserva.Confirmada, FechaReserva = HOY },
                new BE.Reserva { IdExperiencia = 1, NombreExperiencia = "Cata", Estado = BE.EstadoReserva.Cancelada,  FechaReserva = HOY, MotivoCancelacion = "Cliente" },
                new BE.Reserva { IdExperiencia = 2, NombreExperiencia = "Tour", Estado = BE.EstadoReserva.Cancelada,  FechaReserva = HOY, MotivoCancelacion = "Organizador" },
                new BE.Reserva { IdExperiencia = 2, NombreExperiencia = "Tour", Estado = BE.EstadoReserva.Cancelada,  FechaReserva = HOY, MotivoCancelacion = "Organizador" },
            };

            var r = BLL.AnalisisCancelaciones.Construir(reservas, exps);

            Assert.AreEqual(5, r.TotalReservas);
            Assert.AreEqual(3, r.TotalCanceladas);
            Assert.AreEqual(0.6, r.TasaGlobal, 0.0001);
            Assert.AreEqual("Tour", r.PorExperiencia[0].Clave);          // tasa 1.0 primero
            Assert.AreEqual(1.0, r.PorExperiencia[0].Tasa, 0.0001);
            Assert.AreEqual("Organizador", r.PorMotivo[0].Clave);        // motivo más frecuente
            Assert.AreEqual(2, r.PorMotivo[0].Canceladas);
        }

        // ── CU-ANA-07 — Reporte comercial por agente ──────────────────────────
        [TestMethod]
        public void Comercial_AgrupaPorAgenteYCalculaTasas()
        {
            var reservas = new List<BE.Reserva>
            {
                new BE.Reserva { IdEmpleado = 1, NombreEmpleado = "Ana",  IdCliente = 10, Estado = BE.EstadoReserva.Confirmada, FechaReserva = HOY },
                new BE.Reserva { IdEmpleado = 1, NombreEmpleado = "Ana",  IdCliente = 11, Estado = BE.EstadoReserva.Asistio,    FechaReserva = HOY },
                new BE.Reserva { IdEmpleado = 1, NombreEmpleado = "Ana",  IdCliente = 10, Estado = BE.EstadoReserva.Cancelada,  FechaReserva = HOY },
                new BE.Reserva { IdEmpleado = 2, NombreEmpleado = "Beto", IdCliente = 12, Estado = BE.EstadoReserva.Confirmada, FechaReserva = HOY },
            };

            var res = BLL.AnalisisComercial.Construir(reservas);

            Assert.AreEqual(2, res.Count);
            Assert.AreEqual("Ana", res[0].Agente);              // más reservas primero
            Assert.AreEqual(3, res[0].Reservas);
            Assert.AreEqual(1, res[0].Canceladas);
            Assert.AreEqual(1, res[0].Asistidas);
            Assert.AreEqual(2, res[0].Efectivas);               // 3 - 1 cancelada
            Assert.AreEqual(2, res[0].ClientesAtendidos);       // clientes 10 y 11 distintos
            Assert.AreEqual(1.0 / 3.0, res[0].TasaCancelacion, 0.0001);
        }

        // ── CU-ANA-06 — Comportamiento de suscripciones ───────────────────────
        [TestMethod]
        public void Suscripciones_EstadosPorVencerYConsumo()
        {
            var subs = new List<BE.Suscripcion>
            {
                new BE.Suscripcion { NombrePlan = "Basic",   Estado = BE.EstadoSuscripcion.Activa,     FechaVencimiento = HOY.AddDays(5),   ReservasConsumidasMes = 2, ReservasDelPlan = 4 },
                new BE.Suscripcion { NombrePlan = "Basic",   Estado = BE.EstadoSuscripcion.Activa,     FechaVencimiento = HOY.AddDays(100), ReservasConsumidasMes = 4, ReservasDelPlan = 4 },
                new BE.Suscripcion { NombrePlan = "Premium", Estado = BE.EstadoSuscripcion.Suspendida, ReservasConsumidasMes = 0, ReservasDelPlan = 8 },
                new BE.Suscripcion { NombrePlan = "Premium", Estado = BE.EstadoSuscripcion.Vencida,    ReservasConsumidasMes = 1, ReservasDelPlan = 8 },
            };

            var r = BLL.AnalisisSuscripciones.Construir(subs, 15, HOY);

            Assert.AreEqual(4, r.Total);
            Assert.AreEqual(2, r.Activas);
            Assert.AreEqual(1, r.Suspendidas);
            Assert.AreEqual(1, r.Vencidas);
            Assert.AreEqual(1, r.PorVencer);
            Assert.AreEqual(0.40625, r.ConsumoPromedio, 0.0001);
            Assert.AreEqual(2, r.PorPlan.Count);
            Assert.IsTrue(r.PorPlan.All(p => p.Total == 2));
        }
    }
}

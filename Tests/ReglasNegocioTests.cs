using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Servicios.Multiidioma;

namespace Tests
{
    /// <summary>
    /// Pruebas de la lógica de dominio (BE) de las nuevas reglas de negocio de ExperienceHub.
    /// Son puras (sin acceso a BD) → deterministas.
    /// </summary>
    [TestClass]
    public class ReglasNegocioTests
    {
        private static BE.Experiencia Exp(DateTime fecha, int hora, int durMin, int cupo = 10, int edad = 0, bool premium = false)
            => new BE.Experiencia
            {
                Fecha = fecha.Date, HoraInicio = TimeSpan.FromHours(hora), DuracionMinutos = durMin,
                CupoMaximo = cupo, CupoDisponible = cupo, EdadMinima = edad, Premium = premium,
                Estado = BE.EstadoExperiencia.Programada
            };

        // ── Regla 2: solapamiento de horarios ─────────────────────────────────
        [TestMethod]
        public void Experiencia_SeSuperponeCon_Solapadas_True()
        {
            var a = Exp(new DateTime(2026, 9, 1), 18, 120); // 18:00–20:00
            var b = Exp(new DateTime(2026, 9, 1), 19, 60);  // 19:00–20:00
            Assert.IsTrue(a.SeSuperponeCon(b));
            Assert.IsTrue(b.SeSuperponeCon(a));
        }

        [TestMethod]
        public void Experiencia_SeSuperponeCon_Contiguas_False()
        {
            var a = Exp(new DateTime(2026, 9, 1), 18, 60); // 18:00–19:00
            var b = Exp(new DateTime(2026, 9, 1), 19, 60); // 19:00–20:00
            Assert.IsFalse(a.SeSuperponeCon(b));
        }

        // ── Regla 1: cupo + edad mínima ───────────────────────────────────────
        [TestMethod]
        public void Experiencia_TieneCupo_SegunDisponibilidad()
        {
            var e = Exp(new DateTime(2026, 9, 1), 18, 60, cupo: 2);
            Assert.IsTrue(e.TieneCupo(2));
            e.CupoDisponible = 1;
            Assert.IsFalse(e.TieneCupo(2));
            Assert.IsTrue(e.EstaCompleta() == false);
            e.CupoDisponible = 0;
            Assert.IsTrue(e.EstaCompleta());
        }

        [TestMethod]
        public void Experiencia_CumpleEdadMinima()
        {
            var e = Exp(new DateTime(2026, 9, 1), 18, 60, edad: 18);
            Assert.IsFalse(e.CumpleEdadMinima(17));
            Assert.IsTrue(e.CumpleEdadMinima(18));
        }

        // ── Regla 3 y anticipación: suscripción ───────────────────────────────
        [TestMethod]
        public void Suscripcion_ReservasRestantes_Y_PuedeReservar()
        {
            var s = new BE.Suscripcion
            {
                Estado = BE.EstadoSuscripcion.Activa,
                FechaVencimiento = DateTime.Today.AddMonths(1),
                ReservasDelPlan = 4, ReservasConsumidasMes = 3
            };
            Assert.AreEqual(1, s.ReservasRestantes());
            Assert.IsTrue(s.PuedeReservar());
            s.ReservasConsumidasMes = 4;
            Assert.AreEqual(0, s.ReservasRestantes());
            Assert.IsFalse(s.PuedeReservar());
        }

        [TestMethod]
        public void Suscripcion_Suspendida_NoPuedeReservar()
        {
            var s = new BE.Suscripcion { Estado = BE.EstadoSuscripcion.Suspendida, ReservasDelPlan = 4 };
            Assert.IsFalse(s.EstaVigente());
            Assert.IsFalse(s.PuedeReservar());
        }

        [TestMethod]
        public void Suscripcion_AnticipacionPermitida()
        {
            var s = new BE.Suscripcion { AnticipacionMaxDelPlan = 30 };
            Assert.IsTrue(s.AnticipacionPermitida(20));
            Assert.IsFalse(s.AnticipacionPermitida(45));
        }

        // ── Regla 10: calificar ───────────────────────────────────────────────
        [TestMethod]
        public void Reserva_PuedeCalificarse_SoloSiAsistio()
        {
            var r = new BE.Reserva { Estado = BE.EstadoReserva.Confirmada };
            Assert.IsFalse(r.PuedeCalificarse());
            r.Estado = BE.EstadoReserva.Asistio;
            Assert.IsTrue(r.PuedeCalificarse());
        }

        [TestMethod]
        public void Calificacion_PuntajeValido_Rango1a5()
        {
            Assert.IsFalse(new BE.Calificacion { Puntaje = 0 }.PuntajeValido());
            Assert.IsTrue(new BE.Calificacion { Puntaje = 5 }.PuntajeValido());
            Assert.IsFalse(new BE.Calificacion { Puntaje = 6 }.PuntajeValido());
        }

        // ── Máquina de estados de la reserva ──────────────────────────────────
        [TestMethod]
        public void Reserva_TransicionValida()
        {
            var r = new BE.Reserva { Estado = BE.EstadoReserva.Pendiente };
            Assert.IsTrue(r.TransicionValida(BE.EstadoReserva.Confirmada));
            Assert.IsTrue(r.TransicionValida(BE.EstadoReserva.Cancelada));
            Assert.IsFalse(r.TransicionValida(BE.EstadoReserva.Asistio));

            r.Estado = BE.EstadoReserva.Confirmada;
            Assert.IsTrue(r.TransicionValida(BE.EstadoReserva.Asistio));
            Assert.IsFalse(r.TransicionValida(BE.EstadoReserva.Pendiente));

            r.Estado = BE.EstadoReserva.Asistio; // estado final
            Assert.IsFalse(r.TransicionValida(BE.EstadoReserva.Confirmada));
        }

        // ── Lugares ocupados (titular + invitados) ────────────────────────────
        [TestMethod]
        public void Reserva_LugaresOcupados_TitularMasInvitados()
        {
            Assert.AreEqual(1, new BE.Reserva { CantidadInvitados = 0 }.LugaresOcupados);
            Assert.AreEqual(3, new BE.Reserva { CantidadInvitados = 2 }.LugaresOcupados);
        }

        // ── Edad del cliente ──────────────────────────────────────────────────
        [TestMethod]
        public void Cliente_Edad_CalculaAniosCumplidos()
        {
            var c = new BE.Cliente { FechaNacimiento = DateTime.Today.AddYears(-25).AddDays(-1) };
            Assert.AreEqual(25, c.Edad());
        }

        // ── i18n de los módulos nuevos (claves embebidas en traducciones.tsv) ──
        [TestMethod]
        public void I18n_ClavesNuevas_TraducidasEnLosCuatroIdiomas()
        {
            string Txt(string lang, string clave)
            {
                var idm = Traductor.ObtenerIdiomasHardcode().First(i => i.Id == lang);
                var dict = Traductor.ObtenerTraduccionesHardcode(idm);
                Assert.IsTrue(dict.ContainsKey(clave), $"Falta la clave '{clave}' en {lang}");
                return dict[clave].Texto;
            }

            // Menú, título de form, botón y label — en distintos idiomas.
            Assert.AreEqual("Catalogs",  Txt("EN", "mnu.catalogos"));
            Assert.AreEqual("Bookings",  Txt("EN", "eh.frm.reservas"));
            Assert.AreEqual("Save",      Txt("EN", "eh.btn.guardar"));
            Assert.AreEqual("Name",      Txt("EN", "eh.lbl.nombre"));
            Assert.AreEqual("Cidade",    Txt("PT", "eh.lbl.ciudad"));
            Assert.AreEqual("Организатор", Txt("RU", "eh.lbl.organizador"));
            // Y que existan en ES (idioma base).
            Assert.AreEqual("Recomendaciones", Txt("ES", "mnu.cat.recomendaciones"));
        }
    }
}

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Reglas de negocio dependientes de fecha extraídas como funciones PURAS (reciben la fecha como
    /// parámetro en vez de leer DateTime.Now/Today internamente) — "reloj inyectable" aplicado donde
    /// había beneficio concreto: mayoría de edad (duplicada e intestable en Cliente y Usuario),
    /// cancelación a tiempo de una reserva, y expiración del bloqueo de cuenta por intentos fallidos.
    /// </summary>
    [TestClass]
    public class ReglasFechaTests
    {
        [TestMethod]
        public void EsMayorDeEdad_Exactamente18Anios_EsMayor()
        {
            var hoy = new DateTime(2026, 6, 15);
            var nacimiento = new DateTime(2008, 6, 15);   // cumple 18 justo hoy
            Assert.IsTrue(BE.ReglasFecha.EsMayorDeEdad(nacimiento, hoy));
        }

        [TestMethod]
        public void EsMayorDeEdad_UnDiaAntesDeCumplir18_EsMenor()
        {
            var hoy = new DateTime(2026, 6, 15);
            var nacimiento = new DateTime(2008, 6, 16);   // cumple 18 mañana, no hoy
            Assert.IsFalse(BE.ReglasFecha.EsMayorDeEdad(nacimiento, hoy));
        }

        [TestMethod]
        public void EsMayorDeEdad_ClaramenteAdulto_EsMayor()
        {
            Assert.IsTrue(BE.ReglasFecha.EsMayorDeEdad(new DateTime(1990, 1, 1), new DateTime(2026, 1, 1)));
        }

        [TestMethod]
        public void EsMayorDeEdad_ClaramenteMenor_EsMenor()
        {
            Assert.IsFalse(BE.ReglasFecha.EsMayorDeEdad(new DateTime(2015, 1, 1), new DateTime(2026, 1, 1)));
        }

        [TestMethod]
        public void EsMayorDeEdad_RespetaEdadMinimaCustom()
        {
            // 20 años exactos, con edad mínima 21 → no alcanza.
            Assert.IsFalse(BE.ReglasFecha.EsMayorDeEdad(new DateTime(2006, 1, 1), new DateTime(2026, 1, 1), edadMinima: 21));
        }
    }

    /// <summary>PN02 — Cancelación a tiempo (BE.Experiencia.CancelacionATiempo), función pura.</summary>
    [TestClass]
    public class ExperienciaCancelacionTests
    {
        private static BE.Experiencia Exp(DateTime fecha, TimeSpan hora)
            => new BE.Experiencia { Fecha = fecha, HoraInicio = hora };

        [TestMethod]
        public void CancelacionATiempo_ConMasDe48Horas_EsATiempo()
        {
            var exp = Exp(new DateTime(2026, 6, 20), new TimeSpan(20, 0, 0));
            var ahora = new DateTime(2026, 6, 10);   // 10 días antes
            Assert.IsTrue(exp.CancelacionATiempo(ahora, 48));
        }

        [TestMethod]
        public void CancelacionATiempo_ExactamenteEnElLimite_EsATiempo()
        {
            var exp = Exp(new DateTime(2026, 6, 12), new TimeSpan(20, 0, 0));
            var ahora = exp.FechaHoraInicio.AddHours(-48);   // exactamente 48hs antes
            Assert.IsTrue(exp.CancelacionATiempo(ahora, 48));
        }

        [TestMethod]
        public void CancelacionATiempo_ConMenosDe48Horas_EsTardia()
        {
            var exp = Exp(new DateTime(2026, 6, 12), new TimeSpan(20, 0, 0));
            var ahora = exp.FechaHoraInicio.AddHours(-10);   // 10hs antes, menos del mínimo
            Assert.IsFalse(exp.CancelacionATiempo(ahora, 48));
        }

        [TestMethod]
        public void CancelacionATiempo_DespuesDeIniciada_EsTardia()
        {
            var exp = Exp(new DateTime(2026, 6, 12), new TimeSpan(20, 0, 0));
            var ahora = exp.FechaHoraInicio.AddHours(5);   // ya empezó
            Assert.IsFalse(exp.CancelacionATiempo(ahora, 48));
        }
    }

    /// <summary>Usuarios — expiración del bloqueo por intentos fallidos (BLL.Usuario.EvaluarBloqueo),
    /// función pura que recibe "ahora" como parámetro.</summary>
    [TestClass]
    public class UsuarioBloqueoTests
    {
        private static BE.Usuario Bloqueado(DateTime fechaBloqueo, int cantidadBloqueos)
            => new BE.Usuario { Bloqueado = true, FechaBloqueo = fechaBloqueo, CantidadBloqueos = cantidadBloqueos };

        [TestMethod]
        public void SinFechaBloqueo_NoAutoExpira_EsPermanente()
        {
            var u = new BE.Usuario { Bloqueado = true, FechaBloqueo = null, CantidadBloqueos = 1 };
            var (expirado, permanente, _) = BLL.Usuario.EvaluarBloqueo(u, DateTime.Now);
            Assert.IsFalse(expirado);
            Assert.IsTrue(permanente);
        }

        [TestMethod]
        public void AntesDeCumplirseLosMinutos_NoExpiro()
        {
            var bloqueo = new DateTime(2026, 1, 1, 10, 0, 0);
            var u = Bloqueado(bloqueo, cantidadBloqueos: 1);   // primer bloqueo, el más corto de la escala
            var (expirado, permanente, minutosRestantes) = BLL.Usuario.EvaluarBloqueo(u, bloqueo.AddSeconds(1));
            Assert.IsFalse(expirado);
            Assert.IsFalse(permanente);
            Assert.IsTrue(minutosRestantes > 0);
        }

        [TestMethod]
        public void CantidadBloqueosFueraDeEscala_EsPermanente()
        {
            var u = Bloqueado(new DateTime(2026, 1, 1), cantidadBloqueos: 999);
            var (expirado, permanente, _) = BLL.Usuario.EvaluarBloqueo(u, new DateTime(2030, 1, 1));
            Assert.IsFalse(expirado);
            Assert.IsTrue(permanente, "Una escala agotada (fuera de rango) debe tratarse como bloqueo permanente, no auto-expirar.");
        }
    }
}

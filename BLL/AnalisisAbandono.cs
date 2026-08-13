using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL
{
    /// <summary>
    /// CU-ANA-04 — Detección de clientes en riesgo de abandono.
    /// Regla EXPLÍCITA (sin IA): un cliente está en riesgo cuando NO tiene actividad reciente
    /// (sin reservas en los últimos N días, o nunca reservó) Y su suscripción está vencida,
    /// por vencer (≤ M días) o ausente. La evaluación vive en el NÚCLEO PURO <see cref="Evaluar"/>.
    /// </summary>
    public class AnalisisAbandono
    {
        private readonly Cliente     _bllCli = new Cliente();
        private readonly Reserva     _bllRes = new Reserva();
        private readonly Suscripcion _bllSus = new Suscripcion();

        public const int DIAS_SIN_RESERVA = 60;
        public const int DIAS_POR_VENCER  = 15;

        /// <summary>Detecta los clientes en riesgo leyendo los datos vigentes.</summary>
        public List<BE.ClienteEnRiesgo> Detectar(int diasSinReserva = DIAS_SIN_RESERVA, int diasPorVencer = DIAS_POR_VENCER)
        {
            var clientes = _bllCli.ObtenerTodos();
            var reservas = _bllRes.ObtenerTodos();

            var subs = new Dictionary<int, BE.Suscripcion>();
            foreach (var c in clientes)
            {
                BE.Suscripcion s = c.Suscripcion;
                if (s == null) { try { s = _bllSus.ObtenerVigentePorCliente(c.IdCliente); } catch { s = null; } }
                subs[c.IdCliente] = s;
            }
            return Evaluar(clientes, reservas, subs, diasSinReserva, diasPorVencer, DateTime.Today);
        }

        /// <summary>
        /// NÚCLEO PURO: evalúa el riesgo con datos ya cargados (sin acceso a datos ⇒ testeable).
        /// Devuelve solo los clientes en riesgo, ordenados por nivel (Alto→Bajo) y luego por inactividad.
        /// </summary>
        public static List<BE.ClienteEnRiesgo> Evaluar(
            IList<BE.Cliente> clientes, IList<BE.Reserva> reservas,
            IDictionary<int, BE.Suscripcion> suscripciones,
            int diasSinReserva, int diasPorVencer, DateTime hoy)
        {
            var salida = new List<BE.ClienteEnRiesgo>();
            if (clientes == null) return salida;
            reservas = reservas ?? new List<BE.Reserva>();

            // Última reserva NO cancelada por cliente (una sola pasada).
            var ultima = new Dictionary<int, DateTime>();
            foreach (var r in reservas)
            {
                if (r.Estado == BE.EstadoReserva.Cancelada) continue;
                if (!ultima.ContainsKey(r.IdCliente) || r.FechaReserva > ultima[r.IdCliente])
                    ultima[r.IdCliente] = r.FechaReserva;
            }

            foreach (var c in clientes)
            {
                DateTime? ult = ultima.ContainsKey(c.IdCliente) ? (DateTime?)ultima[c.IdCliente] : null;
                int diasSin = ult.HasValue ? (int)(hoy.Date - ult.Value.Date).TotalDays : int.MaxValue;

                BE.Suscripcion s = (suscripciones != null && suscripciones.ContainsKey(c.IdCliente))
                    ? suscripciones[c.IdCliente] : null;

                bool vencida, porVencer; int diasVence;
                string estadoTxt = ClasificarSuscripcion(s, diasPorVencer, hoy, out vencida, out porVencer, out diasVence);

                bool inactivo     = diasSin >= diasSinReserva;   // incluye "nunca reservó"
                bool suscEnRiesgo = s == null || vencida || porVencer;
                if (!(inactivo && suscEnRiesgo)) continue;        // la regla exige AMBAS condiciones

                var motivos = new List<string>();
                if (!ult.HasValue) motivos.Add("Nunca registró una reserva");
                else               motivos.Add("Sin reservas hace " + diasSin + " días");
                if (s == null)      motivos.Add("Sin suscripción activa");
                else if (vencida)   motivos.Add("Suscripción vencida");
                else if (porVencer) motivos.Add("Suscripción vence en " + diasVence + " días");

                bool inactividadFuerte = diasSin == int.MaxValue || diasSin >= diasSinReserva * 2;
                string nivel = (s == null || vencida)
                    ? (inactividadFuerte ? "Alto" : "Medio")
                    : "Bajo";

                salida.Add(new BE.ClienteEnRiesgo
                {
                    IdCliente       = c.IdCliente,
                    Cliente         = c.NombreCompleto,
                    Plan            = s != null ? s.NombrePlan : "—",
                    UltimaReserva   = ult,
                    DiasSinReservar = diasSin == int.MaxValue ? -1 : diasSin,
                    Suscripcion     = estadoTxt,
                    DiasParaVencer  = diasVence,
                    Nivel           = nivel,
                    Motivos         = motivos
                });
            }

            var rank = new Dictionary<string, int> { { "Alto", 0 }, { "Medio", 1 }, { "Bajo", 2 } };
            return salida
                .OrderBy(x => rank.ContainsKey(x.Nivel) ? rank[x.Nivel] : 9)
                .ThenByDescending(x => x.DiasSinReservar)
                .ToList();
        }

        // Clasifica el estado de la suscripción respecto a hoy (sin depender de DateTime interno ⇒ testeable).
        private static string ClasificarSuscripcion(BE.Suscripcion s, int diasPorVencer, DateTime hoy,
            out bool vencida, out bool porVencer, out int diasVence)
        {
            vencida = false; porVencer = false; diasVence = int.MaxValue;
            if (s == null) return "Sin suscripción";
            if (s.Estado == BE.EstadoSuscripcion.Vencida)    { vencida = true; return "Vencida"; }
            if (s.Estado == BE.EstadoSuscripcion.Suspendida) { vencida = true; return "Suspendida"; }
            if (s.FechaVencimiento.HasValue)
            {
                diasVence = (int)(s.FechaVencimiento.Value.Date - hoy.Date).TotalDays;
                if (diasVence < 0)             { vencida = true;   return "Vencida"; }
                if (diasVence <= diasPorVencer) { porVencer = true; return "Vence en " + diasVence + " d"; }
            }
            return "Activa";
        }
    }
}

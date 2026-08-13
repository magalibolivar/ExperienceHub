using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL
{
    /// <summary>
    /// CU-ANA-07 (PdN 8) — Reporte de actividad comercial por Agente de Reservas.
    /// Agrupa las reservas por el empleado que las registró y calcula reservas, efectivas,
    /// canceladas, asistidas, clientes atendidos y tasa de cancelación. La agregación vive en el
    /// NÚCLEO PURO <see cref="Construir"/> (testeable sin BD). Sin IA.
    /// Nota: las altas de suscripción no se atribuyen a un agente (Suscripcion no persiste el empleado),
    /// por eso el reporte se centra en la actividad de reservas.
    /// </summary>
    public class AnalisisComercial
    {
        private readonly Reserva _bllRes = new Reserva();

        /// <summary>Arma el reporte por agente leyendo los datos vigentes. Filtro opcional por fecha de reserva.</summary>
        public List<BE.ReporteComercialAgente> PorAgente(DateTime? desde = null, DateTime? hasta = null)
        {
            return Construir(_bllRes.ObtenerTodos(), desde, hasta);
        }

        /// <summary>NÚCLEO PURO: arma el reporte a partir de datos ya cargados (sin acceso a datos ⇒ testeable).</summary>
        public static List<BE.ReporteComercialAgente> Construir(
            IList<BE.Reserva> reservas, DateTime? desde = null, DateTime? hasta = null)
        {
            var salida = new List<BE.ReporteComercialAgente>();
            if (reservas == null) return salida;

            var enRango = reservas.Where(x =>
                (!desde.HasValue || x.FechaReserva.Date >= desde.Value.Date) &&
                (!hasta.HasValue || x.FechaReserva.Date <= hasta.Value.Date)).ToList();

            foreach (var g in enRango.GroupBy(x => x.IdEmpleado))
            {
                int total   = g.Count();
                int canc    = g.Count(x => x.Estado == BE.EstadoReserva.Cancelada);
                int asist   = g.Count(x => x.Estado == BE.EstadoReserva.Asistio);
                int efect   = total - canc;
                int clis    = g.Select(x => x.IdCliente).Distinct().Count();
                string nom  = g.Select(x => x.NombreEmpleado).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                if (string.IsNullOrWhiteSpace(nom))
                    nom = g.Key > 0 ? ("Agente " + g.Key) : "(sin agente)";

                salida.Add(new BE.ReporteComercialAgente
                {
                    IdEmpleado        = g.Key,
                    Agente            = nom,
                    Reservas          = total,
                    Efectivas         = efect,
                    Canceladas        = canc,
                    Asistidas         = asist,
                    ClientesAtendidos = clis,
                    TasaCancelacion   = total > 0 ? (double)canc / total : 0
                });
            }

            return salida.OrderByDescending(a => a.Reservas).ThenBy(a => a.Agente).ToList();
        }
    }
}

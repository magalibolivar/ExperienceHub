using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL
{
    /// <summary>
    /// CU-ANA-05 — Análisis de cancelaciones.
    /// Cruza las reservas (estado Cancelada + motivo) con sus experiencias/categorías y calcula la
    /// tasa de cancelación por experiencia, por categoría y el conteo por motivo. La agregación vive
    /// en el NÚCLEO PURO <see cref="Construir"/> (testeable sin BD). Sin IA.
    /// </summary>
    public class AnalisisCancelaciones
    {
        private readonly Reserva     _bllRes = new Reserva();
        private readonly Experiencia _bllExp = new Experiencia();

        /// <summary>Analiza las cancelaciones leyendo los datos vigentes. Filtro opcional por fecha de reserva.</summary>
        public BE.ResumenCancelaciones Analizar(DateTime? desde = null, DateTime? hasta = null)
        {
            return Construir(_bllRes.ObtenerTodos(), _bllExp.ObtenerTodos(), desde, hasta);
        }

        /// <summary>NÚCLEO PURO: arma el resumen a partir de datos ya cargados (sin acceso a datos ⇒ testeable).</summary>
        public static BE.ResumenCancelaciones Construir(
            IList<BE.Reserva> reservas, IList<BE.Experiencia> experiencias,
            DateTime? desde = null, DateTime? hasta = null)
        {
            var r = new BE.ResumenCancelaciones();
            reservas = reservas ?? new List<BE.Reserva>();

            var catPorExp = new Dictionary<int, string>();
            if (experiencias != null)
                foreach (var e in experiencias)
                    catPorExp[e.IdExperiencia] = string.IsNullOrWhiteSpace(e.NombreCategoria) ? "(sin categoría)" : e.NombreCategoria;

            var enRango = reservas.Where(x =>
                (!desde.HasValue || x.FechaReserva.Date >= desde.Value.Date) &&
                (!hasta.HasValue || x.FechaReserva.Date <= hasta.Value.Date)).ToList();

            r.TotalReservas   = enRango.Count;
            r.TotalCanceladas = enRango.Count(x => x.Estado == BE.EstadoReserva.Cancelada);
            r.TasaGlobal      = r.TotalReservas > 0 ? (double)r.TotalCanceladas / r.TotalReservas : 0;

            r.PorExperiencia = enRango
                .GroupBy(x => string.IsNullOrWhiteSpace(x.NombreExperiencia) ? ("Experiencia " + x.IdExperiencia) : x.NombreExperiencia)
                .Select(g => Fila(g.Key, g.Count(), g.Count(x => x.Estado == BE.EstadoReserva.Cancelada)))
                .Where(f => f.Canceladas > 0)
                .OrderByDescending(f => f.Tasa).ThenByDescending(f => f.Canceladas)
                .ToList();

            r.PorCategoria = enRango
                .GroupBy(x => catPorExp.ContainsKey(x.IdExperiencia) ? catPorExp[x.IdExperiencia] : "(sin categoría)")
                .Select(g => Fila(g.Key, g.Count(), g.Count(x => x.Estado == BE.EstadoReserva.Cancelada)))
                .Where(f => f.Canceladas > 0)
                .OrderByDescending(f => f.Tasa).ThenByDescending(f => f.Canceladas)
                .ToList();

            r.PorMotivo = enRango
                .Where(x => x.Estado == BE.EstadoReserva.Cancelada)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.MotivoCancelacion) ? "(sin motivo)" : x.MotivoCancelacion.Trim())
                .Select(g => new BE.FilaCancelacion { Clave = g.Key, Total = g.Count(), Canceladas = g.Count(), Tasa = 0 })
                .OrderByDescending(f => f.Canceladas)
                .ToList();

            return r;
        }

        private static BE.FilaCancelacion Fila(string clave, int total, int canceladas) =>
            new BE.FilaCancelacion { Clave = clave, Total = total, Canceladas = canceladas, Tasa = total > 0 ? (double)canceladas / total : 0 };
    }
}

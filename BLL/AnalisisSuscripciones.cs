using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL
{
    /// <summary>
    /// CU-ANA-06 — Análisis del comportamiento de suscripciones.
    /// Resume el estado de la base de suscripciones (activas / suspendidas / vencidas / por vencer)
    /// y el consumo promedio del saldo mensual, global y por plan. La agregación vive en el NÚCLEO
    /// PURO <see cref="Construir"/> (testeable sin BD). Sin IA.
    /// </summary>
    public class AnalisisSuscripciones
    {
        private readonly Suscripcion _bllSus = new Suscripcion();

        public const int DIAS_POR_VENCER = 15;

        /// <summary>Analiza las suscripciones leyendo los datos vigentes.</summary>
        public BE.ResumenSuscripciones Analizar(int diasPorVencer = DIAS_POR_VENCER)
        {
            return Construir(_bllSus.ObtenerTodas(), diasPorVencer, DateTime.Today);
        }

        /// <summary>NÚCLEO PURO: arma el resumen a partir de datos ya cargados (sin acceso a datos ⇒ testeable).</summary>
        public static BE.ResumenSuscripciones Construir(IList<BE.Suscripcion> subs, int diasPorVencer, DateTime hoy)
        {
            var r = new BE.ResumenSuscripciones();
            subs = subs ?? new List<BE.Suscripcion>();
            r.Total = subs.Count;

            foreach (var s in subs)
            {
                switch (Clasificar(s, hoy))
                {
                    case "Activa":     r.Activas++;     break;
                    case "Suspendida": r.Suspendidas++; break;
                    case "Vencida":    r.Vencidas++;    break;
                }
                if (EsPorVencer(s, diasPorVencer, hoy)) r.PorVencer++;
            }
            r.ConsumoPromedio = subs.Count > 0 ? subs.Average(Consumo) : 0;

            r.PorPlan = subs
                .GroupBy(s => string.IsNullOrWhiteSpace(s.NombrePlan) ? ("Plan " + s.IdPlan) : s.NombrePlan)
                .Select(g => new BE.FilaSuscripcionPlan
                {
                    Plan            = g.Key,
                    Total           = g.Count(),
                    Activas         = g.Count(s => Clasificar(s, hoy) == "Activa"),
                    Suspendidas     = g.Count(s => Clasificar(s, hoy) == "Suspendida"),
                    Vencidas        = g.Count(s => Clasificar(s, hoy) == "Vencida"),
                    PorVencer       = g.Count(s => EsPorVencer(s, diasPorVencer, hoy)),
                    ConsumoPromedio = g.Any() ? g.Average(Consumo) : 0
                })
                .OrderByDescending(f => f.Total)
                .ToList();

            return r;
        }

        private static double Consumo(BE.Suscripcion s) =>
            s.ReservasDelPlan > 0 ? Math.Min(1.0, (double)s.ReservasConsumidasMes / s.ReservasDelPlan) : 0;

        private static bool EsPorVencer(BE.Suscripcion s, int dias, DateTime hoy)
        {
            if (s.Estado != BE.EstadoSuscripcion.Activa || !s.FechaVencimiento.HasValue) return false;
            int d = (int)(s.FechaVencimiento.Value.Date - hoy.Date).TotalDays;
            return d >= 0 && d <= dias;
        }

        private static string Clasificar(BE.Suscripcion s, DateTime hoy)
        {
            if (s.Estado == BE.EstadoSuscripcion.Suspendida) return "Suspendida";
            if (s.Estado == BE.EstadoSuscripcion.Vencida)    return "Vencida";
            if (s.FechaVencimiento.HasValue && s.FechaVencimiento.Value.Date < hoy.Date) return "Vencida";
            return "Activa";
        }
    }
}

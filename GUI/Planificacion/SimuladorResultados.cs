using System;
using System.Collections.Generic;
using System.Linq;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 — Simulación de impacto por recomendación EJECUTADA, para mostrar un "preview" de
    /// resultados antes de que exista la evaluación real (<see cref="BLL.PlanAccionService.EvaluarResultados"/>,
    /// que requiere el plan Cerrado + el período de observación ya transcurrido). Es DECLARADAMENTE
    /// FICTICIO — nunca se mezcla con <see cref="BE.KpiSnapshot"/> real ni decide nada por sí mismo.
    ///
    /// DETERMINISTA: la semilla de cada <see cref="Random"/> sale del Id real de la recomendación (PK
    /// estable en BD), nunca de la hora ni de un contador global — por eso los mismos números salen
    /// siempre que se abre la pantalla para el mismo plan, en vez de cambiar en cada render.
    /// </summary>
    internal static class SimuladorResultados
    {
        public sealed class Impacto
        {
            public BE.RecomendacionPlan Rec;
            public bool TieneImpacto;     // false para RevisarPlan/NoReprogramar (tareas sin efecto en catálogo)
            public string Resumen;
            public decimal OcupAntes, OcupDespues;         // % ocupación de la experiencia
            public int ReservasAntes, ReservasDespues;     // reservas confirmadas
            public int EsperaAntes, EsperaDespues;         // clientes en lista de espera
            public int CancelAntes, CancelDespues;         // cancelaciones proyectadas
            public decimal IngresosAntes, IngresosDespues; // $ estimados

            public bool Mejora =>
                TieneImpacto &&
                new[] {
                    ReservasDespues >= ReservasAntes,
                    EsperaDespues <= EsperaAntes,
                    CancelDespues <= CancelAntes,
                    IngresosDespues >= IngresosAntes,
                    OcupDespues >= OcupAntes
                }.Count(ok => ok) >= 3;
        }

        public sealed class Resumen
        {
            public List<Impacto> Impactos;
            public int EjecutadasCount;
            public int PendientesCount;      // aprobadas sin ejecutar — no incluidas en la simulación
            public decimal OcupAntes, OcupDespues;
            public int ReservasAntes, ReservasDespues;
            public int EsperaAntes, EsperaDespues;
            public int CancelAntes, CancelDespues;
            public decimal IngresosAntes, IngresosDespues;
            public decimal Efectividad;      // % de recs con impacto medible que mejoraron
            public int ConImpacto;
            public int MejoraronCount;       // de las ConImpacto, cuántas califican como "Mejora" (ver Impacto.Mejora)
        }

        public static Impacto Simular(BE.RecomendacionPlan r)
        {
            // Semilla estable: mismo Id de recomendación → mismos números siempre.
            var rnd = new Random(unchecked(r.IdRecomendacion * 7919 + ((int)r.AccionSugerida + 1) * 131));
            int esperaBase = 3 + rnd.Next(0, 9);
            decimal ocupBase = 28 + rnd.Next(0, 45);
            int reservasBase = 8 + rnd.Next(0, 20);
            int cancelBase = 1 + rnd.Next(0, 5);
            decimal ingresosBase = 15000 + rnd.Next(0, 40000);

            var im = new Impacto
            {
                Rec = r,
                OcupAntes = ocupBase, ReservasAntes = reservasBase, EsperaAntes = esperaBase,
                CancelAntes = cancelBase, IngresosAntes = ingresosBase
            };

            switch (r.AccionSugerida)
            {
                case BE.AccionSugerida.ProgramarEdicion:
                    im.TieneImpacto = true;
                    im.ReservasDespues = reservasBase + 4 + rnd.Next(0, 6);
                    im.EsperaDespues = Math.Max(0, esperaBase - (2 + rnd.Next(0, 4)));
                    im.OcupDespues = Math.Min(100, ocupBase + 10 + rnd.Next(0, 15));
                    im.CancelDespues = cancelBase;
                    im.IngresosDespues = ingresosBase + 3000 + rnd.Next(0, 6000);
                    im.Resumen = I18n.T("eh.pln.sim.resumen.programar", "Nueva edición programada: reabsorbe parte de la demanda en lista de espera.");
                    break;
                case BE.AccionSugerida.AmpliarCupo:
                    im.TieneImpacto = true;
                    im.ReservasDespues = reservasBase + 2 + rnd.Next(0, 4);
                    im.EsperaDespues = Math.Max(0, esperaBase - (1 + rnd.Next(0, 3)));
                    im.OcupDespues = Math.Min(100, ocupBase + 4 + rnd.Next(0, 8));
                    im.CancelDespues = cancelBase;
                    im.IngresosDespues = ingresosBase + 1200 + rnd.Next(0, 3000);
                    im.Resumen = I18n.T("eh.pln.sim.resumen.ampliar", "Cupo ampliado: entra más demanda de la lista de espera sin abrir una edición nueva.");
                    break;
                case BE.AccionSugerida.CancelarExperiencia:
                    im.TieneImpacto = true;
                    im.ReservasDespues = 0;
                    im.EsperaDespues = 0;
                    im.OcupDespues = 0;
                    im.CancelDespues = cancelBase + 1;
                    im.IngresosDespues = Math.Max(0, ingresosBase - (800 + rnd.Next(0, 1500)));
                    im.Resumen = I18n.T("eh.pln.sim.resumen.cancelar", "Experiencia dada de baja: se evitan nuevas reservas y cancelaciones sobre un evento de baja demanda.");
                    break;
                case BE.AccionSugerida.SuspenderOrganizador:
                    im.TieneImpacto = true;
                    im.ReservasDespues = reservasBase;
                    im.EsperaDespues = esperaBase;
                    im.OcupDespues = ocupBase;
                    im.CancelDespues = Math.Max(0, cancelBase - (1 + rnd.Next(0, 3)));
                    im.IngresosDespues = ingresosBase + rnd.Next(0, 1500);
                    im.Resumen = I18n.T("eh.pln.sim.resumen.suspender", "Organizador suspendido: se proyectan menos cancelaciones en sus próximas experiencias.");
                    break;
                default: // RevisarPlan, NoReprogramar: tareas de decisión sin efecto directo en el catálogo
                    im.TieneImpacto = false;
                    im.ReservasDespues = reservasBase; im.EsperaDespues = esperaBase; im.OcupDespues = ocupBase;
                    im.CancelDespues = cancelBase; im.IngresosDespues = ingresosBase;
                    im.Resumen = I18n.T("eh.pln.sim.resumen.sinimpacto", "Tarea de revisión/decisión: sin impacto operativo directo medible.");
                    break;
            }
            return im;
        }

        public static Resumen Calcular(List<BE.RecomendacionPlan> recs)
        {
            var ejecutadas = recs.Where(r => r.Ejecutada).ToList();
            var pendientes = recs.Count(r => r.Decision == BE.DecisionRecomendacion.Aprobada && !r.Ejecutada);
            var impactos = ejecutadas.Select(Simular).ToList();
            var r2 = new Resumen { Impactos = impactos, EjecutadasCount = ejecutadas.Count, PendientesCount = pendientes };
            if (impactos.Count == 0) return r2;

            r2.OcupAntes = Math.Round(impactos.Average(i => i.OcupAntes), 0);
            r2.OcupDespues = Math.Round(impactos.Average(i => i.OcupDespues), 0);
            r2.ReservasAntes = impactos.Sum(i => i.ReservasAntes);
            r2.ReservasDespues = impactos.Sum(i => i.ReservasDespues);
            r2.EsperaAntes = impactos.Sum(i => i.EsperaAntes);
            r2.EsperaDespues = impactos.Sum(i => i.EsperaDespues);
            r2.CancelAntes = impactos.Sum(i => i.CancelAntes);
            r2.CancelDespues = impactos.Sum(i => i.CancelDespues);
            r2.IngresosAntes = impactos.Sum(i => i.IngresosAntes);
            r2.IngresosDespues = impactos.Sum(i => i.IngresosDespues);
            r2.ConImpacto = impactos.Count(i => i.TieneImpacto);
            r2.MejoraronCount = impactos.Count(i => i.Mejora);
            r2.Efectividad = r2.ConImpacto > 0 ? Math.Round(100m * r2.MejoraronCount / r2.ConImpacto, 0) : 0;
            return r2;
        }
    }
}

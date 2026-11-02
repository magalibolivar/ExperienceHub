using System.Collections.Generic;
using System.Linq;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 — Modelo de las TRES ETAPAS de la experiencia de usuario (Analizar y decidir → Ejecutar el
    /// plan → Evaluar resultados). Es SOLO una capa de presentación/navegación: NO es un estado
    /// persistido ni toca la máquina de estados de BLL. La etapa y el aspecto de cada paso se DERIVAN
    /// del estado real del plan (<see cref="BE.EstadoPlanAccion"/>) y de sus recomendaciones.
    ///
    /// Mapa etapa ↔ estado del plan:
    ///   • Sin plan                       → Analizar (actual); Ejecutar/Evaluar bloqueadas.
    ///   • Borrador/Aprobado/EnEjecucion  → Ejecutar (actual); Analizar completada; Evaluar futura/bloqueada.
    ///   • Cerrado                        → Evaluar (actual); Analizar y Ejecutar completadas.
    /// </summary>
    internal static class ProcesoPlanificacion
    {
        public const int Analizar = 0;
        public const int Ejecutar = 1;
        public const int Evaluar  = 2;

        // Etiquetas CORTAS y neutras (sustantivos, no verbos): este stepper cubre TODO el recorrido
        // dentro de PlanesAccionForm — desde generar/elegir recomendaciones (flujo "Nuevo plan", sin
        // plan todavía) hasta decidir/ejecutar/evaluar sobre un plan ya creado. Un verbo como "Analizar
        // y decidir" encaja en una sola de esas dos situaciones; el sustantivo describe el CONTENIDO de
        // la etapa y vale para ambas sin repetirse.
        public static string[] Etiquetas() => new[]
        {
            I18n.T("eh.pln.etapa.analizar", "① Recomendaciones"),
            I18n.T("eh.pln.etapa.ejecutar", "② Tareas"),
            I18n.T("eh.pln.etapa.evaluar",  "③ Resultados"),
        };

        /// <summary>Etapa sugerida al abrir el recorrido para un plan (o la etapa 1 si no hay plan).
        /// Borrador sugiere Analizar (recién creado: sus recomendaciones están TODAS en Pendiente
        /// — BLL.PlanAccionService.CrearPlan las agrega así, sin decidir ninguna — así que "Tareas"
        /// todavía no tiene nada que mostrar; antes esto mandaba a Ejecutar y la pantalla aparecía
        /// vacía apenas se creaba un plan, por más que después sí se aprobaran recomendaciones).</summary>
        public static int EtapaSugerida(BE.PlanAccion plan)
        {
            if (plan == null) return Analizar;
            switch (plan.Estado)
            {
                case BE.EstadoPlanAccion.Borrador: return Analizar;
                case BE.EstadoPlanAccion.Cerrado:  return Evaluar;
                default:                           return Ejecutar;   // Aprobado, EnEjecucion
            }
        }

        /// <summary>Estado visual de cada etapa, derivado del estado del plan Y del trabajo REAL hecho
        /// en cada una (no solo del estado administrativo). <paramref name="hayHistoricos"/> indica si
        /// existen planes CERRADOS consultables: en ese caso "Evaluar resultados" queda navegable
        /// (futuro) aunque el plan actual todavía no sea evaluable, para poder consultar resultados
        /// anteriores.</summary>
        public static PasoBar.EstadoPaso[] Estados(BE.PlanAccion plan, IEnumerable<BE.RecomendacionPlan> recs,
                                                    bool tieneEvaluacion, bool hayHistoricos = false)
        {
            // "Evaluar" nunca se bloquea si hay resultados históricos que consultar.
            var evaluarInactivo = hayHistoricos ? PasoBar.EstadoPaso.Futuro : PasoBar.EstadoPaso.Bloqueado;
            var e = new PasoBar.EstadoPaso[3];
            if (plan == null)
            {
                e[Analizar] = PasoBar.EstadoPaso.Actual;
                e[Ejecutar] = PasoBar.EstadoPaso.Bloqueado;
                e[Evaluar]  = evaluarInactivo;
                return e;
            }
            // Con un plan ya creado, Analizar está siempre "hecho" (se decidió crearlo) y es navegable.
            e[Analizar] = PasoBar.EstadoPaso.Completado;
            switch (plan.Estado)
            {
                case BE.EstadoPlanAccion.Borrador:
                case BE.EstadoPlanAccion.Aprobado:
                    e[Ejecutar] = PasoBar.EstadoPaso.Actual;
                    e[Evaluar]  = evaluarInactivo;                 // no para ESTE plan; sí si hay históricos
                    break;
                case BE.EstadoPlanAccion.EnEjecucion:
                    e[Ejecutar] = PasoBar.EstadoPaso.Actual;
                    e[Evaluar]  = PasoBar.EstadoPaso.Futuro;       // accesible: muestra "Antes" + aviso de pendiente
                    break;
                case BE.EstadoPlanAccion.Cerrado:
                    // "Completado" (✓ verde) SOLO si no queda nada pendiente en esa etapa. ANTES se
                    // marcaban las TRES como completadas por el solo hecho de que Estado == Cerrado,
                    // aunque hubiera tareas aprobadas sin ejecutar o nunca se hubiera evaluado el
                    // plan: el usuario veía 3 tildes verdes y, al abrir Resultados, una pantalla vacía
                    // o un aviso de "todavía no se ejecutó nada" — contradicción directa. Cerrar el
                    // plan NO exige ejecutar todo (regla real: BLL.PlanAccionService.CerrarPlan solo
                    // valida PuedeCerrar()), así que el check tiene que reflejar el trabajo HECHO, no
                    // el estado administrativo del plan.
                    bool hayTareasSinEjecutar = (recs ?? Enumerable.Empty<BE.RecomendacionPlan>()).Any(r => r.PuedeEjecutar());
                    e[Ejecutar] = hayTareasSinEjecutar ? PasoBar.EstadoPaso.Actual : PasoBar.EstadoPaso.Completado;
                    e[Evaluar]  = tieneEvaluacion
                                      ? PasoBar.EstadoPaso.Completado
                                      : (plan.PeriodoObservacionCumplido(System.DateTime.Now)
                                            ? PasoBar.EstadoPaso.Actual     // listo para evaluar, todavía no se hizo
                                            : PasoBar.EstadoPaso.Futuro);  // período de observación en curso
                    break;
            }
            return e;
        }

        /// <summary>Badge de "operaciones pendientes" por etapa: recomendaciones sin decidir (Analizar)
        /// y tareas aprobadas sin ejecutar (Ejecutar). Se muestra SIEMPRE que exista el pendiente, sin
        /// importar el estado del plan (antes se apagaba apenas el plan quedaba Cerrado, justo cuando
        /// más falta hacía para no perder de vista que algo quedó sin resolver).</summary>
        public static bool[] Pendientes(BE.PlanAccion plan, IEnumerable<BE.RecomendacionPlan> recs)
        {
            var p = new bool[3];
            if (plan == null || recs == null) return p;
            var lista = recs as ICollection<BE.RecomendacionPlan> ?? recs.ToList();
            p[Analizar] = lista.Any(r => r.PuedeDecidir());
            p[Ejecutar] = lista.Any(r => r.PuedeEjecutar());
            return p;
        }
    }
}

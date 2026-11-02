using System.Drawing;
using System.Windows.Forms;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 — Etiquetas i18n de los enums del dominio (tipo/acción/decisión/estado), colores semánticos
    /// y la "pill" (badge) reutilizable. Centraliza lo que antes estaba duplicado para que todos los
    /// formularios de planificación hablen el mismo idioma visual. Sin lógica de negocio.
    /// </summary>
    internal static class PlanificacionTextos
    {
        // ── Pill / badge ──────────────────────────────────────────────────────────
        public static Label Pill(string text, Color color)
            => new Label
            {
                Text = text, AutoSize = true, Font = Estilo.Bold(7.5f), ForeColor = Color.White,
                BackColor = color, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };

        // ── Estado del plan ────────────────────────────────────────────────────────
        public static string EstadoPlan(BE.EstadoPlanAccion e)
        {
            switch (e)
            {
                case BE.EstadoPlanAccion.Borrador:    return I18n.T("eh.pln.est.borrador", "Borrador");
                case BE.EstadoPlanAccion.Aprobado:    return I18n.T("eh.pln.est.aprobado", "Aprobado");
                case BE.EstadoPlanAccion.EnEjecucion: return I18n.T("eh.pln.est.enejecucion", "En ejecución");
                default:                              return I18n.T("eh.pln.est.cerrado", "Cerrado");
            }
        }

        public static Color ColorEstadoPlan(BE.EstadoPlanAccion e)
        {
            switch (e)
            {
                case BE.EstadoPlanAccion.Borrador:    return Estilo.Amarillo;
                case BE.EstadoPlanAccion.Aprobado:    return Estilo.AzulDeSeleccion;
                case BE.EstadoPlanAccion.EnEjecucion: return Estilo.Naranja;
                default:                              return Estilo.Verde;
            }
        }

        /// <summary>
        /// Próxima transición de ESTADO del plan (no confundir con qué etapa se está consultando):
        /// titular + botón a mostrar en la franja de "Próxima acción". Devuelve TagBoton/TextoBoton
        /// null cuando no hay ninguna transición disponible ahora mismo (ya sea porque todavía falta
        /// algo —p. ej. el período de observación— o porque el plan ya está completamente cerrado).
        /// Refleja fielmente <see cref="BE.PlanAccion.PuedeAprobar"/>/PuedeEjecutar/PuedeCerrar — no
        /// agrega ninguna condición nueva (p. ej. NO exige decidir todas las recomendaciones para
        /// habilitar "Aprobar plan": esa regla no existe en la BLL real).
        /// </summary>
        public static (string Titulo, string TagBoton, string TextoBoton) ProximaAccion(BE.PlanAccion p, bool tieneEvaluacion, System.DateTime ahora)
        {
            if (p.PuedeAprobar())
                return (I18n.T("eh.pln.next.aprobar", "Listo para aprobar el plan."), "eh.btn.aprobarplan", I18n.T("eh.btn.aprobarplan", "Aprobar plan"));
            if (p.PuedeEjecutar())
                return (I18n.T("eh.pln.next.ejecutar", "Listo para marcar el plan en ejecución."), "eh.btn.enejecucion", I18n.T("eh.btn.enejecucion", "Marcar en ejecución"));
            if (p.PuedeCerrar())
                return (I18n.T("eh.pln.next.cerrar", "Listo para cerrar el plan."), "eh.btn.cerrarplan", I18n.T("eh.btn.cerrarplan", "Cerrar plan"));
            if (p.Estado == BE.EstadoPlanAccion.Cerrado && !tieneEvaluacion)
            {
                if (p.PeriodoObservacionCumplido(ahora))
                    return (I18n.T("eh.pln.next.evaluarlisto", "Listo para evaluar resultados."), "eh.btn.evaluarresultados", I18n.T("eh.btn.evaluarresultados", "Evaluar resultados"));
                return (string.Format(I18n.T("eh.pln.next.evaluarespera", "Resultados evaluables a partir del {0}."),
                    p.FechaEvaluacionPrevista?.ToString("dd/MM/yyyy") ?? "—"), null, null);
            }
            return (I18n.T("eh.pln.next.ninguno", "Plan finalizado."), null, null);
        }

        // ── Decisión ────────────────────────────────────────────────────────────────
        public static string Decision(BE.DecisionRecomendacion d)
        {
            switch (d)
            {
                case BE.DecisionRecomendacion.Aprobada:   return I18n.T("eh.pln.dec.aprobada", "Aprobada");
                case BE.DecisionRecomendacion.Descartada: return I18n.T("eh.pln.dec.descartada", "Descartada");
                default:                                  return I18n.T("eh.pln.dec.pendiente", "Pendiente");
            }
        }

        public static Color ColorDecision(BE.DecisionRecomendacion d)
        {
            switch (d)
            {
                case BE.DecisionRecomendacion.Aprobada:   return Estilo.Verde;
                case BE.DecisionRecomendacion.Descartada: return Estilo.Gris400;
                default:                                  return Estilo.Amarillo;
            }
        }

        // ── Tipo de recomendación ────────────────────────────────────────────────────
        public static string Tipo(BE.TipoRecomendacion t)
        {
            switch (t)
            {
                case BE.TipoRecomendacion.ListaEsperaAlta:    return I18n.T("eh.pln.tipo.esperaalta", "Lista de espera alta");
                case BE.TipoRecomendacion.OcupacionBaja:      return I18n.T("eh.pln.tipo.ocupbaja", "Ocupación baja");
                case BE.TipoRecomendacion.OrganizadorRevisar: return I18n.T("eh.pln.tipo.orgrevisar", "Revisar organizador");
                default:                                      return I18n.T("eh.pln.tipo.plansubutil", "Plan subutilizado");
            }
        }

        public static Color ColorTipo(BE.TipoRecomendacion t)
        {
            switch (t)
            {
                case BE.TipoRecomendacion.ListaEsperaAlta:    return Estilo.AzulDeSeleccion;
                case BE.TipoRecomendacion.OcupacionBaja:      return Estilo.Coral;
                case BE.TipoRecomendacion.OrganizadorRevisar: return Estilo.Naranja;
                default:                                      return Estilo.Amarillo;
            }
        }

        // ── Acción sugerida ──────────────────────────────────────────────────────────
        public static string Accion(BE.AccionSugerida a)
        {
            switch (a)
            {
                case BE.AccionSugerida.ProgramarEdicion:     return I18n.T("eh.pln.acc.programar", "Programar nueva edición");
                case BE.AccionSugerida.AmpliarCupo:          return I18n.T("eh.pln.acc.ampliar", "Ampliar cupo");
                case BE.AccionSugerida.CancelarExperiencia:  return I18n.T("eh.pln.acc.cancelar", "Cancelar experiencia");
                case BE.AccionSugerida.NoReprogramar:        return I18n.T("eh.pln.acc.noreprog", "No reprogramar");
                case BE.AccionSugerida.SuspenderOrganizador: return I18n.T("eh.pln.acc.suspender", "Suspender organizador");
                default:                                     return I18n.T("eh.pln.acc.revisarplan", "Revisar plan");
            }
        }

        /// <summary>La acción concreta implica una operación destructiva (baja/cancelación)?</summary>
        public static bool AccionDestructiva(BE.AccionSugerida a)
            => a == BE.AccionSugerida.CancelarExperiencia || a == BE.AccionSugerida.SuspenderOrganizador;

        // ── Estado de reserva (distribución por estado) ──────────────────────────────
        public static string EstadoReserva(int estado)
        {
            switch (estado)
            {
                case 1: return I18n.T("eh.pln.estado.1", "Confirmada");
                case 2: return I18n.T("eh.pln.estado.2", "Cancelada");
                case 3: return I18n.T("eh.pln.estado.3", "Asistió");
                case 4: return I18n.T("eh.pln.estado.4", "No asistió");
                default: return "?";
            }
        }

        public static Color ColorEstadoReserva(int estado)
        {
            switch (estado)
            {
                case 1: return Estilo.AzulDeSeleccion;
                case 2: return Estilo.Coral;
                case 3: return Estilo.Verde;
                case 4: return Estilo.Naranja;
                default: return Estilo.Gris400;
            }
        }

        // ── Dirección de "mejora" por indicador — DELEGA en BLL.EvaluacionKpi (lógica de negocio pura,
        // extraída para ser testeable sin WinForms). Acá solo se resuelve texto i18n y color.
        public static string Resultado(string claveSnapshot, decimal delta)
        {
            switch (BLL.EvaluacionKpi.Clasificar(claveSnapshot, delta))
            {
                case BLL.EvaluacionKpi.Resultado.Mejoro:    return I18n.T("eh.pln.res.mejoro", "Mejoró");
                case BLL.EvaluacionKpi.Resultado.Empeoro:   return I18n.T("eh.pln.res.empeoro", "Empeoró");
                case BLL.EvaluacionKpi.Resultado.SinCambio: return I18n.T("eh.pln.res.igual", "Sin cambios");
                default:                                    return I18n.T("eh.pln.res.neutro", "Informativo");
            }
        }

        public static Color ColorResultado(string claveSnapshot, decimal delta)
        {
            switch (BLL.EvaluacionKpi.Clasificar(claveSnapshot, delta))
            {
                case BLL.EvaluacionKpi.Resultado.Mejoro:  return Estilo.Verde;
                case BLL.EvaluacionKpi.Resultado.Empeoro: return Estilo.Coral;
                default:                                  return Estilo.Gris400;
            }
        }

        // ── Clave de KpiSnapshot → etiqueta ──────────────────────────────────────────
        public static string KpiSnapshot(string clave)
        {
            switch (clave)
            {
                case "EnEspera":             return I18n.T("eh.pln.kpi.espera", "En lista de espera");
                case "OfertasVencidas":      return I18n.T("eh.pln.kpi.vencidas", "Ofertas vencidas");
                case "CancelacionesATiempo": return I18n.T("eh.pln.kpi.cancatiempo", "Cancel. a tiempo");
                case "CancelacionesTardias": return I18n.T("eh.pln.kpi.canctardias", "Cancel. tardías");
                case "Inasistencias":        return I18n.T("eh.pln.kpi.inasist", "Inasistencias");
                case "Altas":                return I18n.T("eh.pln.kpi.altas", "Altas");
                case "Renovaciones":         return I18n.T("eh.pln.kpi.renovaciones", "Renovaciones");
                case "IngresosPlanes":       return I18n.T("eh.pln.kpi.ingresos", "Ingresos por planes");
                default: return clave;
            }
        }
    }
}

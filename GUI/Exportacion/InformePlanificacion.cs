using System;
using System.Collections.Generic;
using System.Linq;

namespace GUI.Exportacion
{
    /// <summary>
    /// PN03 — Arma los informes de planificación (tablero y plan de acción) como
    /// <see cref="DocumentoReporte"/> estructurado (secciones, tablas, fichas clave/valor, gráfico de
    /// variación), para exportar PDF DIRECTO a archivo con <see cref="ExportadorPdfArchivo"/> (PDFsharp)
    /// o, para impresión convencional, con <see cref="ExportadorPdfDocumento"/> (PrintPreview). Incluye
    /// período, filtros, KPIs del tablero, las recomendaciones con su evidencia y, cuando corresponde,
    /// el comparativo Antes/Después real del plan.
    /// </summary>
    public static class InformePlanificacion
    {
        private static string Tr(string k, string fb) => GUI.I18n.T(k, fb);
        private static readonly string[] _clavesKpi =
            { "EnEspera", "OfertasVencidas", "CancelacionesATiempo", "CancelacionesTardias", "Inasistencias", "Altas", "Renovaciones", "IngresosPlanes" };

        /// <summary>
        /// PN03 — Informe EJECUTIVO del plan de acción como <see cref="DocumentoReporte"/> estructurado
        /// (encabezado institucional, resumen, comparativo Antes/Después, decisiones, evaluación y
        /// trazabilidad). Lo renderiza <see cref="ExportadorPdfDocumento"/>. Solo usa datos reales del
        /// plan, sus recomendaciones y los snapshots; no inventa conclusiones ni atribuye causalidad.
        /// </summary>
        public static DocumentoReporte DocumentoPlan(BE.PlanAccion plan, List<BE.KpiSnapshot> snaps, List<BE.RecomendacionPlan> recs)
        {
            recs = recs ?? new List<BE.RecomendacionPlan>();
            snaps = snaps ?? new List<BE.KpiSnapshot>();
            var antes = snaps.Where(s => s.Momento == BE.MomentoKpi.Antes).ToDictionary(s => s.Clave, s => s.Valor);
            var despues = snaps.Where(s => s.Momento == BE.MomentoKpi.Despues).ToDictionary(s => s.Clave, s => s.Valor);
            bool hayDespues = despues.Count > 0;

            var doc = new DocumentoReporte
            {
                Institucion = "ExperienceHub",
                Modulo = Tr("eh.pln.modulo", "PN03 – Planificación"),
                Titulo = Tr("eh.pdf.plan.titulo", "Informe de resultados del plan de acción"),
                Subtitulo = "#" + plan.IdPlanAccion + " · " + plan.Titulo,
                NombreArchivo = "InformePlan_" + plan.IdPlanAccion
            };

            // ── A. Datos del plan ──────────────────────────────────────────────────
            doc.ClaveValor(new List<(string, string)>
            {
                (Tr("eh.pdf.estado", "Estado"), GUI.Planificacion.PlanificacionTextos.EstadoPlan(plan.Estado)),
                (Tr("eh.pln.plan.periodo", "Período analizado"), $"{plan.PeriodoDesde:dd/MM/yyyy} – {plan.PeriodoHasta:dd/MM/yyyy}"),
                (Tr("rpt.txt.generado", "Generado"), DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
            });

            // ── B. Resumen ejecutivo ───────────────────────────────────────────────
            int ejec = recs.Count(r => r.Ejecutada);
            int aprob = recs.Count(r => r.Decision == BE.DecisionRecomendacion.Aprobada);
            int descart = recs.Count(r => r.Decision == BE.DecisionRecomendacion.Descartada);
            int pend = recs.Count(r => r.Decision == BE.DecisionRecomendacion.Pendiente);
            doc.Seccion(Tr("eh.pdf.sec.resumen", "Resumen ejecutivo"));
            doc.ClaveValor(new List<(string, string)>
            {
                (Tr("eh.pdf.recomendaciones", "Recomendaciones"), recs.Count.ToString()),
                (Tr("eh.pdf.aprobadas", "Aprobadas"), aprob.ToString()),
                (Tr("eh.pdf.descartadas", "Descartadas"), descart.ToString()),
                (Tr("eh.pdf.ejecutadas", "Ejecutadas"), ejec.ToString()),
                (Tr("eh.pdf.pordecidir", "Por decidir"), pend.ToString()),
            });
            int mejoraron = 0, empeoraron = 0, sinCambio = 0;
            if (hayDespues)
                foreach (var c in _clavesKpi)
                {
                    if (!antes.ContainsKey(c) && !despues.ContainsKey(c)) continue;
                    decimal d = (despues.TryGetValue(c, out var vd) ? vd : 0) - (antes.TryGetValue(c, out var va) ? va : 0);
                    switch (BLL.EvaluacionKpi.Clasificar(c, d))
                    {
                        case BLL.EvaluacionKpi.Resultado.Mejoro:  mejoraron++; break;
                        case BLL.EvaluacionKpi.Resultado.Empeoro: empeoraron++; break;
                        default: sinCambio++; break;
                    }
                }
            bool periodoCumplido = plan.PeriodoObservacionCumplido(DateTime.Now);
            string resumenTxt;
            if (hayDespues)
                resumenTxt = string.Format(Tr("eh.pdf.resumen.txt", "Se ejecutaron {0} de {1} acción(es) aprobada(s). En el comparativo, {2} indicador(es) mejoraron, {3} empeoraron y {4} fueron neutros/sin cambios."), ejec, aprob, mejoraron, empeoraron, sinCambio);
            else if (plan.Estado != BE.EstadoPlanAccion.Cerrado)
                resumenTxt = string.Format(Tr("eh.pdf.resumen.sincierre", "El plan aún no está cerrado: se decidieron {0} recomendación(es) y se ejecutaron {1}. La evaluación final se realiza tras el período de observación posterior al cierre."), aprob + descart, ejec);
            else if (!periodoCumplido)
                resumenTxt = string.Format(Tr("eh.pdf.resumen.enobservacion", "Plan cerrado. El período de observación está en curso: los resultados serán evaluables a partir del {0}."), plan.FechaEvaluacionPrevista?.ToString("dd/MM/yyyy") ?? "—");
            else
                resumenTxt = Tr("eh.pdf.resumen.evaluable", "Plan cerrado y período de observación cumplido: los resultados están listos para evaluarse en la aplicación.");
            doc.Parrafo(resumenTxt);

            // ── C. Comparativo Antes / Después ─────────────────────────────────────
            doc.Seccion(Tr("eh.pdf.sec.comparativo", "Comparativo Antes / Después"));
            if (!hayDespues)
            {
                doc.Parrafo(!periodoCumplido || plan.Estado != BE.EstadoPlanAccion.Cerrado
                    ? resumenTxt
                    : Tr("eh.pdf.resumen.evaluable", "Plan cerrado y período de observación cumplido: los resultados están listos para evaluarse en la aplicación."));
                doc.Nota(string.Format(Tr("eh.pln.seg.periodos", "Antes: {0}   ·   Después: {1}"),
                    $"{plan.PeriodoDesde:dd/MM/yyyy} – {plan.PeriodoHasta:dd/MM/yyyy}", Tr("eh.pln.seg.pend", "pendiente")));
            }
            else
            {
                var filasCmp = new List<string[]>();
                var datosBarras = new List<(string, decimal)>();
                foreach (var c in _clavesKpi)
                {
                    if (!antes.ContainsKey(c) && !despues.ContainsKey(c)) continue;
                    bool money = c == "IngresosPlanes";
                    decimal a = antes.TryGetValue(c, out var va) ? va : 0;
                    decimal d = despues.TryGetValue(c, out var vd) ? vd : 0;
                    decimal delta = d - a;
                    string sPct = a != 0 ? Math.Round(100m * delta / Math.Abs(a), 0).ToString("+0;-0;0") + "%" : "—";
                    filasCmp.Add(new[]
                    {
                        GUI.Planificacion.PlanificacionTextos.KpiSnapshot(c),
                        money ? a.ToString("$ #,##0") : a.ToString("0.##"),
                        money ? d.ToString("$ #,##0") : d.ToString("0.##"),
                        money ? delta.ToString("$ +#,##0;$ -#,##0;0") : delta.ToString("+0.##;-0.##;0"),
                        sPct,
                        GUI.Planificacion.PlanificacionTextos.Resultado(c, delta)
                    });
                    if (a != 0) datosBarras.Add((GUI.Planificacion.PlanificacionTextos.KpiSnapshot(c), Math.Round(100m * delta / Math.Abs(a), 0)));
                }
                doc.Tabla(
                    new[] { Tr("eh.pln.seg.indicador", "Indicador"), Tr("eh.pln.seg.antes", "Antes"), Tr("eh.pln.seg.despues", "Después"), "Δ", "%", Tr("eh.pln.seg.resultado", "Resultado") },
                    filasCmp, new float[] { 28, 14, 14, 15, 10, 19 }, new[] { false, true, true, true, true, true });
                string periodoDespues = $"{plan.FechaCierre.Value:dd/MM/yyyy} – {plan.FechaCierre.Value.AddDays(plan.DuracionPeriodoDias):dd/MM/yyyy}";
                doc.Nota(string.Format(Tr("eh.pln.seg.periodos", "Antes: {0}   ·   Después: {1}"),
                    $"{plan.PeriodoDesde:dd/MM/yyyy} – {plan.PeriodoHasta:dd/MM/yyyy}", periodoDespues));
                if (datosBarras.Count > 0)
                {
                    doc.Espacio(6);
                    doc.BarrasVariacion(Tr("eh.pdf.grafico.variacion", "Variación porcentual por indicador"), datosBarras);
                }
            }

            // ── D. Decisiones y ejecución ──────────────────────────────────────────
            doc.Seccion(Tr("eh.pdf.sec.decisiones", "Decisiones y ejecución"));
            var filasDec = new List<string[]>();
            foreach (var r in recs)
            {
                string estadoEjec = r.Ejecutada
                    ? (r.FechaEjecucion.HasValue ? string.Format(Tr("eh.pdf.ejecel", "Ejecutada {0}"), r.FechaEjecucion.Value.ToString("dd/MM/yy")) : Tr("eh.pln.rec.ejecutada", "Ejecutada"))
                    : Tr("eh.pdf.noejec", "No ejecutada");
                filasDec.Add(new[]
                {
                    r.NombreEntidad ?? "",
                    GUI.Planificacion.PlanificacionTextos.Tipo(r.Tipo),
                    GUI.Planificacion.PlanificacionTextos.Accion(r.AccionSugerida),
                    GUI.Planificacion.PlanificacionTextos.Decision(r.Decision),
                    estadoEjec,
                    string.IsNullOrWhiteSpace(r.MotivoDecision) ? "—" : r.MotivoDecision
                });
            }
            if (filasDec.Count == 0) filasDec.Add(new[] { Tr("eh.pln.vacio.recplan", "Sin recomendaciones."), "", "", "", "", "" });
            doc.Tabla(
                new[] { Tr("eh.pdf.col.entidad", "Entidad"), Tr("eh.pdf.col.problema", "Problema"), Tr("eh.pln.rec.accion", "Acción sugerida"), Tr("eh.pdf.col.decision", "Decisión"), Tr("eh.pdf.col.ejecucion", "Ejecución"), Tr("eh.pln.rec.motivo", "Motivo") },
                filasDec, new float[] { 17, 17, 17, 13, 14, 22 }, null);

            // ── E. Evaluación de resultados ────────────────────────────────────────
            doc.Seccion(Tr("eh.pdf.sec.evaluacion", "Evaluación de resultados"));
            if (hayDespues)
                doc.Parrafo(string.Format(Tr("eh.pln.seg.conclusion2", "Mejoraron: {0} · empeoraron: {1} · sin cambios/informativo: {2}."), mejoraron, empeoraron, sinCambio));
            else
                doc.Parrafo(resumenTxt);
            doc.Nota(Tr("eh.pln.seg.aviso", "Comparativo informativo: la variación puede deberse también a otros factores."));

            // ── F. Trazabilidad ────────────────────────────────────────────────────
            doc.Seccion(Tr("eh.pdf.sec.trazabilidad", "Información de trazabilidad"));
            doc.ClaveValor(new List<(string, string)>
            {
                (Tr("eh.pdf.idplan", "Identificador del plan"), "#" + plan.IdPlanAccion),
                (Tr("eh.pln.plan.creado", "Creado"), plan.FechaCreacion.ToString("dd/MM/yyyy HH:mm")),
                (Tr("eh.pdf.cierre", "Cierre"), plan.FechaCierre.HasValue ? plan.FechaCierre.Value.ToString("dd/MM/yyyy HH:mm") : "—"),
                (Tr("eh.pdf.emision", "Emisión"), DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                (Tr("eh.pdf.estado", "Estado"), GUI.Planificacion.PlanificacionTextos.EstadoPlan(plan.Estado)),
                (Tr("eh.pdf.recids", "Recomendaciones (IDs)"), recs.Count > 0 ? string.Join(", ", recs.Select(r => "#" + r.IdRecomendacion)) : "—"),
            });
            return doc;
        }

        /// <summary>
        /// PN03 — Informe del impacto SIMULADO (<see cref="GUI.Planificacion.SimuladorResultados"/>) de las
        /// tareas ya ejecutadas de un plan, como <see cref="DocumentoReporte"/> estructurado. Declarado
        /// como estimación en el título y en cada sección — nunca se presenta como la evaluación real
        /// (esa sigue siendo <see cref="DocumentoPlan"/> una vez que existe el snapshot "Después").
        /// </summary>
        internal static DocumentoReporte DocumentoImpactoSimulado(BE.PlanAccion plan, GUI.Planificacion.SimuladorResultados.Resumen resumen)
        {
            var doc = new DocumentoReporte
            {
                Institucion = "ExperienceHub",
                Modulo = Tr("eh.pln.modulo", "PN03 – Planificación"),
                Titulo = Tr("eh.pdf.sim.titulo", "Informe de impacto SIMULADO"),
                Subtitulo = "#" + plan.IdPlanAccion + " · " + plan.Titulo,
                NombreArchivo = "InformeSimulado_Plan" + plan.IdPlanAccion
            };

            doc.Nota(Tr("eh.pdf.sim.aviso", "⚠ Documento ILUSTRATIVO: los indicadores de este informe son una simulación determinística (misma entrada → mismo resultado), no mediciones reales. La evaluación oficial del plan se genera por separado, con datos reales, una vez cumplido el período de observación posterior al cierre."));
            doc.ClaveValor(new List<(string, string)>
            {
                (Tr("eh.pdf.estado", "Estado"), GUI.Planificacion.PlanificacionTextos.EstadoPlan(plan.Estado)),
                (Tr("rpt.txt.generado", "Generado"), DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                (Tr("eh.pdf.sim.ejecutadas", "Tareas ejecutadas incluidas"), resumen.EjecutadasCount.ToString()),
                (Tr("eh.pdf.sim.pendientes", "Tareas aprobadas sin ejecutar (excluidas)"), resumen.PendientesCount.ToString()),
            });

            if (resumen.EjecutadasCount == 0)
            {
                doc.Seccion(Tr("eh.pln.sim.titulo", "Resultados simulados del impacto ejecutado"));
                doc.Parrafo(Tr("eh.pln.sim.vacio", "Todavía no se ejecutó ninguna tarea de este plan."));
                return doc;
            }

            doc.Seccion(Tr("eh.pdf.sim.resumen", "Resumen ejecutivo (simulado)"));
            string Fmt(decimal v, bool money) => money ? v.ToString("$ #,##0") : v.ToString("0");
            doc.Tabla(
                new[] { Tr("eh.pdf.sim.indicador", "Indicador"), Tr("eh.pln.seg.antes", "Antes"), Tr("eh.pln.seg.despues", "Después (simulado)"), "Δ" },
                new List<string[]>
                {
                    new[] { Tr("eh.pln.sim.kpi.ocup", "Ocupación promedio"), resumen.OcupAntes + "%", resumen.OcupDespues + "%", (resumen.OcupDespues - resumen.OcupAntes).ToString("+0;-0;0") + " pp" },
                    new[] { Tr("eh.pln.sim.kpi.reservas", "Reservas"), resumen.ReservasAntes.ToString(), resumen.ReservasDespues.ToString(), (resumen.ReservasDespues - resumen.ReservasAntes).ToString("+0;-0;0") },
                    new[] { Tr("eh.pln.sim.kpi.espera", "Lista de espera"), resumen.EsperaAntes.ToString(), resumen.EsperaDespues.ToString(), (resumen.EsperaDespues - resumen.EsperaAntes).ToString("+0;-0;0") },
                    new[] { Tr("eh.pln.sim.kpi.cancel", "Cancelaciones"), resumen.CancelAntes.ToString(), resumen.CancelDespues.ToString(), (resumen.CancelDespues - resumen.CancelAntes).ToString("+0;-0;0") },
                    new[] { Tr("eh.pln.sim.kpi.ingresos", "Ingresos estimados"), Fmt(resumen.IngresosAntes, true), Fmt(resumen.IngresosDespues, true), (resumen.IngresosDespues - resumen.IngresosAntes).ToString("$ +#,##0;$ -#,##0;0") },
                },
                new float[] { 36, 20, 24, 20}, new[] { false, true, true, true });
            // "pp" (puntos porcentuales) para Ocupación —ya es un %, no un cambio relativo—; el resto de
            // indicadores son cantidades/montos, no corresponde la distinción pp/%. MISMO criterio que
            // la pantalla (PlanificacionFormBase.TarjetaKpiSim / UnidadKpi), para que informe y pantalla
            // nunca se contradigan.
            doc.Parrafo(string.Format(Tr("eh.pdf.sim.efectividad", "Efectividad simulada de las recomendaciones ejecutadas: {0}% — {1} de {2} tarea(s) con impacto medible mejoraron (≥3 de 5 indicadores positivos)."), resumen.Efectividad, resumen.MejoraronCount, resumen.ConImpacto));

            var barras = new List<(string, decimal)>
            {
                (Tr("eh.pln.sim.kpi.reservas", "Reservas"), resumen.ReservasAntes != 0 ? Math.Round(100m * (resumen.ReservasDespues - resumen.ReservasAntes) / Math.Abs(resumen.ReservasAntes), 0) : 0),
                (Tr("eh.pln.sim.kpi.espera", "Lista de espera"), resumen.EsperaAntes != 0 ? Math.Round(100m * (resumen.EsperaDespues - resumen.EsperaAntes) / Math.Abs(resumen.EsperaAntes), 0) : 0),
                (Tr("eh.pln.sim.kpi.cancel", "Cancelaciones"), resumen.CancelAntes != 0 ? Math.Round(100m * (resumen.CancelDespues - resumen.CancelAntes) / Math.Abs(resumen.CancelAntes), 0) : 0),
                (Tr("eh.pln.sim.kpi.ingresos", "Ingresos estimados"), resumen.IngresosAntes != 0 ? Math.Round(100m * (resumen.IngresosDespues - resumen.IngresosAntes) / Math.Abs(resumen.IngresosAntes), 0) : 0),
            };
            doc.Espacio(6);
            doc.BarrasVariacion(Tr("eh.pdf.grafico.variacion", "Variación porcentual por indicador"), barras);

            doc.Seccion(Tr("eh.pln.sim.detalle.titulo", "Impacto simulado por recomendación ejecutada"));
            var filas = new List<string[]>();
            foreach (var im in resumen.Impactos)
            {
                string D(decimal a, decimal d) => im.TieneImpacto ? (d - a).ToString("+0;-0;0") : "—";
                string resultado = im.TieneImpacto ? (im.Mejora ? Tr("eh.pln.sim.resultado.mejoro", "Mejoró") : Tr("eh.pln.sim.resultado.neutro", "Neutro")) : Tr("eh.pln.sim.resultado.sinimpacto", "Sin impacto medible");
                filas.Add(new[]
                {
                    im.Rec.NombreEntidad ?? "", GUI.Planificacion.PlanificacionTextos.Accion(im.Rec.AccionSugerida),
                    D(im.ReservasAntes, im.ReservasDespues), D(im.EsperaAntes, im.EsperaDespues),
                    im.TieneImpacto ? (im.IngresosDespues - im.IngresosAntes).ToString("$ +#,##0;$ -#,##0;0") : "—",
                    resultado, im.Resumen
                });
            }
            doc.Tabla(
                new[] { Tr("eh.pdf.col.entidad", "Entidad"), Tr("eh.pln.rec.accion", "Acción sugerida"), Tr("eh.pln.sim.col.reservas", "Δ Reservas"), Tr("eh.pln.sim.col.espera", "Δ Espera"), Tr("eh.pln.sim.col.ingresos", "Δ Ingresos"), Tr("eh.pln.col.decision", "Resultado"), Tr("eh.pdf.sim.detalle", "Detalle") },
                filas, new float[] { 14, 14, 10, 10, 12, 12, 28 }, new[] { false, false, true, true, true, false, false });

            doc.Seccion(Tr("eh.pdf.sim.aclaracion.titulo", "Aclaración metodológica"));
            doc.Nota(Tr("eh.pdf.sim.aclaracion", "Los valores de este informe se generan con una semilla determinística basada en el identificador real de cada recomendación: para el mismo plan siempre arrojan los mismos números, pero NO provienen de mediciones reales del sistema. Útiles para previsualizar la magnitud esperada del impacto, no para reportar resultados reales."));
            return doc;
        }

        /// <summary>
        /// PN03 — Informe EJECUTIVO del tablero del período como <see cref="DocumentoReporte"/> estructurado
        /// (KPIs, ocupación/desempeño/planes en tablas, recomendaciones). Muestra el período y los filtros
        /// aplicados. Solo datos reales del tablero; sin inventar conclusiones.
        /// </summary>
        public static DocumentoReporte DocumentoTablero(BE.TableroPeriodo t, List<BE.RecomendacionGenerada> recs, string filtrosTexto)
        {
            recs = recs ?? new List<BE.RecomendacionGenerada>();
            var doc = new DocumentoReporte
            {
                Institucion = "ExperienceHub",
                Modulo = Tr("eh.pln.modulo", "PN03 – Planificación"),
                Titulo = Tr("eh.pdf.tablero.titulo", "Informe de planificación del período"),
                Subtitulo = $"{t.Desde:dd/MM/yyyy} – {t.Hasta:dd/MM/yyyy}",
                NombreArchivo = $"Planificacion_{t.Desde:yyyyMMdd}_{t.Hasta:yyyyMMdd}"
            };
            doc.ClaveValor(new List<(string, string)>
            {
                (Tr("eh.pln.plan.periodo", "Período analizado"), $"{t.Desde:dd/MM/yyyy} – {t.Hasta:dd/MM/yyyy}"),
                (Tr("eh.pdf.filtros", "Filtros aplicados"), string.IsNullOrWhiteSpace(filtrosTexto) ? Tr("eh.pln.sinfiltros", "Sin filtros (todo el catálogo)") : filtrosTexto),
                (Tr("rpt.txt.generado", "Generado"), DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
            });

            doc.Seccion(Tr("eh.pdf.sec.indicadores", "Indicadores globales"));
            doc.ClaveValor(new List<(string, string)>
            {
                (Tr("eh.pln.kpi.espera", "En lista de espera"), t.EnEsperaTotal.ToString()),
                (Tr("eh.pln.kpi.vencidas", "Ofertas vencidas"), t.OfertasVencidas.ToString()),
                (Tr("eh.pln.kpi.cancatiempo", "Cancel. a tiempo"), t.CancelacionesATiempo.ToString()),
                (Tr("eh.pln.kpi.canctardias", "Cancel. tardías"), t.CancelacionesTardias.ToString()),
                (Tr("eh.pln.kpi.inasist", "Inasistencias"), t.Inasistencias.ToString()),
                (Tr("eh.pln.kpi.altas", "Altas"), t.Altas.ToString()),
                (Tr("eh.pln.kpi.renovaciones", "Renovaciones"), t.Renovaciones.ToString()),
                (Tr("eh.pln.kpi.ingresos", "Ingresos por planes"), t.IngresosPlanes.ToString("$ #,##0.00")),
            });

            if (t.Experiencias != null && t.Experiencias.Count > 0)
            {
                doc.Seccion(Tr("eh.pln.g.ocup", "Ocupación por experiencia"));
                doc.Tabla(
                    new[] { Tr("eh.pln.col.experiencia", "Experiencia"), Tr("eh.pln.col.ocupacion", "Ocupación %"), Tr("eh.pln.col.reservados", "Reservados"), Tr("eh.pln.col.cupo", "Cupo máx."), Tr("eh.pln.col.espera", "En espera") },
                    t.Experiencias.OrderBy(x => x.Ocupacion).Select(x => new[] { x.Nombre, x.Ocupacion.ToString("0.0") + "%", x.LugaresReservados.ToString(), x.CupoMaximo.ToString(), x.EnEspera.ToString() }).ToList(),
                    new float[] { 40, 16, 15, 15, 14 }, new[] { false, true, true, true, true });
            }
            if (t.Organizadores != null && t.Organizadores.Count > 0)
            {
                doc.Seccion(Tr("eh.pln.g.calif", "Desempeño por organizador"));
                doc.Tabla(
                    new[] { Tr("eh.pln.organizador", "Organizador"), Tr("eh.pln.kpi.calif", "Calificación"), Tr("eh.pln.kpi.inasistpct", "% inasistencia") },
                    t.Organizadores.Select(o => new[] { o.Nombre, o.CalificacionPromedio.ToString("0.0"), o.PctInasistencia.ToString("0.0") + "%" }).ToList(),
                    new float[] { 56, 22, 22 }, new[] { false, true, true });
            }
            if (t.Planes != null && t.Planes.Count > 0)
            {
                doc.Seccion(Tr("eh.pln.g.planes", "Uso de planes"));
                doc.Tabla(
                    new[] { Tr("eh.doc.f.plan", "Plan"), Tr("eh.pln.g.planes", "Uso"), Tr("eh.pdf.suscripciones", "Suscripciones") },
                    t.Planes.Select(p => new[] { p.Nombre, p.PctUso.ToString("0.0") + "% (" + p.ReservasUsadas + "/" + p.ReservasPermitidas + ")", p.Suscripciones.ToString() }).ToList(),
                    new float[] { 48, 32, 20 }, new[] { false, false, true });
            }

            doc.Seccion(Tr("eh.pln.sec.recomperiodo", "Recomendaciones del período"));
            if (recs.Count == 0)
                doc.Parrafo(Tr("eh.pdf.sinrecs", "Ninguna: los indicadores están dentro de los umbrales configurados."));
            else
                doc.Tabla(
                    new[] { Tr("eh.pdf.col.problema", "Problema"), Tr("eh.pdf.col.entidad", "Entidad"), Tr("eh.pln.rec.accion", "Acción sugerida"), Tr("eh.pln.rec.evidencia", "Evidencia") },
                    recs.Select(r => new[] { GUI.Planificacion.PlanificacionTextos.Tipo(r.Tipo), r.NombreEntidad ?? "", GUI.Planificacion.PlanificacionTextos.Accion(r.AccionSugerida), r.Evidencia ?? "" }).ToList(),
                    new float[] { 20, 22, 22, 36 }, null);
            return doc;
        }

    }
}

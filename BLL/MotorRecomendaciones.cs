using System.Collections.Generic;

namespace BLL
{
    /// <summary>
    /// PN03 — Motor de recomendaciones por REGLAS (decisión PURA, sin BD ni sesión: testeable).
    /// Recorre los KPIs del período (BE.TableroPeriodo) y, comparándolos con los umbrales configurables
    /// (BE.UmbralesRecomendacion, cargados desde [Parametro]), emite recomendaciones con su evidencia.
    /// Cada regla dispara en o por encima/por debajo de su umbral; no dispara del lado inocuo.
    /// La decisión (aprobar/descartar) y la ejecución las hace el Gerente/Coordinador en PlanAccionService.
    /// </summary>
    public static class MotorRecomendaciones
    {
        public static List<BE.RecomendacionGenerada> Generar(BE.TableroPeriodo t, BE.UmbralesRecomendacion u)
        {
            var recs = new List<BE.RecomendacionGenerada>();
            if (t == null) return recs;
            u = u ?? new BE.UmbralesRecomendacion();

            // 1) Lista de espera alta → abrir nueva edición (o ampliar cupo si todavía hay margen de sala).
            foreach (var e in t.Experiencias ?? new List<BE.KpiExperiencia>())
            {
                if (e.EnEspera >= u.ListaEsperaAlta)
                    recs.Add(new BE.RecomendacionGenerada
                    {
                        Tipo = BE.TipoRecomendacion.ListaEsperaAlta,
                        AccionSugerida = BE.AccionSugerida.ProgramarEdicion,
                        IdEntidad = e.IdExperiencia, NombreEntidad = e.Nombre,
                        Evidencia = string.Format("{0} cliente(s) en lista de espera (umbral {1}); cupo {2}.",
                            e.EnEspera, u.ListaEsperaAlta, e.CupoMaximo)
                    });
            }

            // 2) Ocupación baja → revisar / dar de baja (solo experiencias con cupo definido).
            foreach (var e in t.Experiencias ?? new List<BE.KpiExperiencia>())
            {
                if (e.CupoMaximo > 0 && e.Ocupacion < u.OcupacionBaja)
                    recs.Add(new BE.RecomendacionGenerada
                    {
                        Tipo = BE.TipoRecomendacion.OcupacionBaja,
                        AccionSugerida = BE.AccionSugerida.CancelarExperiencia,
                        IdEntidad = e.IdExperiencia, NombreEntidad = e.Nombre,
                        Evidencia = string.Format("Ocupación {0}% (umbral {1}%): {2}/{3} lugares.",
                            e.Ocupacion, u.OcupacionBaja, e.LugaresReservados, e.CupoMaximo)
                    });
            }

            // 3) Organizador a revisar → calificación baja O inasistencias altas → suspender organizador.
            foreach (var o in t.Organizadores ?? new List<BE.KpiOrganizador>())
            {
                bool califBaja = o.Calificaciones > 0 && o.CalificacionPromedio < u.CalifBaja;
                bool inasisAlta = (o.Asistencias + o.Inasistencias) > 0 && o.PctInasistencia > u.InasistenciaAlta;
                if (califBaja || inasisAlta)
                    recs.Add(new BE.RecomendacionGenerada
                    {
                        Tipo = BE.TipoRecomendacion.OrganizadorRevisar,
                        AccionSugerida = BE.AccionSugerida.SuspenderOrganizador,
                        IdEntidad = o.IdOrganizador, NombreEntidad = o.Nombre,
                        Evidencia = califBaja && inasisAlta
                            ? string.Format("Calificación {0} (umbral {1}) e inasistencia {2}% (umbral {3}%).",
                                o.CalificacionPromedio, u.CalifBaja, o.PctInasistencia, u.InasistenciaAlta)
                            : califBaja
                                ? string.Format("Calificación promedio {0} sobre {1} (umbral {2}).",
                                    o.CalificacionPromedio, o.Calificaciones, u.CalifBaja)
                                : string.Format("Inasistencia {0}% (umbral {1}%): {2} de {3}.",
                                    o.PctInasistencia, u.InasistenciaAlta, o.Inasistencias, o.Asistencias + o.Inasistencias)
                    });
            }

            // 4) Plan subutilizado → revisar beneficios (tarea para el Administrador).
            foreach (var p in t.Planes ?? new List<BE.KpiPlan>())
            {
                if (p.ReservasPermitidas > 0 && p.PctUso < u.PlanSubutilizado)
                    recs.Add(new BE.RecomendacionGenerada
                    {
                        Tipo = BE.TipoRecomendacion.PlanSubutilizado,
                        AccionSugerida = BE.AccionSugerida.RevisarPlan,
                        IdEntidad = p.IdPlan, NombreEntidad = p.Nombre,
                        Evidencia = string.Format("Uso {0}% (umbral {1}%): {2}/{3} reservas · {4} suscripción(es).",
                            p.PctUso, u.PlanSubutilizado, p.ReservasUsadas, p.ReservasPermitidas, p.Suscripciones)
                    });
            }

            return recs;
        }
    }
}

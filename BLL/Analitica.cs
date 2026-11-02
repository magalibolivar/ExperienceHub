using System;

namespace BLL
{
    /// <summary>
    /// PN03 — Servicio de analítica: arma el tablero del período (KPIs) desde DAL.AnaliticaDAL y
    /// carga los umbrales del motor de recomendaciones desde [Parametro]. Solo lectura; no persiste.
    /// </summary>
    public class Analitica
    {
        private readonly DAL.AnaliticaDAL _dal = new DAL.AnaliticaDAL();
        private readonly DAL.Parametro    _par = new DAL.Parametro();

        /// <summary>Tablero de KPIs del período, con filtros opcionales (ciudad/categoría/organizador).</summary>
        public BE.TableroPeriodo CalcularTablero(DateTime desde, DateTime hasta,
            int? ciudad = null, int? categoria = null, int? organizador = null)
        {
            PermisosAccion.Exigir(BE.Patentes.Planificacion);

            var t = new BE.TableroPeriodo { Desde = desde, Hasta = hasta };
            t.Experiencias  = _dal.KpisExperiencia(desde, hasta, ciudad, categoria, organizador);
            t.Organizadores = _dal.KpisOrganizador(desde, hasta, ciudad, categoria, organizador);
            t.Planes        = _dal.KpisPlan(desde, hasta, ciudad, categoria, organizador);
            t.ReservasMensuales = _dal.ReservasPorMes(desde, hasta, ciudad, categoria, organizador);
            t.ReservasPorEstado = _dal.ReservasPorEstado(desde, hasta, ciudad, categoria, organizador);
            _dal.Globales(t, desde, hasta, ciudad, categoria, organizador);
            return t;
        }

        /// <summary>Umbrales del motor de recomendaciones, leídos de [Parametro] (con defaults de BE).</summary>
        public BE.UmbralesRecomendacion CargarUmbrales()
        {
            var def = new BE.UmbralesRecomendacion();
            return new BE.UmbralesRecomendacion
            {
                ListaEsperaAlta  = _par.ObtenerEntero ("PN03.UmbralListaEsperaAlta", def.ListaEsperaAlta),
                OcupacionBaja    = _par.ObtenerDecimal("PN03.UmbralOcupacionBaja",   def.OcupacionBaja),
                CalifBaja        = _par.ObtenerDecimal("PN03.UmbralCalifBaja",       def.CalifBaja),
                InasistenciaAlta = _par.ObtenerDecimal("PN03.UmbralInasistencia",    def.InasistenciaAlta),
                PlanSubutilizado = _par.ObtenerDecimal("PN03.UmbralPlanSubutil",     def.PlanSubutilizado)
            };
        }

        /// <summary>Tablero + recomendaciones generadas en un solo paso (conveniencia para la GUI).</summary>
        public System.Collections.Generic.List<BE.RecomendacionGenerada> Recomendar(BE.TableroPeriodo t)
            => MotorRecomendaciones.Generar(t, CargarUmbrales());
    }
}

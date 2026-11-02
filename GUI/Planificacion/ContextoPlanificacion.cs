using System;
using System.Collections.Generic;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 — Contexto COMPARTIDO de la planificación entre los formularios especializados.
    /// Guarda el período y los filtros vigentes (para que al pasar de Resumen a Demanda, etc., no se
    /// pierdan) y CACHEA el <see cref="BE.TableroPeriodo"/> por "firma" de filtros: si dos formularios
    /// piden el tablero con los mismos filtros, se calcula UNA sola vez (sin recalcular lo que no cambió).
    /// También comparte las recomendaciones generadas y los umbrales restaurados de una vista guardada.
    /// Es estado de sesión de UI (no toca reglas de negocio).
    /// </summary>
    internal static class ContextoPlanificacion
    {
        public static DateTime Desde = DateTime.Today.AddMonths(-12);
        public static DateTime Hasta = DateTime.Today;
        public static int? Ciudad;
        public static int? Categoria;
        public static int? Organizador;

        public static List<BE.RecomendacionGenerada> RecsPeriodo;
        public static BE.UmbralesRecomendacion UmbralesVista;   // umbrales de una vista abierta (o null)

        /// <summary>Plan vigente en el recorrido de 3 etapas (Analizar → Ejecutar → Evaluar). Lo setea
        /// la etapa 1 al crear un plan, y la etapa 2/3 al seleccionar uno; permite retomar sin buscar.</summary>
        public static int? PlanSeleccionadoId;

        private static BE.TableroPeriodo _cache;
        private static string _firma;

        public static string Firma()
            => $"{Desde:yyyyMMdd}|{Hasta:yyyyMMdd}|{Ciudad}|{Categoria}|{Organizador}";

        /// <summary>Tablero del período vigente. Reusa la caché si los filtros no cambiaron.</summary>
        public static BE.TableroPeriodo ObtenerTablero(BLL.Analitica analitica, bool forzar = false)
        {
            string f = Firma();
            if (!forzar && _cache != null && _firma == f) return _cache;
            _cache = analitica.CalcularTablero(Desde, Hasta, Ciudad, Categoria, Organizador);
            _firma = f;
            RecsPeriodo = null;   // los filtros cambiaron → las recomendaciones previas ya no aplican
            return _cache;
        }

        /// <summary>Recomendaciones del período (se generan una vez y se comparten entre formularios).</summary>
        public static List<BE.RecomendacionGenerada> ObtenerRecomendaciones(BLL.Analitica analitica)
        {
            if (RecsPeriodo != null) return RecsPeriodo;
            var t = ObtenerTablero(analitica);
            RecsPeriodo = UmbralesVista != null
                ? BLL.MotorRecomendaciones.Generar(t, UmbralesVista)
                : analitica.Recomendar(t);
            return RecsPeriodo;
        }

        public static void Invalidar() { _cache = null; _firma = null; RecsPeriodo = null; }
    }
}

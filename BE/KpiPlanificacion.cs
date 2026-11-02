using System.Collections.Generic;

namespace BE
{
    /// <summary>PN03 — Umbrales del motor de recomendaciones (se cargan desde [Parametro]).</summary>
    public class UmbralesRecomendacion
    {
        public int     ListaEsperaAlta { get; set; } = 5;     // inscripciones en espera
        public decimal OcupacionBaja   { get; set; } = 40m;   // % de ocupación
        public decimal CalifBaja       { get; set; } = 3.0m;  // promedio 1..5
        public decimal InasistenciaAlta{ get; set; } = 30m;   // % de inasistencia
        public decimal PlanSubutilizado{ get; set; } = 25m;   // % de uso del plan
    }

    /// <summary>KPI de una experiencia (insumo del motor y del tablero).</summary>
    public class KpiExperiencia
    {
        public int    IdExperiencia { get; set; }
        public string Nombre        { get; set; }
        public int    CupoMaximo    { get; set; }
        public int    LugaresReservados { get; set; }   // confirmadas + asistió + no asistió (ocupan/ocuparon)
        public int    EnEspera      { get; set; }         // inscripciones Esperando
        /// <summary>% de ocupación = reservados / cupo * 100 (0 si cupo = 0).</summary>
        public decimal Ocupacion => CupoMaximo > 0 ? System.Math.Round(100m * LugaresReservados / CupoMaximo, 1) : 0m;
    }

    /// <summary>KPI de un organizador (calidad: calificación + inasistencias).</summary>
    public class KpiOrganizador
    {
        public int    IdOrganizador { get; set; }
        public string Nombre        { get; set; }
        public decimal CalificacionPromedio { get; set; }  // 0 si no hay calificaciones
        public int    Calificaciones { get; set; }
        public int    Asistencias   { get; set; }
        public int    Inasistencias { get; set; }
        /// <summary>% de inasistencia sobre asistencia+inasistencia (0 si no hubo ninguna).</summary>
        public decimal PctInasistencia
            => (Asistencias + Inasistencias) > 0
               ? System.Math.Round(100m * Inasistencias / (Asistencias + Inasistencias), 1) : 0m;
    }

    /// <summary>PN03 — Reservas de un mes del período (serie temporal para "evolución mensual").
    /// Cuenta las reservas efectivas (estados 1 Confirmada / 3 Asistió / 4 No asistió) por mes de la
    /// fecha de la experiencia. POCO puro: la etiqueta legible la arma la GUI según el idioma.</summary>
    public class KpiMes
    {
        public int Anio { get; set; }
        public int Mes  { get; set; }   // 1..12
        public int Reservas { get; set; }
    }

    /// <summary>PN03 — Cantidad de reservas de un estado dentro del período ("distribución por estado").
    /// Estado: 1 Confirmada, 2 Cancelada, 3 Asistió, 4 No asistió. La etiqueta legible la arma la GUI.</summary>
    public class KpiEstadoReserva
    {
        public int Estado   { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>KPI de uso de un plan (reservas usadas vs permitidas en el período).</summary>
    public class KpiPlan
    {
        public int    IdPlan { get; set; }
        public string Nombre { get; set; }
        public int    Suscripciones { get; set; }
        public int    ReservasUsadas { get; set; }
        public int    ReservasPermitidas { get; set; }   // suscripciones * reservas/mes * meses
        /// <summary>% de uso = usadas / permitidas * 100 (0 si no hay cupo permitido).</summary>
        public decimal PctUso => ReservasPermitidas > 0
            ? System.Math.Round(100m * ReservasUsadas / ReservasPermitidas, 1) : 0m;
    }

    /// <summary>
    /// Tablero del período (PN03): agrega todos los KPIs calculados por BLL.Analitica a partir de
    /// consultas de solo lectura. Es el insumo del motor de recomendaciones y del tablero visual.
    /// </summary>
    public class TableroPeriodo
    {
        public System.DateTime Desde { get; set; }
        public System.DateTime Hasta { get; set; }

        public List<KpiExperiencia> Experiencias { get; set; } = new List<KpiExperiencia>();
        public List<KpiOrganizador> Organizadores { get; set; } = new List<KpiOrganizador>();
        public List<KpiPlan>        Planes       { get; set; } = new List<KpiPlan>();

        // Series para las visualizaciones del Resumen ejecutivo (solo lectura, agregadas por el DAL).
        public List<KpiMes>           ReservasMensuales { get; set; } = new List<KpiMes>();
        public List<KpiEstadoReserva> ReservasPorEstado { get; set; } = new List<KpiEstadoReserva>();

        // Globales / demanda insatisfecha / cancelaciones / asistencia.
        public int EnEsperaTotal      { get; set; }
        public int OfertasVencidas    { get; set; }
        public int CancelacionesATiempo { get; set; }
        public int CancelacionesTardias { get; set; }
        public int Inasistencias      { get; set; }
        public int Altas              { get; set; }
        public int Renovaciones       { get; set; }
        public decimal IngresosPlanes { get; set; }
    }

    /// <summary>Una recomendación emitida por el motor, con su evidencia (datos que la justifican).</summary>
    public class RecomendacionGenerada
    {
        public TipoRecomendacion Tipo { get; set; }
        public AccionSugerida    AccionSugerida { get; set; }
        public int    IdEntidad { get; set; }
        public string NombreEntidad { get; set; }
        public string Evidencia { get; set; }
    }
}

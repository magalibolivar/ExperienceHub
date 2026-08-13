using System;

namespace BE
{
    /// <summary>
    /// CU-ANA-03 — Fila de análisis de popularidad y ocupación de una experiencia.
    /// Es un resultado DERIVADO (no persiste): lo arma BLL.AnalisisExperiencias a partir de
    /// Experiencia + Reserva + ListaEspera + Calificacion.
    /// </summary>
    public class AnalisisExperiencia
    {
        public int      IdExperiencia { get; set; }
        public string   Experiencia   { get; set; }
        public string   Categoria     { get; set; }
        public string   Ciudad        { get; set; }
        public DateTime Fecha         { get; set; }
        public int      CupoMaximo    { get; set; }

        /// <summary>Reservas que ocuparon cupo (no canceladas): mide la demanda.</summary>
        public int      Reservas      { get; set; }
        public int      Canceladas    { get; set; }
        /// <summary>Interesados en lista de espera (demanda insatisfecha).</summary>
        public int      EnEspera      { get; set; }

        /// <summary>Ocupación 0..1 = Reservas / CupoMaximo (acotada a 1).</summary>
        public double   Ocupacion     { get; set; }
        /// <summary>Promedio de calificación 0..5 (0 si no tiene calificaciones).</summary>
        public double   PromedioCalif { get; set; }

        /// <summary>Segmento de demanda: "Alta demanda" / "Normal" / "Baja demanda".</summary>
        public string   Segmento      { get; set; }

        // ── Presentación (para la grilla) ─────────────────────────────────────
        public string OcupacionTexto => (Ocupacion * 100).ToString("0") + "%";
        public string CalificacionTexto => PromedioCalif > 0 ? PromedioCalif.ToString("0.0") + " ★" : "—";
        /// <summary>Etiqueta corta de demanda para la grilla ("Alta" / "Normal" / "Baja").</summary>
        public string SegmentoCorto =>
            Segmento == "Alta demanda" ? "Alta" : Segmento == "Baja demanda" ? "Baja" : "Normal";
    }
}

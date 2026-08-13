using System.Collections.Generic;

namespace BE
{
    /// <summary>CU-ANA-05 — Fila de cancelaciones agrupada (por experiencia, categoría o motivo).</summary>
    public class FilaCancelacion
    {
        public string Clave      { get; set; }
        /// <summary>Total de reservas del grupo (base para la tasa). En "por motivo" es igual a Canceladas.</summary>
        public int    Total      { get; set; }
        public int    Canceladas { get; set; }
        /// <summary>Tasa de cancelación 0..1 = Canceladas / Total (0 en la vista por motivo).</summary>
        public double Tasa       { get; set; }

        public string TasaTexto => (Tasa * 100).ToString("0") + "%";
    }

    /// <summary>
    /// CU-ANA-05 — Resultado del análisis de cancelaciones. Resultado DERIVADO (no persiste):
    /// lo arma BLL.AnalisisCancelaciones a partir de Reserva (estado Cancelada + motivo) y Experiencia.
    /// </summary>
    public class ResumenCancelaciones
    {
        public int    TotalReservas   { get; set; }
        public int    TotalCanceladas { get; set; }
        /// <summary>Tasa global de cancelación 0..1.</summary>
        public double TasaGlobal      { get; set; }

        public List<FilaCancelacion> PorExperiencia { get; set; } = new List<FilaCancelacion>();
        public List<FilaCancelacion> PorCategoria   { get; set; } = new List<FilaCancelacion>();
        public List<FilaCancelacion> PorMotivo      { get; set; } = new List<FilaCancelacion>();
    }
}

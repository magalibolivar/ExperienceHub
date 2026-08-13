using System;
using System.Collections.Generic;

namespace BE
{
    /// <summary>
    /// CU-ANA-04 — Cliente detectado en riesgo de abandono. Resultado DERIVADO (no persiste):
    /// lo arma BLL.AnalisisAbandono cruzando la última reserva del cliente con el estado de su
    /// suscripción, según una regla explícita (sin IA).
    /// </summary>
    public class ClienteEnRiesgo
    {
        public int       IdCliente       { get; set; }
        public string    Cliente         { get; set; }
        public string    Plan            { get; set; }

        public DateTime? UltimaReserva   { get; set; }
        /// <summary>Días desde la última reserva; -1 si nunca reservó.</summary>
        public int       DiasSinReservar { get; set; }

        /// <summary>Estado de la suscripción en texto: "Activa" / "Vencida" / "Vence en N d" / "Sin suscripción".</summary>
        public string    Suscripcion     { get; set; }
        /// <summary>Días hasta el vencimiento; int.MaxValue si no aplica.</summary>
        public int       DiasParaVencer  { get; set; }

        /// <summary>Nivel de riesgo: "Alto" / "Medio" / "Bajo".</summary>
        public string    Nivel           { get; set; }
        /// <summary>Motivos concretos que explican por qué está en riesgo.</summary>
        public List<string> Motivos      { get; set; } = new List<string>();

        // ── Presentación (para la grilla) ─────────────────────────────────────
        public string UltimaReservaTexto  => UltimaReserva.HasValue ? UltimaReserva.Value.ToString("dd/MM/yyyy") : "—";
        public string DiasSinReservarTexto => DiasSinReservar < 0 ? "—" : DiasSinReservar.ToString();
        public string MotivosTexto         => string.Join("  ·  ", Motivos);
    }
}

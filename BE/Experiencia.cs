using System;

namespace BE
{
    /// <summary>Entidad Experiencia. Mapea la tabla [Experiencia].</summary>
    public class Experiencia
    {
        public int               IdExperiencia   { get; set; }
        public string            Nombre          { get; set; }
        public string            Descripcion     { get; set; }

        /// <summary>FK → Categoria.</summary>
        public int               IdCategoria     { get; set; }
        /// <summary>Nombre de la categoría (cargado por JOIN, no persiste).</summary>
        public string            NombreCategoria { get; set; }

        /// <summary>FK → Ciudad.</summary>
        public int               IdCiudad        { get; set; }
        /// <summary>Nombre de la ciudad (cargado por JOIN, no persiste).</summary>
        public string            NombreCiudad    { get; set; }

        /// <summary>Dirección/lugar puntual dentro de la ciudad.</summary>
        public string            Ubicacion       { get; set; }

        /// <summary>Fecha del evento (solo la parte de fecha).</summary>
        public DateTime          Fecha           { get; set; }
        /// <summary>Horario de inicio.</summary>
        public TimeSpan          HoraInicio      { get; set; }
        /// <summary>Duración en minutos.</summary>
        public int               DuracionMinutos { get; set; }

        /// <summary>FK → Organizador.</summary>
        public int               IdOrganizador     { get; set; }
        /// <summary>Nombre del organizador (cargado por JOIN, no persiste).</summary>
        public string            NombreOrganizador { get; set; }

        public int               CupoMaximo      { get; set; }
        public int               CupoDisponible  { get; set; }

        /// <summary>Edad mínima requerida para asistir.</summary>
        public int               EdadMinima      { get; set; }

        /// <summary>Solo accesible para clientes con plan que habilite experiencias premium.</summary>
        public bool              Premium         { get; set; }

        public EstadoExperiencia Estado          { get; set; } = EstadoExperiencia.Programada;

        // ── Derivados de presentación ─────────────────────────────────────────

        /// <summary>Momento exacto de inicio (fecha + hora).</summary>
        public DateTime FechaHoraInicio => Fecha.Date + HoraInicio;

        /// <summary>Momento exacto de fin (inicio + duración).</summary>
        public DateTime FechaHoraFin => FechaHoraInicio.AddMinutes(DuracionMinutos);

        /// <summary>Descripción para grillas: "Cata de vinos — 12 cupos — Programada".</summary>
        public string ResumenEstado =>
            $"{Nombre} — {CupoDisponible}/{CupoMaximo} cupos — {Estado}";

        // ── Comportamiento ────────────────────────────────────────────────────

        /// <summary>Indica si la experiencia admite una reserva de la cantidad de lugares indicada.</summary>
        public bool TieneCupo(int lugares = 1)
            => Estado == EstadoExperiencia.Programada && CupoDisponible >= lugares;

        /// <summary>True si la experiencia ya no tiene cupos disponibles.</summary>
        public bool EstaCompleta() => CupoDisponible <= 0;

        /// <summary>True si la experiencia todavía no ocurrió.</summary>
        public bool EsFutura() => FechaHoraInicio > DateTime.Now;

        /// <summary>Días de anticipación entre ahora y el inicio del evento (negativo si ya pasó).</summary>
        public double DiasDeAnticipacion() => (FechaHoraInicio - DateTime.Now).TotalDays;

        /// <summary>
        /// Indica si esta experiencia se solapa en el tiempo con otra
        /// (usado para impedir reservar dos experiencias en horarios superpuestos).
        /// Dos intervalos [a1,a2) y [b1,b2) se solapan si a1 &lt; b2 y b1 &lt; a2.
        /// </summary>
        public bool SeSuperponeCon(Experiencia otra)
        {
            if (otra == null) return false;
            return FechaHoraInicio < otra.FechaHoraFin
                && otra.FechaHoraInicio < FechaHoraFin;
        }

        /// <summary>True si un cliente con la edad indicada cumple la edad mínima.</summary>
        public bool CumpleEdadMinima(int edadCliente) => edadCliente >= EdadMinima;
    }
}

using System;

namespace BE
{
    /// <summary>
    /// Entidad — Suscripción de un cliente a un plan. Concentra la vigencia y el consumo
    /// del beneficio mensual (reservas). Mapea la tabla [Suscripcion].
    /// Reemplaza a los campos de plan/vencimiento que antes vivían sueltos en Cliente.
    /// </summary>
    public class Suscripcion
    {
        public int               IdSuscripcion         { get; set; }
        public int               IdCliente             { get; set; }
        public int               IdPlan                { get; set; }

        public DateTime          FechaInicio           { get; set; }
        /// <summary>Null = sin vencimiento.</summary>
        public DateTime?         FechaVencimiento      { get; set; }

        public EstadoSuscripcion Estado                { get; set; } = EstadoSuscripcion.Activa;

        /// <summary>Reservas ya consumidas en el período mensual en curso.</summary>
        public int               ReservasConsumidasMes { get; set; }

        // Datos por JOIN (no persisten)
        public string            NombrePlan            { get; set; }
        /// <summary>Reservas mensuales que otorga el plan (cargado por JOIN).</summary>
        public int               ReservasDelPlan       { get; set; }
        /// <summary>Días de anticipación máxima que otorga el plan (cargado por JOIN).</summary>
        public int               AnticipacionMaxDelPlan { get; set; }
        /// <summary>Acceso premium que otorga el plan (cargado por JOIN).</summary>
        public bool              AccesoPremiumDelPlan  { get; set; }

        // ── Comportamiento ────────────────────────────────────────────────────

        /// <summary>True si la suscripción está activa y no venció.</summary>
        public bool EstaVigente()
        {
            if (Estado != EstadoSuscripcion.Activa) return false;
            if (!FechaVencimiento.HasValue) return true;
            return FechaVencimiento.Value.Date >= DateTime.Today;
        }

        /// <summary>Reservas que aún puede hacer este mes según el plan.</summary>
        public int ReservasRestantes()
            => Math.Max(0, ReservasDelPlan - ReservasConsumidasMes);

        /// <summary>True si todavía puede consumir al menos una reserva este mes.</summary>
        public bool PuedeReservar() => EstaVigente() && ReservasRestantes() > 0;

        /// <summary>
        /// True si se puede reservar una experiencia que ocurre dentro de la anticipación
        /// máxima permitida por el plan (regla: no reservar con demasiada anticipación).
        /// </summary>
        public bool AnticipacionPermitida(double diasHastaExperiencia)
            => diasHastaExperiencia <= AnticipacionMaxDelPlan;

        /// <summary>True si la suscripción vence dentro de los próximos días indicados.</summary>
        public bool ProximaAVencer(int diasAlerta = 7) =>
            FechaVencimiento.HasValue
            && FechaVencimiento.Value.Date >= DateTime.Today
            && (FechaVencimiento.Value.Date - DateTime.Today).TotalDays <= diasAlerta;

        public int DiasHastaVencimiento() =>
            FechaVencimiento.HasValue
                ? Math.Max(0, (FechaVencimiento.Value.Date - DateTime.Today).Days)
                : int.MaxValue;
    }
}

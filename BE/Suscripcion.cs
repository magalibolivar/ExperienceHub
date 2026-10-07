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

        /// <summary>
        /// Mes del período de consumo en curso (primer día del mes). Cuando el mes cambia, la tarea
        /// de sistema reinicia <see cref="ReservasConsumidasMes"/>. Null = aún no sellado.
        /// </summary>
        public DateTime?         PeriodoConsumo        { get; set; }

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

        // ── Ciclo de vida (decisiones PURAS: sin BD ni sesión, testeables) ──────

        /// <summary>True si la suscripción venció por fecha (independiente del estado guardado).</summary>
        public bool EstaVencida(DateTime hoy)
            => FechaVencimiento.HasValue && FechaVencimiento.Value.Date < hoy.Date;

        /// <summary>
        /// Estado REAL considerando la fecha: una Activa cuyo vencimiento ya pasó se considera Vencida,
        /// aunque en la BD todavía figure Activa (hasta que la tarea de sistema la actualice).
        /// Suspendida y Vencida se devuelven tal cual.
        /// </summary>
        public EstadoSuscripcion EstadoVigenciaCalculado(DateTime hoy)
            => (Estado == EstadoSuscripcion.Activa && EstaVencida(hoy))
                ? EstadoSuscripcion.Vencida
                : Estado;

        /// <summary>Se renueva una suscripción que no esté ya activa-y-vigente (vencida o suspendida, o activa por extender).</summary>
        public bool PuedeRenovar() => true;

        /// <summary>Solo se suspende una suscripción Activa.</summary>
        public bool PuedeSuspender() => Estado == EstadoSuscripcion.Activa;

        /// <summary>Solo se reactiva una suscripción Suspendida.</summary>
        public bool PuedeReactivar() => Estado == EstadoSuscripcion.Suspendida;

        /// <summary>
        /// ¿Hay que reiniciar el consumo mensual? True si todavía no se selló un período, o si el
        /// período sellado es de un mes anterior al de <paramref name="hoy"/> (rollover mensual).
        /// </summary>
        public static bool DebeReiniciarConsumo(DateTime hoy, DateTime? periodoActual)
        {
            if (!periodoActual.HasValue) return true;
            return periodoActual.Value.Year != hoy.Year || periodoActual.Value.Month != hoy.Month;
        }

        /// <summary>Primer día del mes de una fecha (sello del período de consumo).</summary>
        public static DateTime InicioDeMes(DateTime fecha) => new DateTime(fecha.Year, fecha.Month, 1);
    }
}

using System;

namespace BE
{
    /// <summary>
    /// Entidad — Reserva de una experiencia generada por el Agente para un Cliente.
    /// Mapea la tabla [Reserva]. Una reserva corresponde a UNA experiencia.
    /// </summary>
    public class Reserva
    {
        public int IdReserva { get; set; }

        public int IdCliente { get; set; }

        public int IdExperiencia { get; set; }

        /// <summary>Empleado (agente) que registró la reserva.</summary>
        public int IdEmpleado { get; set; }

        public EstadoReserva Estado { get; set; } = EstadoReserva.Pendiente;

        public DateTime FechaReserva { get; set; }
        public DateTime? FechaCancelacion { get; set; }

        public string MotivoCancelacion { get; set; }

        /// <summary>Acompañantes incluidos en la reserva (según el plan del cliente).</summary>
        public int CantidadInvitados { get; set; }

        // ── Datos cargados por JOIN (no persisten) ────────────────────────────
        public string NombreCliente { get; set; }
        public string NombreExperiencia { get; set; }
        public string NombreEmpleado { get; set; }

        /// <summary>Momento de inicio de la experiencia reservada (cargado por JOIN).</summary>
        public DateTime? FechaHoraExperiencia { get; set; }

        /// <summary>Duración de la experiencia reservada, en minutos (cargado por JOIN).</summary>
        public int DuracionExperienciaMinutos { get; set; }

        /// <summary>Puntaje (1–5) de la calificación si el cliente ya calificó; null si no. Cargado por JOIN, no persiste.</summary>
        public int? Puntaje { get; set; }

        // ── Derivados de presentación ─────────────────────────────────────────

        /// <summary>Lugares que consume esta reserva: el titular + sus invitados.</summary>
        public int LugaresOcupados => 1 + Math.Max(0, CantidadInvitados);

        public string Resumen =>
            $"Reserva #{IdReserva} — {NombreCliente ?? $"Cliente {IdCliente}"} — " +
            $"{NombreExperiencia ?? $"Experiencia {IdExperiencia}"} — {Estado}";

        // ── Comportamiento (máquina de estados) ───────────────────────────────
        // Pendiente → Confirmada → Asistio / NoAsistio
        // Pendiente/Confirmada → Cancelada

        /// <summary>La reserva puede confirmarse solo si está Pendiente.</summary>
        public bool PuedeConfirmarse() => Estado == EstadoReserva.Pendiente;

        /// <summary>La reserva puede cancelarse mientras no se haya registrado la asistencia.</summary>
        public bool PuedeCancelarse()
            => Estado == EstadoReserva.Pendiente || Estado == EstadoReserva.Confirmada;

        /// <summary>Puede registrarse la asistencia solo sobre una reserva Confirmada.</summary>
        public bool PuedeRegistrarAsistencia() => Estado == EstadoReserva.Confirmada;

        /// <summary>Solo se puede calificar una experiencia a la que el cliente asistió.</summary>
        public bool PuedeCalificarse() => Estado == EstadoReserva.Asistio;

        /// <summary>True si la reserva ocupa cupo actualmente (Pendiente o Confirmada).</summary>
        public bool OcupaCupo()
            => Estado == EstadoReserva.Pendiente || Estado == EstadoReserva.Confirmada;

        /// <summary>
        /// Valida que la transición de estado sea permitida según el flujo definido:
        ///   Pendiente  → Confirmada | Cancelada
        ///   Confirmada → Asistio | NoAsistio | Cancelada
        ///   Asistio / NoAsistio / Cancelada → (estados finales)
        /// </summary>
        public bool TransicionValida(EstadoReserva destino)
        {
            switch (Estado)
            {
                case EstadoReserva.Pendiente:
                    return destino == EstadoReserva.Confirmada
                        || destino == EstadoReserva.Cancelada;

                case EstadoReserva.Confirmada:
                    return destino == EstadoReserva.Asistio
                        || destino == EstadoReserva.NoAsistio
                        || destino == EstadoReserva.Cancelada;

                case EstadoReserva.Asistio:
                case EstadoReserva.NoAsistio:
                case EstadoReserva.Cancelada:
                default:
                    return false; // estados finales
            }
        }
    }
}

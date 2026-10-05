namespace BE
{
    /// <summary>
    /// Resultado de verificar la disponibilidad REAL de cupo de una experiencia.
    /// PN01 — CU01-DEP "Verificar Disponibilidad": operación de SOLO LECTURA, no compromete
    /// cupo. La reserva efectiva (comprometer el cupo de forma atómica) ocurre recién en
    /// CU02-DEP, dentro de DAL.Reserva.CrearConCupo.
    ///
    /// En el dominio de ExperienceHub, el "Depósito" de WardrobeFlow se reinterpreta como el
    /// cupo real de la experiencia: verificar disponibilidad = constatar que la experiencia
    /// siga Programada y con lugares suficientes.
    /// </summary>
    public class ResultadoDisponibilidad
    {
        public int    IdExperiencia      { get; set; }
        public int    LugaresSolicitados { get; set; }
        public int    CupoDisponible     { get; set; }
        public bool   Disponible         { get; set; }
        /// <summary>Texto informativo del resultado (apto para mostrar al Vendedor/Depósito).</summary>
        public string Motivo             { get; set; }

        /// <summary>
        /// Decisión PURA (sin BD ni sesión): ¿hay cupo real para los lugares pedidos?
        /// Una experiencia solo ofrece cupo si está Programada; Completa/Cancelada/Finalizada/EnCurso no.
        /// </summary>
        public static ResultadoDisponibilidad Evaluar(
            int idExperiencia, EstadoExperiencia estado, int cupoDisponible, int lugares)
        {
            var r = new ResultadoDisponibilidad
            {
                IdExperiencia      = idExperiencia,
                LugaresSolicitados = lugares,
                CupoDisponible     = cupoDisponible
            };

            if (lugares <= 0)
            {
                r.Disponible = false;
                r.Motivo     = "La cantidad de lugares solicitada debe ser mayor que cero.";
                return r;
            }

            if (estado != EstadoExperiencia.Programada)
            {
                r.Disponible = false;
                r.Motivo     = $"La experiencia no admite reservas (estado '{estado}').";
                return r;
            }

            if (cupoDisponible < lugares)
            {
                r.Disponible = false;
                r.Motivo     = $"Cupo insuficiente: se piden {lugares} y quedan {cupoDisponible}.";
                return r;
            }

            r.Disponible = true;
            r.Motivo     = $"Disponible: quedan {cupoDisponible} lugar(es) para los {lugares} solicitado(s).";
            return r;
        }
    }
}

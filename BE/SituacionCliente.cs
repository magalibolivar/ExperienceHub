namespace BE
{
    /// <summary>
    /// Estado comercial resuelto de un cliente de cara a armar un pedido (PN01 — CU03-VEN).
    /// Reemplaza los códigos sueltos de la especificación (CLIENTE_INEXISTENTE, SIN_PLAN,
    /// SUSCRIPCION_VENCIDA) por un enum tipado.
    /// </summary>
    public enum EstadoSituacionCliente
    {
        /// <summary>Puede avanzar a armar el pedido.</summary>
        Ok = 0,
        /// <summary>No existe un cliente con esa identificación.</summary>
        ClienteInexistente = 1,
        /// <summary>El cliente existe pero no tiene un plan/suscripción asignado.</summary>
        SinPlan = 2,
        /// <summary>El cliente tiene suscripción pero no está vigente (vencida o inactiva).</summary>
        SuscripcionVencida = 3
    }

    /// <summary>
    /// Fotografía de la situación comercial de un cliente para el Vendedor (PN01 — CU03-VEN).
    /// Operación de SOLO LECTURA: no modifica nada. Se puede consultar de forma independiente
    /// de armar un pedido.
    /// </summary>
    public class SituacionCliente
    {
        public EstadoSituacionCliente Estado { get; set; }

        public int     IdCliente      { get; set; }
        public string  NombreCompleto { get; set; }

        public string  NombrePlan        { get; set; }
        /// <summary>Reservas que le restan al cliente este mes según su plan (NO es el cupo de la
        /// experiencia; ese es BE.Experiencia.CupoDisponible). Renombrado para evitar la confusión.</summary>
        public int     ReservasRestantes { get; set; }
        public System.DateTime? FechaVencimiento { get; set; }

        // COMPATIBILIDAD: campos de la antigua "reserva Pendiente bloqueante" (regla eliminada cuando
        // crear+confirmar pasó a ser atómico). Ya no los llena ConsultarSituacionCliente; se conservan
        // para no romper serialización/binding de registros o consumidores externos.
        public bool    TieneReservaActiva      { get; set; }
        public int?    IdReservaActiva         { get; set; }
        public string  NombreExperienciaActiva { get; set; }

        /// <summary>True si la situación habilita a avanzar con el armado de la reserva.</summary>
        public bool PuedeArmarReserva => Estado == EstadoSituacionCliente.Ok;

        /// <summary>
        /// Resolución PURA (sin BD ni sesión) del estado comercial a partir del cliente.
        /// La información del "pedido activo" la completa la BLL, porque requiere consultar
        /// las reservas del cliente.
        /// </summary>
        public static SituacionCliente Resolver(Cliente cliente)
        {
            if (cliente == null)
                return new SituacionCliente { Estado = EstadoSituacionCliente.ClienteInexistente };

            var s = new SituacionCliente
            {
                IdCliente      = cliente.IdCliente,
                NombreCompleto = cliente.NombreCompleto
            };

            if (!cliente.TieneSuscripcion())
            {
                s.Estado = EstadoSituacionCliente.SinPlan;
                return s;
            }

            s.NombrePlan       = cliente.Suscripcion.NombrePlan;
            s.FechaVencimiento = cliente.Suscripcion.FechaVencimiento;

            if (!cliente.SuscripcionVigente())
            {
                s.Estado = EstadoSituacionCliente.SuscripcionVencida;
                return s;
            }

            s.Estado            = EstadoSituacionCliente.Ok;
            s.ReservasRestantes = cliente.ReservasRestantes();
            return s;
        }
    }
}

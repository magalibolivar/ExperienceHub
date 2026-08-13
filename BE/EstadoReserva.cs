namespace BE
{
    /// <summary>
    /// Estado de una reserva de experiencia generada por el Agente para un Cliente.
    /// </summary>
    public enum EstadoReserva
    {
        Pendiente = 0,

        Confirmada = 1,

        Cancelada = 2,

        Asistio = 3,

        NoAsistio = 4
    }
}

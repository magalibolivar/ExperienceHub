namespace BE
{
    /// <summary>
    /// Estado de una experiencia dentro del catálogo.
    /// El estado se deriva de la fecha, el cupo y las acciones del organizador.
    /// </summary>
    public enum EstadoExperiencia
    {
        /// <summary>Publicada y con cupos disponibles; admite reservas.</summary>
        Programada = 0,

        /// <summary>Sin cupos disponibles; admite lista de espera.</summary>
        Completa = 1,

        /// <summary>Ocurriendo en este momento.</summary>
        EnCurso = 2,

        /// <summary>Ya ocurrió; habilita registrar asistencia y calificar.</summary>
        Finalizada = 3,

        /// <summary>Cancelada por el organizador; estado final.</summary>
        Cancelada = 4
    }
}

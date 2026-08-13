namespace BE
{
    /// <summary>
    /// Estado de un cliente dentro de la lista de espera de una experiencia.
    /// </summary>
    public enum EstadoListaEspera
    {
        /// <summary>Aguardando que se libere un cupo.</summary>
        Esperando = 0,

        /// <summary>Se le ofreció el cupo liberado (primero de la fila).</summary>
        Ofrecido = 1,

        /// <summary>Confirmó el cupo ofrecido; se convirtió en reserva.</summary>
        Confirmado = 2,

        /// <summary>Dejó la lista (canceló o dejó vencer la oferta).</summary>
        Retirado = 3
    }
}

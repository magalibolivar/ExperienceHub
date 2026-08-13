using System;

namespace BE
{
    /// <summary>
    /// Entidad — Entrada de un cliente en la lista de espera de una experiencia completa.
    /// El orden lo determina Posicion (FIFO). Mapea la tabla [ListaEspera].
    /// </summary>
    public class ListaEspera
    {
        public int               IdListaEspera { get; set; }
        public int               IdExperiencia { get; set; }
        public int               IdCliente     { get; set; }

        /// <summary>Posición en la fila (1 = primero en recibir el cupo liberado).</summary>
        public int               Posicion      { get; set; }

        public DateTime          FechaIngreso  { get; set; }
        public EstadoListaEspera Estado        { get; set; } = EstadoListaEspera.Esperando;

        // Datos por JOIN (no persisten)
        public string NombreCliente     { get; set; }
        public string NombreExperiencia { get; set; }

        /// <summary>True si esta entrada aún aguarda un cupo.</summary>
        public bool EstaEsperando() => Estado == EstadoListaEspera.Esperando;
    }
}

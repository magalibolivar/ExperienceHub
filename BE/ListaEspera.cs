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

        /// <summary>
        /// Momento en que se le ofreció el cupo liberado (null mientras no hay oferta).
        /// A partir de acá corre el plazo de vigencia de la oferta.
        /// </summary>
        public DateTime?         FechaOferta   { get; set; }

        // Datos por JOIN (no persisten)
        public string NombreCliente     { get; set; }
        public string NombreExperiencia { get; set; }

        /// <summary>True si esta entrada aún aguarda un cupo.</summary>
        public bool EstaEsperando() => Estado == EstadoListaEspera.Esperando;

        // ── Guardas PURAS del ciclo de la oferta (testeables sin BD) ────────────
        /// <summary>Solo se confirma/rechaza una entrada a la que se le ofreció el cupo.</summary>
        public bool PuedeConfirmarse() => Estado == EstadoListaEspera.Ofrecido;
        public bool PuedeRechazarse()  => Estado == EstadoListaEspera.Ofrecido;

        /// <summary>
        /// ¿Venció la oferta? True si hay una oferta vigente (Ofrecido con fecha) y pasaron más de
        /// <paramref name="horasVigencia"/> horas desde que se ofreció. Decisión pura para poder
        /// avanzar la cola al siguiente cliente cuando el ofrecido no respondió a tiempo.
        /// </summary>
        public bool OfertaVencida(int horasVigencia, DateTime ahora)
            => Estado == EstadoListaEspera.Ofrecido
               && FechaOferta.HasValue
               && ahora > FechaOferta.Value.AddHours(horasVigencia);
    }
}

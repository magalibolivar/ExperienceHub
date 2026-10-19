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

        /// <summary>Inicio de la experiencia asociada (cargado por JOIN). Permite vencer la oferta
        /// con margen antes del inicio y decidir si todavía se puede ofrecer el cupo (regla 5).</summary>
        public DateTime? FechaHoraExperiencia { get; set; }

        /// <summary>True si esta entrada aún aguarda un cupo.</summary>
        public bool EstaEsperando() => Estado == EstadoListaEspera.Esperando;

        // ── Guardas PURAS del ciclo de la oferta (testeables sin BD) ────────────
        /// <summary>Solo se confirma/rechaza una entrada a la que se le ofreció el cupo.</summary>
        public bool PuedeConfirmarse() => Estado == EstadoListaEspera.Ofrecido;
        public bool PuedeRechazarse()  => Estado == EstadoListaEspera.Ofrecido;

        /// <summary>
        /// ¿Venció la oferta? (regla 5) Una oferta vigente (Ofrecido con fecha) vence cuando:
        ///   • pasaron más de <paramref name="horasVigencia"/> horas desde que se ofreció, O
        ///   • falta menos de <paramref name="margenInicioHoras"/> horas para el inicio de la
        ///     experiencia (aunque el plazo no se haya cumplido), para que el siguiente de la cola
        ///     todavía tenga tiempo de responder.
        /// Decisión pura (sin BD) para avanzar/liberar la cola.
        /// </summary>
        public bool OfertaVencida(int horasVigencia, int margenInicioHoras, DateTime ahora)
        {
            if (Estado != EstadoListaEspera.Ofrecido || !FechaOferta.HasValue) return false;
            if (ahora > FechaOferta.Value.AddHours(horasVigencia)) return true;                 // venció el plazo de respuesta
            if (FechaHoraExperiencia.HasValue &&
                ahora >= FechaHoraExperiencia.Value.AddHours(-margenInicioHoras)) return true;   // se acercó al inicio
            return false;
        }

        /// <summary>
        /// Regla 5 — ¿hay margen suficiente para ofrecerle el cupo a este cliente? Solo se ofrece si
        /// falta al menos <paramref name="margenInicioHoras"/> horas para el inicio; si falta menos,
        /// el cupo queda libre para reserva directa (nadie alcanza a responder). Sin fecha de inicio
        /// conocida se permite ofrecer (no se puede evaluar el margen).
        /// </summary>
        public bool HayMargenParaOferta(int margenInicioHoras, DateTime ahora)
            => !FechaHoraExperiencia.HasValue
               || FechaHoraExperiencia.Value.AddHours(-margenInicioHoras) > ahora;
    }
}

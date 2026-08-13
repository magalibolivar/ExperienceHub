using System;

namespace BE
{
    /// <summary>
    /// Entidad Calificación que un cliente deja sobre una experiencia luego de asistir.
    /// Mapea la tabla [Calificacion].
    /// </summary>
    public class Calificacion
    {
        public int      IdCalificacion { get; set; }
        public int      IdReserva      { get; set; }
        public int      IdCliente      { get; set; }
        public int      IdExperiencia  { get; set; }

        /// <summary>Puntaje de 1 a 5.</summary>
        public int      Puntaje        { get; set; }
        public string   Comentario     { get; set; }
        public DateTime Fecha          { get; set; }

        // Datos por JOIN (no persisten)
        public string   NombreCliente     { get; set; }
        public string   NombreExperiencia { get; set; }

        /// <summary>Rango válido del puntaje.</summary>
        public const int PuntajeMinimo = 1;
        public const int PuntajeMaximo = 5;

        public bool PuntajeValido() => Puntaje >= PuntajeMinimo && Puntaje <= PuntajeMaximo;
    }
}

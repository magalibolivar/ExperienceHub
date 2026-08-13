using System.Collections.Generic;

namespace BE
{
    /// <summary>
    /// Resultado de una recomendación: la experiencia sugerida, el % de afinidad con el perfil del
    /// cliente y los motivos (reglas) por los que fue recomendada. DTO de solo lectura para la UI.
    /// </summary>
    public class RecomendacionExperiencia
    {
        public Experiencia Experiencia { get; set; }

        /// <summary>Afinidad con el perfil del cliente, de 0 a 100.</summary>
        public int Afinidad { get; set; }

        /// <summary>Explicaciones legibles de por qué se recomendó (una por regla que aplicó).</summary>
        public List<string> Motivos { get; set; } = new List<string>();
    }
}

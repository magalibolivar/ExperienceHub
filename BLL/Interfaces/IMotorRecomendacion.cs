using System.Collections.Generic;

namespace BLL.Interfaces
{
    /// <summary>
    /// Punto de extensión del motor de recomendaciones de ExperienceHub.
    /// Hoy lo implementa la versión basada en REGLAS (<see cref="BLL.Recomendacion"/>), explicable y
    /// demostrable. En el futuro puede reemplazarse o complementarse por un modelo inteligente
    /// (ML/scoring aprendido) sin modificar la UI ni el resto de la arquitectura: basta con proveer
    /// otra implementación de esta interfaz.
    /// </summary>
    public interface IMotorRecomendacion
    {
        /// <summary>Devuelve recomendaciones con % de afinidad y motivos, mejor puntuadas primero.</summary>
        List<BE.RecomendacionExperiencia> RecomendarDetallado(int idCliente, int cantidad);
    }
}

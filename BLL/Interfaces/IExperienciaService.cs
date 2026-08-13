using System.Collections.Generic;

namespace BLL.Interfaces
{
    /// <summary>Gestión del catálogo de Experiencias.</summary>
    public interface IExperienciaService
    {
        List<BE.Experiencia> ObtenerTodos();
        List<BE.Experiencia> ObtenerDisponibles();
        List<BE.Experiencia> ObtenerPorCiudad(int idCiudad);
        BE.Experiencia       ObtenerPorId(int idExperiencia);

        void Alta(string modulo, BE.Experiencia experiencia);
        void Modificar(string modulo, BE.Experiencia experiencia);
        void CambiarEstado(string modulo, BE.Experiencia experiencia, BE.EstadoExperiencia nuevoEstado);
    }
}

using System.Collections.Generic;

namespace BLL
{
    /// <summary>Lógica de negocio para el catálogo de Intereses (alimenta el perfil del cliente).</summary>
    public class Interes
    {
        private readonly DAL.Interes _dal = new DAL.Interes();

        public List<BE.Interes> ObtenerTodos() => _dal.ObtenerTodos();
        public List<BE.Interes> ObtenerPorCliente(int idCliente) => _dal.ObtenerPorCliente(idCliente);
    }
}

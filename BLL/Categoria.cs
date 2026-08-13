using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>Lógica de negocio para la gestión de categorías de experiencias.</summary>
    public class Categoria
    {
        private readonly DAL.Categoria     dal      = new DAL.Categoria();
        private readonly Servicios.Bitacora bitacora = new Servicios.Bitacora();

        public List<BE.Categoria> ObtenerActivas() => dal.ObtenerActivas();
        public List<BE.Categoria> ObtenerTodos()   => dal.ObtenerTodos();
        public BE.Categoria       ObtenerPorId(int id) => dal.ObtenerPorId(id);

        public void Alta(string modulo, BE.Categoria c)
        {
            PermisosAccion.Exigir(BE.Patentes.CategoriasEditar, BE.Patentes.Categorias);
            Validar(c);
            c.Estado = true;
            c.IdCategoria = dal.Alta(c);
            bitacora.Registrar(modulo, $"Alta Categoría: {c.Nombre}", BE.Criticidad.Baja);
        }

        public void Modificar(string modulo, BE.Categoria c)
        {
            PermisosAccion.Exigir(BE.Patentes.CategoriasEditar, BE.Patentes.Categorias);
            Validar(c);
            dal.Modificar(c);
            bitacora.Registrar(modulo, $"Modificar Categoría ID {c.IdCategoria}: {c.Nombre}", BE.Criticidad.Baja);
        }

        public void Baja(string modulo, int idCategoria)
        {
            PermisosAccion.Exigir(BE.Patentes.CategoriasEditar, BE.Patentes.Categorias);
            if (dal.ContarExperienciasEnCategoria(idCategoria) > 0)
                throw new BE.AppException("err.bll.categoria.tiene_experiencias",
                    "No se puede desactivar la categoría: tiene experiencias asociadas.");
            dal.CambiarEstado(idCategoria, false);
            bitacora.Registrar(modulo, $"Baja Categoría ID {idCategoria}", BE.Criticidad.Media);
        }

        public void Activar(string modulo, int idCategoria)
        {
            PermisosAccion.Exigir(BE.Patentes.CategoriasEditar, BE.Patentes.Categorias);
            dal.CambiarEstado(idCategoria, true);
        }

        private void Validar(BE.Categoria c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (string.IsNullOrWhiteSpace(c.Nombre))
                throw new BE.AppException("err.bll.categoria.nombre_requerido", "El nombre de la categoría es obligatorio.");
        }
    }
}

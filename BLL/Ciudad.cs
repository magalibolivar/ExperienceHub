using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>Lógica de negocio para la gestión de ciudades.</summary>
    public class Ciudad
    {
        private readonly DAL.Ciudad         dal      = new DAL.Ciudad();
        private readonly Servicios.Bitacora bitacora = new Servicios.Bitacora();

        public List<BE.Ciudad> ObtenerActivas() => dal.ObtenerActivas();
        public List<BE.Ciudad> ObtenerTodos()   => dal.ObtenerTodos();
        public BE.Ciudad       ObtenerPorId(int id) => dal.ObtenerPorId(id);

        public void Alta(string modulo, BE.Ciudad c)
        {
            PermisosAccion.Exigir(BE.Patentes.CiudadesEditar, BE.Patentes.Ciudades);
            Validar(c);
            c.Estado = true;
            c.IdCiudad = dal.Alta(c);
            bitacora.Registrar(modulo, $"Alta Ciudad: {c.Nombre}", BE.Criticidad.Baja);
        }

        public void Modificar(string modulo, BE.Ciudad c)
        {
            PermisosAccion.Exigir(BE.Patentes.CiudadesEditar, BE.Patentes.Ciudades);
            Validar(c);
            dal.Modificar(c);
            bitacora.Registrar(modulo, $"Modificar Ciudad ID {c.IdCiudad}: {c.Nombre}", BE.Criticidad.Baja);
        }

        public void Baja(string modulo, int idCiudad)
        {
            PermisosAccion.Exigir(BE.Patentes.CiudadesEditar, BE.Patentes.Ciudades);
            if (dal.ContarExperienciasEnCiudad(idCiudad) > 0)
                throw new BE.AppException("err.bll.ciudad.tiene_experiencias",
                    "No se puede desactivar la ciudad: tiene experiencias asociadas.");
            dal.CambiarEstado(idCiudad, false);
            bitacora.Registrar(modulo, $"Baja Ciudad ID {idCiudad}", BE.Criticidad.Media);
        }

        public void Activar(string modulo, int idCiudad)
        {
            PermisosAccion.Exigir(BE.Patentes.CiudadesEditar, BE.Patentes.Ciudades);
            dal.CambiarEstado(idCiudad, true);
        }

        private void Validar(BE.Ciudad c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (string.IsNullOrWhiteSpace(c.Nombre))
                throw new BE.AppException("err.bll.ciudad.nombre_requerido", "El nombre de la ciudad es obligatorio.");
        }
    }
}

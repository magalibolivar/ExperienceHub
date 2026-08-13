using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>Lógica de negocio para la gestión de organizadores de experiencias.</summary>
    public class Organizador
    {
        private readonly DAL.Organizador    dal      = new DAL.Organizador();
        private readonly Servicios.Bitacora bitacora = new Servicios.Bitacora();

        public List<BE.Organizador> ObtenerActivos()  => dal.ObtenerActivos();
        public List<BE.Organizador> ObtenerTodos()    => dal.ObtenerTodos();
        public BE.Organizador       ObtenerPorId(int id) => dal.ObtenerPorId(id);

        public void Alta(string modulo, BE.Organizador o)
        {
            PermisosAccion.Exigir(BE.Patentes.OrganizadoresEditar, BE.Patentes.Organizadores);
            Validar(o);
            o.Estado = true;
            int id = dal.Alta(o);
            o.IdOrganizador = id;
            bitacora.Registrar(modulo, $"Alta Organizador: {o.Nombre}", BE.Criticidad.Baja);
        }

        public void Modificar(string modulo, BE.Organizador o)
        {
            PermisosAccion.Exigir(BE.Patentes.OrganizadoresEditar, BE.Patentes.Organizadores);
            Validar(o);
            dal.Modificar(o);
            bitacora.Registrar(modulo, $"Modificar Organizador ID {o.IdOrganizador}: {o.Nombre}", BE.Criticidad.Baja);
        }

        public void Baja(string modulo, int idOrganizador)
        {
            PermisosAccion.Exigir(BE.Patentes.OrganizadoresEditar, BE.Patentes.Organizadores);
            if (dal.ContarExperienciasDelOrganizador(idOrganizador) > 0)
                throw new BE.AppException("err.bll.organizador.tiene_experiencias",
                    "No se puede desactivar el organizador: tiene experiencias asociadas.");
            dal.CambiarEstado(idOrganizador, false);
            bitacora.Registrar(modulo, $"Baja Organizador ID {idOrganizador}", BE.Criticidad.Media);
        }

        public void Activar(string modulo, int idOrganizador)
        {
            PermisosAccion.Exigir(BE.Patentes.OrganizadoresEditar, BE.Patentes.Organizadores);
            dal.CambiarEstado(idOrganizador, true);
        }

        private void Validar(BE.Organizador o)
        {
            if (o == null) throw new ArgumentNullException(nameof(o));
            if (string.IsNullOrWhiteSpace(o.Nombre))
                throw new BE.AppException("err.bll.organizador.nombre_requerido", "El nombre del organizador es obligatorio.");
            if (!string.IsNullOrWhiteSpace(o.Mail) && !o.Mail.Contains("@"))
                throw new BE.AppException("err.bll.organizador.mail_invalido", "El mail no tiene un formato válido.");
        }
    }
}

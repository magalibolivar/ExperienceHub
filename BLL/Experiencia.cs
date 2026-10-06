using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>Lógica de negocio para la gestión de experiencias.</summary>
    public class Experiencia : Interfaces.IExperienciaService
    {
        private readonly DAL.Experiencia            dalExp      = new DAL.Experiencia();
        private readonly Servicios.Bitacora         bitacora    = new Servicios.Bitacora();
        private readonly Servicios.BitacoraNegocio  bitacoraNeg = new Servicios.BitacoraNegocio();

        /// <summary>
        /// Mantenimiento de estados por fecha (Finalizada / EnCurso). Sin guarda de permiso: es una
        /// tarea de sistema (como el recálculo de DV), pensada para correr al arranque o por timer.
        /// </summary>
        public int ActualizarEstadosPorFecha() => dalExp.ActualizarEstadosPorFecha();

        public List<BE.Experiencia> ObtenerTodos()              => dalExp.ObtenerTodos();
        public List<BE.Experiencia> ObtenerDisponibles()        => dalExp.ObtenerDisponibles();
        /// <summary>Programadas (con cupo) + Completas (candidatas a lista de espera), para el módulo de Reservas.</summary>
        public List<BE.Experiencia> ObtenerParaReserva()        => dalExp.ObtenerParaReserva();
        public List<BE.Experiencia> ObtenerPorCiudad(int id)    => dalExp.ObtenerPorCiudad(id);
        public BE.Experiencia       ObtenerPorId(int id)        => dalExp.ObtenerPorId(id);

        // Alta de una experiencia. El cupo disponible arranca igual al cupo máximo.
        public void Alta(string modulo, BE.Experiencia experiencia)
        {
            PermisosAccion.Exigir(BE.Patentes.ExperienciasEditar, BE.Patentes.Experiencias);
            Validar(experiencia);
            experiencia.Estado         = BE.EstadoExperiencia.Programada;
            experiencia.CupoDisponible = experiencia.CupoMaximo;

            int idNuevo = dalExp.Alta(experiencia);
            experiencia.IdExperiencia = idNuevo;

            bitacora.Registrar(modulo,
                $"Alta Experiencia: {experiencia.Nombre} ({experiencia.Fecha:dd/MM/yyyy} {experiencia.HoraInicio:hh\\:mm})",
                BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.AltaExperiencia,
                $"Nueva experiencia: {experiencia.Nombre} — {experiencia.NombreCategoria} — {experiencia.NombreCiudad} — cupo {experiencia.CupoMaximo}",
                idExperiencia: idNuevo);
        }

        // Modifica datos descriptivos (no toca el cupo disponible, lo gestionan las reservas).
        public void Modificar(string modulo, BE.Experiencia experiencia)
        {
            PermisosAccion.Exigir(BE.Patentes.ExperienciasEditar, BE.Patentes.Experiencias);
            Validar(experiencia);
            dalExp.Modificar(experiencia);

            bitacora.Registrar(modulo, $"Modificar Experiencia ID {experiencia.IdExperiencia}: {experiencia.Nombre}", BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.ModificacionExperiencia,
                $"Modificación experiencia: '{experiencia.Nombre}' (ID {experiencia.IdExperiencia})",
                idExperiencia: experiencia.IdExperiencia);
        }

        public void CambiarEstado(string modulo, BE.Experiencia experiencia, BE.EstadoExperiencia nuevoEstado)
        {
            PermisosAccion.Exigir(BE.Patentes.ExperienciasEditar, BE.Patentes.Experiencias);
            dalExp.CambiarEstado(experiencia.IdExperiencia, nuevoEstado);

            bitacora.Registrar(modulo,
                $"Estado Experiencia ID {experiencia.IdExperiencia} '{experiencia.Nombre}': {experiencia.Estado} → {nuevoEstado}",
                BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.CambioEstadoExperiencia,
                $"Experiencia '{experiencia.Nombre}' (ID {experiencia.IdExperiencia}): {experiencia.Estado} → {nuevoEstado}",
                idExperiencia: experiencia.IdExperiencia);
        }

        private void Validar(BE.Experiencia e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            if (string.IsNullOrWhiteSpace(e.Nombre))
                throw new BE.AppException("err.bll.experiencia.nombre_requerido", "El nombre de la experiencia es obligatorio.");
            if (e.IdCategoria <= 0)
                throw new BE.AppException("err.bll.experiencia.categoria_requerida", "Debe seleccionar una categoría.");
            if (e.IdCiudad <= 0)
                throw new BE.AppException("err.bll.experiencia.ciudad_requerida", "Debe seleccionar una ciudad.");
            if (e.IdOrganizador <= 0)
                throw new BE.AppException("err.bll.experiencia.organizador_requerido", "Debe seleccionar un organizador.");
            if (e.CupoMaximo <= 0)
                throw new BE.AppException("err.bll.experiencia.cupo_invalido", "El cupo máximo debe ser mayor que cero.");
            if (e.DuracionMinutos <= 0)
                throw new BE.AppException("err.bll.experiencia.duracion_invalida", "La duración debe ser mayor que cero.");
            if (e.EdadMinima < 0)
                throw new BE.AppException("err.bll.experiencia.edad_invalida", "La edad mínima no puede ser negativa.");
        }
    }
}

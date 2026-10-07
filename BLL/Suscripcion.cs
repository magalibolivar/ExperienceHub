using System;

namespace BLL
{
    /// <summary>
    /// Lógica de negocio para las suscripciones de los clientes (alta, renovación, suspensión).
    /// </summary>
    public class Suscripcion
    {
        private readonly DAL.Suscripcion        dal         = new DAL.Suscripcion();
        private readonly DAL.PlanSuscripcion    dalPlan     = new DAL.PlanSuscripcion();
        private readonly Servicios.Bitacora        bitacora    = new Servicios.Bitacora();
        private readonly Servicios.BitacoraNegocio bitacoraNeg = new Servicios.BitacoraNegocio();

        public BE.Suscripcion ObtenerVigentePorCliente(int idCliente) => dal.ObtenerVigentePorCliente(idCliente);

        /// <summary>Todas las suscripciones (para el análisis de comportamiento, CU-ANA-06).</summary>
        public System.Collections.Generic.List<BE.Suscripcion> ObtenerTodas() => dal.ObtenerTodas();

        /// <summary>Suscripciones Activas que vencen dentro de los próximos días (para avisos).</summary>
        public System.Collections.Generic.List<BE.Suscripcion> ObtenerPorVencer(int dias = 7) => dal.ObtenerPorVencer(dias);

        /// <summary>Suscripciones vencidas (por estado o por fecha).</summary>
        public System.Collections.Generic.List<BE.Suscripcion> ObtenerVencidas() => dal.ObtenerVencidas();

        /// <summary>
        /// Tarea de SISTEMA (sin guarda de permiso, como BLL.Experiencia.ActualizarEstadosPorFecha):
        /// vence las suscripciones cuyo plazo pasó y reinicia el cupo mensual al cambiar de mes.
        /// Pensada para correr al arranque. Devuelve la cantidad de filas afectadas.
        /// </summary>
        public int ActualizarEstadosPorFecha() => dal.ActualizarVigenciaYConsumo();

        /// <summary>Da de alta la suscripción inicial de un cliente (al crearlo).</summary>
        public int Crear(int idCliente, int idPlan, DateTime? vencimiento)
        {
            if (dalPlan.ObtenerPorId(idPlan) == null)
                throw new BE.AppException("err.bll.suscripcion.plan_inexistente", "El plan seleccionado no existe.");
            var s = new BE.Suscripcion
            {
                IdCliente        = idCliente,
                IdPlan           = idPlan,
                FechaInicio      = DateTime.Today,
                FechaVencimiento = vencimiento,
                Estado           = BE.EstadoSuscripcion.Activa,
                ReservasConsumidasMes = 0
            };
            return dal.Alta(s);
        }

        /// <summary>Renueva la suscripción del cliente: reactiva, cambia plan/vencimiento y reinicia el consumo.</summary>
        public void Renovar(string modulo, int idCliente, int idPlan, DateTime? nuevoVencimiento)
        {
            PermisosAccion.Exigir(BE.Patentes.ClientesEditar, BE.Patentes.Clientes);
            if (dalPlan.ObtenerPorId(idPlan) == null)
                throw new BE.AppException("err.bll.suscripcion.plan_inexistente", "El plan seleccionado no existe.");

            var actual = dal.ObtenerVigentePorCliente(idCliente);
            if (actual == null)
                Crear(idCliente, idPlan, nuevoVencimiento);
            else
                dal.Renovar(actual.IdSuscripcion, idPlan, nuevoVencimiento);

            bitacora.Registrar(modulo, $"Renovar suscripción del cliente {idCliente} — plan {idPlan}", BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.RenovacionSuscripcion,
                $"Suscripción renovada — cliente {idCliente} — plan {idPlan}", idCliente: idCliente);
        }

        public void Suspender(string modulo, int idCliente)
        {
            PermisosAccion.Exigir(BE.Patentes.ClientesEditar, BE.Patentes.Clientes);
            var s = dal.ObtenerVigentePorCliente(idCliente)
                ?? throw new BE.AppException("err.bll.suscripcion.inexistente", "El cliente no tiene suscripción.");
            if (!s.PuedeSuspender())
                throw new BE.AppException("err.bll.suscripcion.no_suspendible",
                    "Solo se puede suspender una suscripción activa (esta está '{0}').", s.Estado);
            dal.CambiarEstado(s.IdSuscripcion, BE.EstadoSuscripcion.Suspendida);
            bitacora.Registrar(modulo, $"Suspender suscripción del cliente {idCliente}", BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.SuspensionSuscripcion,
                $"Suscripción suspendida — cliente {idCliente}", idCliente: idCliente);
        }

        public void Reactivar(string modulo, int idCliente)
        {
            PermisosAccion.Exigir(BE.Patentes.ClientesEditar, BE.Patentes.Clientes);
            var s = dal.ObtenerVigentePorCliente(idCliente)
                ?? throw new BE.AppException("err.bll.suscripcion.inexistente", "El cliente no tiene suscripción.");
            if (!s.PuedeReactivar())
                throw new BE.AppException("err.bll.suscripcion.no_reactivable",
                    "Solo se puede reactivar una suscripción suspendida (esta está '{0}'). Si venció, renovala.", s.Estado);
            dal.CambiarEstado(s.IdSuscripcion, BE.EstadoSuscripcion.Activa);
            bitacora.Registrar(modulo, $"Reactivar suscripción del cliente {idCliente}", BE.Criticidad.Media);
        }
    }
}

using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>
    /// Capa de Lógica de Negocio — Gestión de Planes de Suscripción.
    /// Valida datos antes de persistir y centraliza reglas de negocio.
    /// </summary>
    public class PlanSuscripcion : Interfaces.IPlanSuscripcionService
    {
        private readonly DAL.PlanSuscripcion dalPlan = new DAL.PlanSuscripcion();
        private readonly DAL.Suscripcion     dalSus  = new DAL.Suscripcion();

        public List<BE.PlanSuscripcion> ObtenerActivos() => dalPlan.ObtenerActivos();
        public List<BE.PlanSuscripcion> ObtenerTodos()   => dalPlan.ObtenerTodos();
        public BE.PlanSuscripcion ObtenerPorId(int idPlan) => dalPlan.ObtenerPorId(idPlan);

        public void Alta(BE.PlanSuscripcion plan)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanSuscripcionesEditar, BE.Patentes.PlanSuscripciones);
            Validar(plan);
            plan.Estado = true;
            dalPlan.Alta(plan);
        }

        public void Modificar(BE.PlanSuscripcion plan)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanSuscripcionesEditar, BE.Patentes.PlanSuscripciones);
            Validar(plan);
            dalPlan.Modificar(plan);
        }

        // Desactiva (baja lógica). Falla si hay suscripciones activas en ese plan.
        public void Desactivar(int idPlan)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanSuscripcionesEditar, BE.Patentes.PlanSuscripciones);
            int activas = dalSus.ContarActivasPorPlan(idPlan);
            if (activas > 0)
                throw new BE.AppException("err.bll.plan.tiene_clientes",
                    "No se puede desactivar el plan: tiene {0} suscripción(es) activa(s). " +
                    "Reasignalas a otro plan antes de desactivarlo.", activas);
            dalPlan.Desactivar(idPlan);
        }

        public void Activar(int idPlan)
        {
            PermisosAccion.Exigir(BE.Patentes.PlanSuscripcionesEditar, BE.Patentes.PlanSuscripciones);
            dalPlan.Activar(idPlan);
        }

        private void Validar(BE.PlanSuscripcion plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            if (string.IsNullOrWhiteSpace(plan.Nombre))
                throw new BE.AppException("err.bll.plan.nombre_requerido", "El nombre del plan es obligatorio.");
            foreach (char c in plan.Nombre)
                if (!char.IsLetterOrDigit(c) && c != ' ')
                    throw new BE.AppException("err.bll.plan.nombre_letras",
                        "El nombre del plan solo puede contener letras, números y espacios.");
            if (plan.ReservasMensuales <= 0)
                throw new BE.AppException("err.bll.plan.reservas_invalido", "Las reservas mensuales deben ser mayores que cero.");
            if (plan.AnticipacionMaximaDias <= 0)
                throw new BE.AppException("err.bll.plan.anticipacion_invalida", "La anticipación máxima debe ser mayor que cero.");
            if (plan.CantidadInvitados < 0)
                throw new BE.AppException("err.bll.plan.invitados_invalido", "La cantidad de invitados no puede ser negativa.");
            if (plan.Precio <= 0)
                throw new BE.AppException("err.bll.plan.precio_cero", "El precio debe ser mayor a cero.");
        }
    }
}

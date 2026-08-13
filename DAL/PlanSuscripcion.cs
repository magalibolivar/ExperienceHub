using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Capa de Acceso a Datos — PlanSuscripcion.
    /// Opera sobre la tabla [PlanSuscripcion].
    /// </summary>
    public class PlanSuscripcion
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        private const string COLS =
            "IdPlan, Nombre, Precio, Estado, ReservasMensuales, AnticipacionMaximaDias, " +
            "AccesoPremium, PrioridadListaEspera, CantidadInvitados";

        public List<BE.PlanSuscripcion> ObtenerActivos()
        {
            var lista = new List<BE.PlanSuscripcion>();
            try
            {
                DataTable tabla = acceso.Leer(
                    "SELECT " + COLS + " FROM PlanSuscripcion WHERE Estado = 1 ORDER BY Precio", null);
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener planes de suscripción.", ex); }
            return lista;
        }

        public List<BE.PlanSuscripcion> ObtenerTodos()
        {
            var lista = new List<BE.PlanSuscripcion>();
            try
            {
                DataTable tabla = acceso.Leer(
                    "SELECT " + COLS + " FROM PlanSuscripcion ORDER BY Precio", null);
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener planes de suscripción.", ex); }
            return lista;
        }

        public BE.PlanSuscripcion ObtenerPorId(int idPlan)
        {
            DataTable tabla = acceso.Leer(
                "SELECT " + COLS + " FROM PlanSuscripcion WHERE IdPlan = @IdPlan",
                new[] { new SqlParameter("@IdPlan", idPlan) });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        public void Alta(BE.PlanSuscripcion plan)
        {
            acceso.Escribir(
                "INSERT INTO PlanSuscripcion (Nombre, Precio, Estado, ReservasMensuales, " +
                "AnticipacionMaximaDias, AccesoPremium, PrioridadListaEspera, CantidadInvitados) " +
                "VALUES (@Nombre, @Precio, @Estado, @Reservas, @Anticipacion, @Premium, @Prioridad, @Invitados)",
                Params(plan, incluirId: false));
        }

        public void Modificar(BE.PlanSuscripcion plan)
        {
            acceso.Escribir(
                "UPDATE PlanSuscripcion SET Nombre=@Nombre, Precio=@Precio, Estado=@Estado, " +
                "ReservasMensuales=@Reservas, AnticipacionMaximaDias=@Anticipacion, AccesoPremium=@Premium, " +
                "PrioridadListaEspera=@Prioridad, CantidadInvitados=@Invitados WHERE IdPlan=@IdPlan",
                Params(plan, incluirId: true));
        }

        public void Desactivar(int idPlan)
        {
            acceso.Escribir("UPDATE PlanSuscripcion SET Estado=0 WHERE IdPlan=@IdPlan",
                new[] { new SqlParameter("@IdPlan", idPlan) });
        }

        public void Activar(int idPlan)
        {
            acceso.Escribir("UPDATE PlanSuscripcion SET Estado=1 WHERE IdPlan=@IdPlan",
                new[] { new SqlParameter("@IdPlan", idPlan) });
        }

        private static SqlParameter[] Params(BE.PlanSuscripcion plan, bool incluirId)
        {
            var p = new List<SqlParameter>
            {
                new SqlParameter("@Nombre", plan.Nombre),
                new SqlParameter("@Precio", plan.Precio),
                new SqlParameter("@Estado", plan.Estado ? 1 : 0),
                new SqlParameter("@Reservas", plan.ReservasMensuales),
                new SqlParameter("@Anticipacion", plan.AnticipacionMaximaDias),
                new SqlParameter("@Premium", plan.AccesoPremium ? 1 : 0),
                new SqlParameter("@Prioridad", plan.PrioridadListaEspera ? 1 : 0),
                new SqlParameter("@Invitados", plan.CantidadInvitados)
            };
            if (incluirId) p.Add(new SqlParameter("@IdPlan", plan.IdPlan));
            return p.ToArray();
        }

        private BE.PlanSuscripcion Mapear(DataRow row) => new BE.PlanSuscripcion
        {
            IdPlan                 = Convert.ToInt32(row["IdPlan"]),
            Nombre                 = row["Nombre"].ToString(),
            Precio                 = Convert.ToDecimal(row["Precio"]),
            Estado                 = Convert.ToBoolean(row["Estado"]),
            ReservasMensuales      = Convert.ToInt32(row["ReservasMensuales"]),
            AnticipacionMaximaDias = Convert.ToInt32(row["AnticipacionMaximaDias"]),
            AccesoPremium          = Convert.ToBoolean(row["AccesoPremium"]),
            PrioridadListaEspera   = Convert.ToBoolean(row["PrioridadListaEspera"]),
            CantidadInvitados      = Convert.ToInt32(row["CantidadInvitados"])
        };
    }
}

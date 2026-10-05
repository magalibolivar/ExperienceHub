using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Capa de Acceso a Datos — Contratacion (PN01). Opera sobre la tabla [Contratacion],
    /// con JOIN a [Cliente] y [PlanSuscripcion] para traer los nombres.
    /// El cobro y la formalización revalidan el estado en el WHERE (concurrencia optimista):
    /// si otra sesión de Caja cambió el estado, la operación afecta 0 filas y se rechaza.
    /// </summary>
    public class Contratacion
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        private const string SELECT_BASE =
            "SELECT ct.IdContratacion, ct.IdCliente, ct.IdPlan, ct.Importe, ct.Estado, ct.IntentosPago, " +
            "       ct.MedioPago, ct.FechaPago, ct.NumeroComprobante, ct.FechaComprobante, " +
            "       ct.IdSuscripcion, ct.FechaAlta, " +
            "       c.Nombre + ' ' + c.Apellido AS NombreCliente, p.Nombre AS NombrePlan " +
            "FROM Contratacion ct " +
            "LEFT JOIN Cliente        c ON c.IdCliente = ct.IdCliente " +
            "LEFT JOIN PlanSuscripcion p ON p.IdPlan   = ct.IdPlan ";

        public BE.Contratacion ObtenerPorId(int idContratacion)
        {
            DataTable t = acceso.Leer(SELECT_BASE + "WHERE ct.IdContratacion = @Id",
                new[] { new SqlParameter("@Id", idContratacion) });
            return t.Rows.Count == 0 ? null : Mapear(t.Rows[0]);
        }

        /// <summary>Cola de Caja: contrataciones pendientes de pago, más antiguas primero.</summary>
        public List<BE.Contratacion> ObtenerPendientes()
        {
            var lista = new List<BE.Contratacion>();
            DataTable t = acceso.Leer(
                SELECT_BASE + "WHERE ct.Estado = @Pend ORDER BY ct.FechaAlta",
                new[] { new SqlParameter("@Pend", (object)(int)BE.EstadoContratacion.PendienteDePago) });
            foreach (DataRow row in t.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>Contrataciones "en proceso" (Pendiente de pago o Pagada sin formalizar), más antiguas primero.</summary>
        public List<BE.Contratacion> ObtenerActivas()
        {
            var lista = new List<BE.Contratacion>();
            DataTable t = acceso.Leer(
                SELECT_BASE + "WHERE ct.Estado IN (@Pend, @Pag) ORDER BY ct.FechaAlta",
                new[]
                {
                    new SqlParameter("@Pend", (object)(int)BE.EstadoContratacion.PendienteDePago),
                    new SqlParameter("@Pag",  (object)(int)BE.EstadoContratacion.Pagada)
                });
            foreach (DataRow row in t.Rows) lista.Add(Mapear(row));
            return lista;
        }

        public int Alta(BE.Contratacion c)
        {
            DataTable t = acceso.Leer(
                "INSERT INTO Contratacion (IdCliente, IdPlan, Importe, Estado, IntentosPago, FechaAlta) " +
                "VALUES (@IdCliente, @IdPlan, @Importe, @Estado, 0, @FechaAlta); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT) AS IdNuevo",
                new[]
                {
                    new SqlParameter("@IdCliente", c.IdCliente),
                    new SqlParameter("@IdPlan", c.IdPlan),
                    new SqlParameter("@Importe", c.Importe),
                    new SqlParameter("@Estado", (object)(int)BE.EstadoContratacion.PendienteDePago),
                    new SqlParameter("@FechaAlta", c.FechaAlta)
                });
            return t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[0]["IdNuevo"]) : 0;
        }

        /// <summary>
        /// CU01-CAJ + CU02-CAJ (cobro + comprobante en un paso): marca Pagada, registra el medio,
        /// la fecha y el número de comprobante. Revalida PendienteDePago en el WHERE.
        /// </summary>
        public void RegistrarPago(int idContratacion, BE.MedioPago medio, string numeroComprobante, DateTime fecha)
        {
            int filas = acceso.Escribir(
                "UPDATE Contratacion SET Estado=@Pagada, MedioPago=@Medio, FechaPago=@Fecha, " +
                "NumeroComprobante=@Nro, FechaComprobante=@Fecha " +
                "WHERE IdContratacion=@Id AND Estado=@Pend",
                new[]
                {
                    new SqlParameter("@Pagada", (object)(int)BE.EstadoContratacion.Pagada),
                    new SqlParameter("@Medio", (object)(int)medio),
                    new SqlParameter("@Fecha", fecha),
                    new SqlParameter("@Nro", (object)numeroComprobante ?? DBNull.Value),
                    new SqlParameter("@Id", idContratacion),
                    new SqlParameter("@Pend", (object)(int)BE.EstadoContratacion.PendienteDePago)
                });
            if (filas == 0)
                throw new BE.AppException("err.dal.contratacion.no_pendiente",
                    "La contratación ya no está pendiente de pago (otra operación la modificó).");
        }

        /// <summary>
        /// Registra un intento de pago fallido: incrementa el contador y fija el estado resultante
        /// (PendienteDePago si quedan intentos, o Cancelada al agotarlos). Revalida pendiente.
        /// </summary>
        public void RegistrarIntentoFallido(int idContratacion, BE.EstadoContratacion estadoResultante)
        {
            int filas = acceso.Escribir(
                "UPDATE Contratacion SET IntentosPago = IntentosPago + 1, Estado=@Estado " +
                "WHERE IdContratacion=@Id AND Estado=@Pend",
                new[]
                {
                    new SqlParameter("@Estado", (object)(int)estadoResultante),
                    new SqlParameter("@Id", idContratacion),
                    new SqlParameter("@Pend", (object)(int)BE.EstadoContratacion.PendienteDePago)
                });
            if (filas == 0)
                throw new BE.AppException("err.dal.contratacion.no_pendiente",
                    "La contratación ya no está pendiente de pago (otra operación la modificó).");
        }

        /// <summary>
        /// Formaliza: crea la suscripción vigente y marca la contratación como Formalizada,
        /// vinculándola, todo en la MISMA transacción. Revalida que siga Pagada.
        /// Devuelve el Id de la suscripción generada.
        /// </summary>
        public int FormalizarConSuscripcion(int idContratacion, BE.Suscripcion s)
        {
            int idSus = 0;
            acceso.EjecutarTransaccion((conn, tx) =>
            {
                using (var ins = new SqlCommand(
                    "INSERT INTO Suscripcion (IdCliente, IdPlan, FechaInicio, FechaVencimiento, Estado, ReservasConsumidasMes) " +
                    "VALUES (@IdCliente, @IdPlan, @Inicio, @Vto, @Estado, 0); " +
                    "SELECT CAST(SCOPE_IDENTITY() AS INT)", conn, tx))
                {
                    ins.Parameters.AddWithValue("@IdCliente", s.IdCliente);
                    ins.Parameters.AddWithValue("@IdPlan", s.IdPlan);
                    ins.Parameters.AddWithValue("@Inicio", s.FechaInicio.Date);
                    ins.Parameters.AddWithValue("@Vto", (object)s.FechaVencimiento ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@Estado", (int)s.Estado);
                    idSus = (int)ins.ExecuteScalar();
                }

                using (var upd = new SqlCommand(
                    "UPDATE Contratacion SET Estado=@Form, IdSuscripcion=@IdSus " +
                    "WHERE IdContratacion=@Id AND Estado=@Pagada", conn, tx))
                {
                    upd.Parameters.AddWithValue("@Form", (int)BE.EstadoContratacion.Formalizada);
                    upd.Parameters.AddWithValue("@IdSus", idSus);
                    upd.Parameters.AddWithValue("@Id", idContratacion);
                    upd.Parameters.AddWithValue("@Pagada", (int)BE.EstadoContratacion.Pagada);
                    if (upd.ExecuteNonQuery() == 0)
                        throw new BE.AppException("err.dal.contratacion.no_pagada",
                            "La contratación ya no está en estado Pagada; no se puede formalizar.");
                }
            });
            return idSus;
        }

        private BE.Contratacion Mapear(DataRow row) => new BE.Contratacion
        {
            IdContratacion    = Convert.ToInt32(row["IdContratacion"]),
            IdCliente         = Convert.ToInt32(row["IdCliente"]),
            IdPlan            = Convert.ToInt32(row["IdPlan"]),
            Importe           = Convert.ToDecimal(row["Importe"]),
            Estado            = (BE.EstadoContratacion)Convert.ToInt32(row["Estado"]),
            IntentosPago      = Convert.ToInt32(row["IntentosPago"]),
            Medio             = row["MedioPago"] != DBNull.Value ? (BE.MedioPago?)Convert.ToInt32(row["MedioPago"]) : null,
            FechaPago         = row["FechaPago"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["FechaPago"]) : null,
            NumeroComprobante = row["NumeroComprobante"] != DBNull.Value ? row["NumeroComprobante"].ToString() : null,
            FechaComprobante  = row["FechaComprobante"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["FechaComprobante"]) : null,
            IdSuscripcion     = row["IdSuscripcion"] != DBNull.Value ? (int?)Convert.ToInt32(row["IdSuscripcion"]) : null,
            FechaAlta         = Convert.ToDateTime(row["FechaAlta"]),
            NombreCliente     = row["NombreCliente"] != DBNull.Value ? row["NombreCliente"].ToString() : null,
            NombrePlan        = row["NombrePlan"] != DBNull.Value ? row["NombrePlan"].ToString() : null
        };
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>Capa de Acceso a Datos — ListaEspera. Opera sobre la tabla [ListaEspera].</summary>
    public class ListaEspera
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        private const string SELECT_BASE =
            "SELECT le.IdListaEspera, le.IdExperiencia, le.IdCliente, le.Posicion, le.FechaIngreso, le.Estado, " +
            "       c.Nombre + ' ' + c.Apellido AS NombreCliente, e.Nombre AS NombreExperiencia " +
            "FROM ListaEspera le " +
            "LEFT JOIN Cliente     c ON c.IdCliente     = le.IdCliente " +
            "LEFT JOIN Experiencia e ON e.IdExperiencia = le.IdExperiencia ";

        /// <summary>Cola de espera de una experiencia (los que aún esperan), en orden FIFO.</summary>
        public List<BE.ListaEspera> ObtenerColaPorExperiencia(int idExperiencia)
        {
            var lista = new List<BE.ListaEspera>();
            DataTable tabla = acceso.Leer(
                SELECT_BASE + "WHERE le.IdExperiencia = @IdExp AND le.Estado = @Esperando ORDER BY le.Posicion",
                new[]
                {
                    new SqlParameter("@IdExp", idExperiencia),
                    new SqlParameter("@Esperando", (object)(int)BE.EstadoListaEspera.Esperando)
                });
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>Primer cliente en espera (el que recibe el cupo liberado). Null si la cola está vacía.</summary>
        public BE.ListaEspera ObtenerPrimeroEnEspera(int idExperiencia)
        {
            DataTable tabla = acceso.Leer(
                SELECT_BASE.Replace("SELECT le.", "SELECT TOP 1 le.") +
                "WHERE le.IdExperiencia = @IdExp AND le.Estado = @Esperando ORDER BY le.Posicion",
                new[]
                {
                    new SqlParameter("@IdExp", idExperiencia),
                    new SqlParameter("@Esperando", (object)(int)BE.EstadoListaEspera.Esperando)
                });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        /// <summary>Todas las entradas de una experiencia (cualquier estado), en orden de posición.</summary>
        public List<BE.ListaEspera> ObtenerPorExperiencia(int idExperiencia)
        {
            var lista = new List<BE.ListaEspera>();
            DataTable tabla = acceso.Leer(
                SELECT_BASE + "WHERE le.IdExperiencia = @IdExp ORDER BY le.Posicion",
                new[] { new SqlParameter("@IdExp", idExperiencia) });
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        public bool YaEstaEnCola(int idExperiencia, int idCliente)
        {
            var dt = acceso.Leer(
                "SELECT COUNT(*) AS Total FROM ListaEspera " +
                "WHERE IdExperiencia=@IdExp AND IdCliente=@IdCliente AND Estado=@Esperando",
                new[]
                {
                    new SqlParameter("@IdExp", idExperiencia),
                    new SqlParameter("@IdCliente", idCliente),
                    new SqlParameter("@Esperando", (object)(int)BE.EstadoListaEspera.Esperando)
                });
            return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Total"]) > 0;
        }

        /// <summary>Agrega un cliente al final de la cola de una experiencia. Devuelve la posición asignada.</summary>
        public int Agregar(int idExperiencia, int idCliente)
        {
            DataTable t = acceso.Leer(
                "DECLARE @Pos INT = (SELECT ISNULL(MAX(Posicion),0)+1 FROM ListaEspera WHERE IdExperiencia=@IdExp); " +
                "INSERT INTO ListaEspera (IdExperiencia, IdCliente, Posicion, FechaIngreso, Estado) " +
                "VALUES (@IdExp, @IdCliente, @Pos, GETDATE(), @Esperando); SELECT @Pos AS Posicion",
                new[]
                {
                    new SqlParameter("@IdExp", idExperiencia),
                    new SqlParameter("@IdCliente", idCliente),
                    new SqlParameter("@Esperando", (object)(int)BE.EstadoListaEspera.Esperando)
                });
            return t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[0]["Posicion"]) : 0;
        }

        public void CambiarEstado(int idListaEspera, BE.EstadoListaEspera estado)
        {
            acceso.Escribir("UPDATE ListaEspera SET Estado=@Estado WHERE IdListaEspera=@Id",
                new[] { new SqlParameter("@Estado", (int)estado), new SqlParameter("@Id", idListaEspera) });
        }

        private BE.ListaEspera Mapear(DataRow row) => new BE.ListaEspera
        {
            IdListaEspera     = Convert.ToInt32(row["IdListaEspera"]),
            IdExperiencia     = Convert.ToInt32(row["IdExperiencia"]),
            IdCliente         = Convert.ToInt32(row["IdCliente"]),
            Posicion          = Convert.ToInt32(row["Posicion"]),
            FechaIngreso      = Convert.ToDateTime(row["FechaIngreso"]),
            Estado            = (BE.EstadoListaEspera)Convert.ToInt32(row["Estado"]),
            NombreCliente     = row["NombreCliente"] != DBNull.Value ? row["NombreCliente"].ToString() : null,
            NombreExperiencia = row["NombreExperiencia"] != DBNull.Value ? row["NombreExperiencia"].ToString() : null
        };
    }
}

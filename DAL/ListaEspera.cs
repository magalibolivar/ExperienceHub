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
            "SELECT le.IdListaEspera, le.IdExperiencia, le.IdCliente, le.Posicion, le.FechaIngreso, le.Estado, le.FechaOferta, " +
            "       c.Nombre + ' ' + c.Apellido AS NombreCliente, e.Nombre AS NombreExperiencia, " +
            "       (CAST(e.Fecha AS DATETIME) + CAST(e.HoraInicio AS DATETIME)) AS FechaHoraExperiencia " +
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

        /// <summary>Oferta vigente de una experiencia (entrada en estado Ofrecido). Null si no hay.</summary>
        public BE.ListaEspera ObtenerOfertaVigente(int idExperiencia)
        {
            DataTable tabla = acceso.Leer(
                SELECT_BASE.Replace("SELECT le.", "SELECT TOP 1 le.") +
                "WHERE le.IdExperiencia = @IdExp AND le.Estado = @Ofrecido ORDER BY le.Posicion",
                new[]
                {
                    new SqlParameter("@IdExp", idExperiencia),
                    new SqlParameter("@Ofrecido", (object)(int)BE.EstadoListaEspera.Ofrecido)
                });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        /// <summary>Ofrece el cupo a una entrada: la marca Ofrecido y sella la fecha de la oferta.</summary>
        public void Ofrecer(int idListaEspera, DateTime fechaOferta)
        {
            acceso.Escribir(
                "UPDATE ListaEspera SET Estado=@Ofrecido, FechaOferta=@Fecha WHERE IdListaEspera=@Id",
                new[]
                {
                    new SqlParameter("@Ofrecido", (object)(int)BE.EstadoListaEspera.Ofrecido),
                    new SqlParameter("@Fecha", fechaOferta),
                    new SqlParameter("@Id", idListaEspera)
                });
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

        /// <summary>
        /// IdExperiencia de todas las experiencias en las que el cliente está en la cola (Esperando u
        /// Ofrecido). Carga por lotes para evaluar compatibilidad sin un COUNT por experiencia.
        /// </summary>
        public System.Collections.Generic.HashSet<int> ObtenerIdExperienciasEnCola(int idCliente)
        {
            var set = new System.Collections.Generic.HashSet<int>();
            var dt = acceso.Leer(
                "SELECT DISTINCT IdExperiencia FROM ListaEspera WHERE IdCliente=@Id AND Estado IN (@Esp,@Ofr)",
                new[]
                {
                    new SqlParameter("@Id", idCliente),
                    new SqlParameter("@Esp", (object)(int)BE.EstadoListaEspera.Esperando),
                    new SqlParameter("@Ofr", (object)(int)BE.EstadoListaEspera.Ofrecido)
                });
            foreach (DataRow row in dt.Rows) set.Add(Convert.ToInt32(row["IdExperiencia"]));
            return set;
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

        /// <summary>
        /// Todas las ofertas vigentes (entradas en estado Ofrecido), de cualquier experiencia, ordenadas
        /// por fecha de oferta. SELECT de solo lectura — insumo de la vista orientada a tareas (PN02).
        /// </summary>
        public List<BE.ListaEspera> ObtenerOfertasVigentes()
        {
            var lista = new List<BE.ListaEspera>();
            DataTable t = acceso.Leer(
                SELECT_BASE + "WHERE le.Estado = @Ofrecido ORDER BY le.FechaOferta",
                new[] { new SqlParameter("@Ofrecido", (object)(int)BE.EstadoListaEspera.Ofrecido) });
            foreach (DataRow row in t.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>
        /// Resumen de actividad por experiencia: por cada experiencia con entradas Esperando u Ofrecido,
        /// cuántas hay de cada tipo. SELECT agregado de solo lectura (no consulta por experiencia).
        /// </summary>
        public List<BE.ResumenListaEspera> ObtenerResumenActivo()
        {
            var lista = new List<BE.ResumenListaEspera>();
            DataTable t = acceso.Leer(
                "SELECT le.IdExperiencia, e.Nombre AS NombreExperiencia, " +
                "       (CAST(e.Fecha AS DATETIME) + CAST(e.HoraInicio AS DATETIME)) AS FechaHoraExperiencia, " +
                "       SUM(CASE WHEN le.Estado = @Esp THEN 1 ELSE 0 END) AS EnEspera, " +
                "       SUM(CASE WHEN le.Estado = @Ofr THEN 1 ELSE 0 END) AS Ofrecidas " +
                "FROM ListaEspera le " +
                "JOIN Experiencia e ON e.IdExperiencia = le.IdExperiencia " +
                "WHERE le.Estado IN (@Esp, @Ofr) " +
                "GROUP BY le.IdExperiencia, e.Nombre, e.Fecha, e.HoraInicio " +
                "ORDER BY FechaHoraExperiencia",
                new[]
                {
                    new SqlParameter("@Esp", (object)(int)BE.EstadoListaEspera.Esperando),
                    new SqlParameter("@Ofr", (object)(int)BE.EstadoListaEspera.Ofrecido)
                });
            foreach (DataRow row in t.Rows)
                lista.Add(new BE.ResumenListaEspera
                {
                    IdExperiencia        = Convert.ToInt32(row["IdExperiencia"]),
                    NombreExperiencia    = row["NombreExperiencia"] != DBNull.Value ? row["NombreExperiencia"].ToString() : null,
                    FechaHoraExperiencia = row["FechaHoraExperiencia"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["FechaHoraExperiencia"]) : null,
                    EnEspera             = Convert.ToInt32(row["EnEspera"]),
                    Ofrecidas            = Convert.ToInt32(row["Ofrecidas"])
                });
            return lista;
        }

        /// <summary>IdExperiencia de todas las experiencias que hoy tienen una oferta vigente (Ofrecido).
        /// Insumo para procesar vencimientos de forma global (p. ej. al iniciar sesión).</summary>
        public List<int> ObtenerExperienciasConOfertaVigente()
        {
            var ids = new List<int>();
            DataTable t = acceso.Leer(
                "SELECT DISTINCT IdExperiencia FROM ListaEspera WHERE Estado=@Ofrecido",
                new[] { new SqlParameter("@Ofrecido", (object)(int)BE.EstadoListaEspera.Ofrecido) });
            foreach (DataRow row in t.Rows) ids.Add(Convert.ToInt32(row["IdExperiencia"]));
            return ids;
        }

        /// <summary>Cantidad de entradas activas de una experiencia (Esperando u Ofrecido): clientes que
        /// se verían afectados si se cierra su lista de espera (preview de la cancelación de experiencia).</summary>
        public int ContarActivas(int idExperiencia)
        {
            var dt = acceso.Leer(
                "SELECT COUNT(*) AS Total FROM ListaEspera WHERE IdExperiencia=@IdExp AND Estado IN (@Esp,@Ofr)",
                new[]
                {
                    new SqlParameter("@IdExp", idExperiencia),
                    new SqlParameter("@Esp", (object)(int)BE.EstadoListaEspera.Esperando),
                    new SqlParameter("@Ofr", (object)(int)BE.EstadoListaEspera.Ofrecido)
                });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Total"]) : 0;
        }

        private BE.ListaEspera Mapear(DataRow row) => new BE.ListaEspera
        {
            IdListaEspera     = Convert.ToInt32(row["IdListaEspera"]),
            IdExperiencia     = Convert.ToInt32(row["IdExperiencia"]),
            IdCliente         = Convert.ToInt32(row["IdCliente"]),
            Posicion          = Convert.ToInt32(row["Posicion"]),
            FechaIngreso      = Convert.ToDateTime(row["FechaIngreso"]),
            Estado            = (BE.EstadoListaEspera)Convert.ToInt32(row["Estado"]),
            FechaOferta       = row.Table.Columns.Contains("FechaOferta") && row["FechaOferta"] != DBNull.Value
                                    ? (DateTime?)Convert.ToDateTime(row["FechaOferta"]) : null,
            NombreCliente     = row["NombreCliente"] != DBNull.Value ? row["NombreCliente"].ToString() : null,
            NombreExperiencia = row["NombreExperiencia"] != DBNull.Value ? row["NombreExperiencia"].ToString() : null,
            FechaHoraExperiencia = row.Table.Columns.Contains("FechaHoraExperiencia") && row["FechaHoraExperiencia"] != DBNull.Value
                                    ? (DateTime?)Convert.ToDateTime(row["FechaHoraExperiencia"]) : null
        };
    }
}

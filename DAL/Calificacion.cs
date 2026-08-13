using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>Capa de Acceso a Datos — Calificacion. Opera sobre la tabla [Calificacion].</summary>
    public class Calificacion
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        public int Alta(BE.Calificacion c)
        {
            DataTable t = acceso.Leer(
                "INSERT INTO Calificacion (IdReserva, IdCliente, IdExperiencia, Puntaje, Comentario, Fecha) " +
                "VALUES (@IdReserva, @IdCliente, @IdExp, @Puntaje, @Comentario, @Fecha); " +
                "SELECT SCOPE_IDENTITY() AS IdNuevo",
                new[]
                {
                    new SqlParameter("@IdReserva", c.IdReserva),
                    new SqlParameter("@IdCliente", c.IdCliente),
                    new SqlParameter("@IdExp", c.IdExperiencia),
                    new SqlParameter("@Puntaje", c.Puntaje),
                    new SqlParameter("@Comentario", (object)c.Comentario ?? DBNull.Value),
                    new SqlParameter("@Fecha", c.Fecha)
                });
            return t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[0]["IdNuevo"]) : 0;
        }

        public bool ExistePorReserva(int idReserva)
        {
            var dt = acceso.Leer("SELECT COUNT(*) AS Total FROM Calificacion WHERE IdReserva=@Id",
                new[] { new SqlParameter("@Id", idReserva) });
            return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Total"]) > 0;
        }

        /// <summary>Mapa IdReserva → Puntaje de todas las calificaciones (para mostrar en listados).</summary>
        public Dictionary<int, int> ObtenerPuntajesPorReserva()
        {
            var mapa = new Dictionary<int, int>();
            DataTable dt = acceso.Leer("SELECT IdReserva, Puntaje FROM Calificacion", new SqlParameter[0]);
            foreach (DataRow row in dt.Rows)
                mapa[Convert.ToInt32(row["IdReserva"])] = Convert.ToInt32(row["Puntaje"]);
            return mapa;
        }

        public List<BE.Calificacion> ObtenerPorExperiencia(int idExperiencia)
        {
            var lista = new List<BE.Calificacion>();
            DataTable tabla = acceso.Leer(
                "SELECT ca.IdCalificacion, ca.IdReserva, ca.IdCliente, ca.IdExperiencia, ca.Puntaje, ca.Comentario, ca.Fecha, " +
                "       c.Nombre + ' ' + c.Apellido AS NombreCliente, e.Nombre AS NombreExperiencia " +
                "FROM Calificacion ca " +
                "LEFT JOIN Cliente     c ON c.IdCliente     = ca.IdCliente " +
                "LEFT JOIN Experiencia e ON e.IdExperiencia = ca.IdExperiencia " +
                "WHERE ca.IdExperiencia = @IdExp ORDER BY ca.Fecha DESC",
                new[] { new SqlParameter("@IdExp", idExperiencia) });
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>Promedio de puntaje de una experiencia (0 si no tiene calificaciones).</summary>
        public double PromedioPorExperiencia(int idExperiencia)
        {
            var dt = acceso.Leer(
                "SELECT ISNULL(AVG(CAST(Puntaje AS FLOAT)), 0) AS Promedio FROM Calificacion WHERE IdExperiencia=@Id",
                new[] { new SqlParameter("@Id", idExperiencia) });
            return dt.Rows.Count > 0 ? Convert.ToDouble(dt.Rows[0]["Promedio"]) : 0;
        }

        private BE.Calificacion Mapear(DataRow row) => new BE.Calificacion
        {
            IdCalificacion    = Convert.ToInt32(row["IdCalificacion"]),
            IdReserva         = Convert.ToInt32(row["IdReserva"]),
            IdCliente         = Convert.ToInt32(row["IdCliente"]),
            IdExperiencia     = Convert.ToInt32(row["IdExperiencia"]),
            Puntaje           = Convert.ToInt32(row["Puntaje"]),
            Comentario        = row["Comentario"] != DBNull.Value ? row["Comentario"].ToString() : null,
            Fecha             = Convert.ToDateTime(row["Fecha"]),
            NombreCliente     = row["NombreCliente"] != DBNull.Value ? row["NombreCliente"].ToString() : null,
            NombreExperiencia = row["NombreExperiencia"] != DBNull.Value ? row["NombreExperiencia"].ToString() : null
        };
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>Capa de Acceso a Datos — Categoria. Opera sobre la tabla [Categoria].</summary>
    public class Categoria
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        public List<BE.Categoria> ObtenerActivas() => Obtener("WHERE Estado = 1");
        public List<BE.Categoria> ObtenerTodos()   => Obtener("");

        private List<BE.Categoria> Obtener(string filtro)
        {
            var lista = new List<BE.Categoria>();
            try
            {
                DataTable tabla = acceso.Leer(
                    "SELECT IdCategoria, Nombre, Descripcion, Estado FROM Categoria " +
                    filtro + " ORDER BY Nombre", null);
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener categorías.", ex); }
            return lista;
        }

        public BE.Categoria ObtenerPorId(int id)
        {
            DataTable tabla = acceso.Leer(
                "SELECT IdCategoria, Nombre, Descripcion, Estado FROM Categoria WHERE IdCategoria = @Id",
                new[] { new SqlParameter("@Id", id) });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        public int Alta(BE.Categoria c)
        {
            DataTable t = acceso.Leer(
                "INSERT INTO Categoria (Nombre, Descripcion, Estado) VALUES (@Nombre, @Descripcion, @Estado); " +
                "SELECT SCOPE_IDENTITY() AS IdNuevo",
                new[]
                {
                    new SqlParameter("@Nombre", c.Nombre),
                    new SqlParameter("@Descripcion", (object)c.Descripcion ?? DBNull.Value),
                    new SqlParameter("@Estado", c.Estado ? 1 : 0)
                });
            return t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[0]["IdNuevo"]) : 0;
        }

        public void Modificar(BE.Categoria c)
        {
            acceso.Escribir(
                "UPDATE Categoria SET Nombre=@Nombre, Descripcion=@Descripcion, Estado=@Estado WHERE IdCategoria=@Id",
                new[]
                {
                    new SqlParameter("@Nombre", c.Nombre),
                    new SqlParameter("@Descripcion", (object)c.Descripcion ?? DBNull.Value),
                    new SqlParameter("@Estado", c.Estado ? 1 : 0),
                    new SqlParameter("@Id", c.IdCategoria)
                });
        }

        public void CambiarEstado(int idCategoria, bool activo)
        {
            acceso.Escribir("UPDATE Categoria SET Estado=@Estado WHERE IdCategoria=@Id",
                new[] { new SqlParameter("@Estado", activo ? 1 : 0), new SqlParameter("@Id", idCategoria) });
        }

        public int ContarExperienciasEnCategoria(int idCategoria)
        {
            var dt = acceso.Leer("SELECT COUNT(*) AS Total FROM Experiencia WHERE IdCategoria=@Id",
                new[] { new SqlParameter("@Id", idCategoria) });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Total"]) : 0;
        }

        private BE.Categoria Mapear(DataRow row) => new BE.Categoria
        {
            IdCategoria = Convert.ToInt32(row["IdCategoria"]),
            Nombre      = row["Nombre"].ToString(),
            Descripcion = row["Descripcion"] != DBNull.Value ? row["Descripcion"].ToString() : null,
            Estado      = Convert.ToBoolean(row["Estado"])
        };
    }
}

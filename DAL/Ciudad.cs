using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>Capa de Acceso a Datos — Ciudad. Opera sobre la tabla [Ciudad].</summary>
    public class Ciudad
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        public List<BE.Ciudad> ObtenerActivas() => Obtener("WHERE Estado = 1");
        public List<BE.Ciudad> ObtenerTodos()   => Obtener("");

        private List<BE.Ciudad> Obtener(string filtro)
        {
            var lista = new List<BE.Ciudad>();
            try
            {
                DataTable tabla = acceso.Leer(
                    "SELECT IdCiudad, Nombre, Provincia, Estado FROM Ciudad " +
                    filtro + " ORDER BY Nombre", null);
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener ciudades.", ex); }
            return lista;
        }

        public BE.Ciudad ObtenerPorId(int idCiudad)
        {
            DataTable tabla = acceso.Leer(
                "SELECT IdCiudad, Nombre, Provincia, Estado FROM Ciudad WHERE IdCiudad = @Id",
                new[] { new SqlParameter("@Id", idCiudad) });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        public int Alta(BE.Ciudad c)
        {
            DataTable t = acceso.Leer(
                "INSERT INTO Ciudad (Nombre, Provincia, Estado) VALUES (@Nombre, @Provincia, @Estado); " +
                "SELECT SCOPE_IDENTITY() AS IdNuevo",
                new[]
                {
                    new SqlParameter("@Nombre", c.Nombre),
                    new SqlParameter("@Provincia", (object)c.Provincia ?? DBNull.Value),
                    new SqlParameter("@Estado", c.Estado ? 1 : 0)
                });
            return t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[0]["IdNuevo"]) : 0;
        }

        public void Modificar(BE.Ciudad c)
        {
            acceso.Escribir(
                "UPDATE Ciudad SET Nombre=@Nombre, Provincia=@Provincia, Estado=@Estado WHERE IdCiudad=@Id",
                new[]
                {
                    new SqlParameter("@Nombre", c.Nombre),
                    new SqlParameter("@Provincia", (object)c.Provincia ?? DBNull.Value),
                    new SqlParameter("@Estado", c.Estado ? 1 : 0),
                    new SqlParameter("@Id", c.IdCiudad)
                });
        }

        public void CambiarEstado(int idCiudad, bool activo)
        {
            acceso.Escribir("UPDATE Ciudad SET Estado=@Estado WHERE IdCiudad=@Id",
                new[] { new SqlParameter("@Estado", activo ? 1 : 0), new SqlParameter("@Id", idCiudad) });
        }

        public int ContarExperienciasEnCiudad(int idCiudad)
        {
            var dt = acceso.Leer("SELECT COUNT(*) AS Total FROM Experiencia WHERE IdCiudad=@Id",
                new[] { new SqlParameter("@Id", idCiudad) });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Total"]) : 0;
        }

        private BE.Ciudad Mapear(DataRow row) => new BE.Ciudad
        {
            IdCiudad  = Convert.ToInt32(row["IdCiudad"]),
            Nombre    = row["Nombre"].ToString(),
            Provincia = row["Provincia"] != DBNull.Value ? row["Provincia"].ToString() : null,
            Estado    = Convert.ToBoolean(row["Estado"])
        };
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Capa de Acceso a Datos — Interes y la relación N:M [ClienteInteres].
    /// </summary>
    public class Interes
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        public List<BE.Interes> ObtenerTodos()
        {
            var lista = new List<BE.Interes>();
            DataTable tabla = acceso.Leer("SELECT IdInteres, Nombre FROM Interes ORDER BY Nombre", null);
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>Intereses asociados a un cliente.</summary>
        public List<BE.Interes> ObtenerPorCliente(int idCliente)
        {
            var lista = new List<BE.Interes>();
            DataTable tabla = acceso.Leer(
                "SELECT i.IdInteres, i.Nombre FROM Interes i " +
                "INNER JOIN ClienteInteres ci ON ci.IdInteres = i.IdInteres " +
                "WHERE ci.IdCliente = @IdCliente ORDER BY i.Nombre",
                new[] { new SqlParameter("@IdCliente", idCliente) });
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        /// <summary>Reemplaza el set de intereses de un cliente (borra y reinserta) en una transacción.</summary>
        public void GuardarInteresesCliente(int idCliente, IEnumerable<int> idsIntereses)
        {
            acceso.EjecutarTransaccion((conn, tx) =>
            {
                using (var del = new SqlCommand("DELETE FROM ClienteInteres WHERE IdCliente=@IdCliente", conn, tx))
                {
                    del.Parameters.AddWithValue("@IdCliente", idCliente);
                    del.ExecuteNonQuery();
                }
                if (idsIntereses == null) return;
                foreach (int idInteres in idsIntereses)
                    using (var ins = new SqlCommand(
                        "INSERT INTO ClienteInteres (IdCliente, IdInteres) VALUES (@IdCliente, @IdInteres)", conn, tx))
                    {
                        ins.Parameters.AddWithValue("@IdCliente", idCliente);
                        ins.Parameters.AddWithValue("@IdInteres", idInteres);
                        ins.ExecuteNonQuery();
                    }
            });
        }

        private BE.Interes Mapear(DataRow row) => new BE.Interes
        {
            IdInteres = Convert.ToInt32(row["IdInteres"]),
            Nombre    = row["Nombre"].ToString()
        };
    }
}

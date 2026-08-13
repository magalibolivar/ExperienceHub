using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>Capa de Acceso a Datos — Organizador. Opera sobre la tabla [Organizador].</summary>
    public class Organizador
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        // T07 — Dígito Verificador de esta tabla (fuente única).
        public const  string   DV_Tabla    = "Organizador";
        public const  string   DV_Pk       = "IdOrganizador";
        public static readonly string[] DV_Columnas = { "Nombre", "Mail", "Telefono", "Estado" };

        private void RecalcularDV()
        {
            try { new DigitoVerificador().RecalcularTabla(DV_Tabla, DV_Pk, DV_Columnas); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("[DAL.Organizador.RecalcularDV] " + ex.Message); }
        }

        public List<BE.Organizador> ObtenerActivos() => Obtener("WHERE Estado = 1");
        public List<BE.Organizador> ObtenerTodos()   => Obtener("");

        private List<BE.Organizador> Obtener(string filtro)
        {
            var lista = new List<BE.Organizador>();
            try
            {
                DataTable tabla = acceso.Leer(
                    "SELECT IdOrganizador, Nombre, Contacto, Telefono, Mail, Estado FROM Organizador " +
                    filtro + " ORDER BY Nombre", null);
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener organizadores.", ex); }
            return lista;
        }

        public BE.Organizador ObtenerPorId(int id)
        {
            DataTable tabla = acceso.Leer(
                "SELECT IdOrganizador, Nombre, Contacto, Telefono, Mail, Estado FROM Organizador WHERE IdOrganizador = @Id",
                new[] { new SqlParameter("@Id", id) });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        public int Alta(BE.Organizador o)
        {
            DataTable t = acceso.Leer(
                "INSERT INTO Organizador (Nombre, Contacto, Telefono, Mail, Estado) " +
                "VALUES (@Nombre, @Contacto, @Telefono, @Mail, @Estado); SELECT SCOPE_IDENTITY() AS IdNuevo",
                Params(o));
            int id = t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[0]["IdNuevo"]) : 0;
            RecalcularDV();
            return id;
        }

        public void Modificar(BE.Organizador o)
        {
            var p = new List<SqlParameter>(Params(o)) { new SqlParameter("@Id", o.IdOrganizador) };
            acceso.Escribir(
                "UPDATE Organizador SET Nombre=@Nombre, Contacto=@Contacto, Telefono=@Telefono, " +
                "Mail=@Mail, Estado=@Estado WHERE IdOrganizador=@Id",
                p.ToArray());
            RecalcularDV();
        }

        public void CambiarEstado(int idOrganizador, bool activo)
        {
            acceso.Escribir("UPDATE Organizador SET Estado=@Estado WHERE IdOrganizador=@Id",
                new[] { new SqlParameter("@Estado", activo ? 1 : 0), new SqlParameter("@Id", idOrganizador) });
            RecalcularDV();
        }

        public int ContarExperienciasDelOrganizador(int idOrganizador)
        {
            var dt = acceso.Leer("SELECT COUNT(*) AS Total FROM Experiencia WHERE IdOrganizador=@Id",
                new[] { new SqlParameter("@Id", idOrganizador) });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Total"]) : 0;
        }

        private static SqlParameter[] Params(BE.Organizador o) => new[]
        {
            new SqlParameter("@Nombre", o.Nombre),
            new SqlParameter("@Contacto", (object)o.Contacto ?? DBNull.Value),
            new SqlParameter("@Telefono", (object)o.Telefono ?? DBNull.Value),
            new SqlParameter("@Mail", (object)o.Mail ?? DBNull.Value),
            new SqlParameter("@Estado", o.Estado ? 1 : 0)
        };

        private BE.Organizador Mapear(DataRow row) => new BE.Organizador
        {
            IdOrganizador = Convert.ToInt32(row["IdOrganizador"]),
            Nombre        = row["Nombre"].ToString(),
            Contacto      = row["Contacto"] != DBNull.Value ? row["Contacto"].ToString() : null,
            Telefono      = row["Telefono"] != DBNull.Value ? row["Telefono"].ToString() : null,
            Mail          = row["Mail"]     != DBNull.Value ? row["Mail"].ToString()     : null,
            Estado        = Convert.ToBoolean(row["Estado"])
        };
    }
}

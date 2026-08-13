using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>Acceso a datos de la tabla [Cliente].</summary>
    public class Cliente : BaseDAL<BE.Cliente>, Interfaces.IClienteDAL
    {
        // T07 — Dígito Verificador de esta tabla (fuente única).
        public const  string   DV_Tabla    = "Cliente";
        public const  string   DV_Pk       = "IdCliente";
        public static readonly string[] DV_Columnas = { "Nombre", "Apellido", "DNI", "Email", "IdCiudad" };

        private readonly Interes dalInteres = new Interes();

        private void RecalcularDV()
        {
            try { new DigitoVerificador().RecalcularTabla(DV_Tabla, DV_Pk, DV_Columnas); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("[DAL.Cliente.RecalcularDV] " + ex.Message); }
        }

        // SELECT con nombre de ciudad y la suscripción más reciente (OUTER APPLY) + su plan.
        private const string SELECT_BASE =
            "SELECT c.IdCliente, c.Nombre, c.Apellido, c.DNI, c.Email, c.IdCiudad, " +
            "       c.FechaAlta, c.FechaNacimiento, ciu.Nombre AS NombreCiudad, " +
            "       sus.IdSuscripcion, sus.IdPlan, sus.FechaInicio, sus.FechaVencimiento, " +
            "       sus.EstadoSus, sus.ReservasConsumidasMes, sus.NombrePlan, " +
            "       sus.ReservasMensuales, sus.AnticipacionMaximaDias, sus.AccesoPremium " +
            "FROM Cliente c " +
            "LEFT JOIN Ciudad ciu ON ciu.IdCiudad = c.IdCiudad " +
            "OUTER APPLY ( " +
            "   SELECT TOP 1 s.IdSuscripcion, s.IdPlan, s.FechaInicio, s.FechaVencimiento, " +
            "          s.Estado AS EstadoSus, s.ReservasConsumidasMes, " +
            "          p.Nombre AS NombrePlan, p.ReservasMensuales, p.AnticipacionMaximaDias, p.AccesoPremium " +
            "   FROM Suscripcion s INNER JOIN PlanSuscripcion p ON p.IdPlan = s.IdPlan " +
            "   WHERE s.IdCliente = c.IdCliente ORDER BY s.FechaInicio DESC ) sus ";

        public override List<BE.Cliente> ObtenerTodos()
        {
            var lista = new List<BE.Cliente>();
            try
            {
                DataTable tabla = acceso.Leer(
                    SELECT_BASE + "WHERE c.Activo = 1 ORDER BY c.Apellido, c.Nombre", null);
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener la lista de clientes.", ex); }
            return lista;
        }

        public override BE.Cliente ObtenerPorId(int idCliente)
        {
            try
            {
                DataTable tabla = acceso.Leer(
                    SELECT_BASE + "WHERE c.IdCliente = @IdCliente AND c.Activo = 1",
                    new[] { new SqlParameter("@IdCliente", idCliente) });
                if (tabla.Rows.Count == 0) return null;
                var cliente = Mapear(tabla.Rows[0]);
                cliente.Intereses = dalInteres.ObtenerPorCliente(idCliente);  // detalle: cargar intereses
                return cliente;
            }
            catch (Exception ex) { throw new Exception("Error al obtener el cliente.", ex); }
        }

        // Verifica si ya existe un cliente activo con ese DNI (DNI cifrado → compara en memoria).
        public bool ExisteDNI(string dni) => BuscarIdPorDni(dni, -1) != 0;
        public bool ExisteDNIParaOtro(string dni, int idExcluir) => BuscarIdPorDni(dni, idExcluir) != 0;

        private int BuscarIdPorDni(string dni, int idExcluir)
        {
            DataTable tabla = acceso.Leer("SELECT IdCliente, DNI FROM Cliente WHERE Activo = 1", null);
            if (tabla == null) return 0;
            foreach (DataRow row in tabla.Rows)
            {
                int id = Convert.ToInt32(row["IdCliente"]);
                if (id == idExcluir) continue;
                string dniGuardado = Seguridad.Encriptador.TryDesencriptar(row["DNI"].ToString());
                if (string.Equals(dniGuardado, dni, StringComparison.Ordinal)) return id;
            }
            return 0;
        }

        public int Alta(BE.Cliente cliente)
        {
            // MetodoPago quedó como columna legacy NOT NULL; se la satisface con 'N/A'
            // (el concepto de método de pago no aplica en ExperienceHub).
            DataTable tabla = acceso.Leer(
                "INSERT INTO Cliente (Nombre, Apellido, DNI, Email, MetodoPago, IdCiudad, FechaAlta, FechaNacimiento, Activo) " +
                "VALUES (@Nombre, @Apellido, @DNI, @Email, 'N/A', @IdCiudad, @FechaAlta, @FechaNacimiento, 1); " +
                "SELECT SCOPE_IDENTITY() AS IdNuevo",
                Params(cliente, incluirId: false));

            int idNuevo = tabla.Rows.Count > 0 ? Convert.ToInt32(tabla.Rows[0]["IdNuevo"]) : 0;
            RecalcularDV();
            return idNuevo;
        }

        public void Modificar(BE.Cliente cliente)
        {
            acceso.Escribir(
                "UPDATE Cliente SET Nombre=@Nombre, Apellido=@Apellido, DNI=@DNI, Email=@Email, " +
                "IdCiudad=@IdCiudad, FechaNacimiento=@FechaNacimiento WHERE IdCliente=@IdCliente",
                Params(cliente, incluirId: true));
            RecalcularDV();
        }

        public void Baja(int idCliente)
        {
            acceso.Escribir("UPDATE Cliente SET Activo = 0 WHERE IdCliente = @IdCliente",
                new[] { new SqlParameter("@IdCliente", idCliente) });
            RecalcularDV();
        }

        private static SqlParameter[] Params(BE.Cliente c, bool incluirId)
        {
            var p = new List<SqlParameter>
            {
                new SqlParameter("@Nombre",          c.Nombre),
                new SqlParameter("@Apellido",        c.Apellido),
                new SqlParameter("@DNI",             Seguridad.Encriptador.Encriptar(c.DNI)),
                new SqlParameter("@Email",           (object)c.Email ?? DBNull.Value),
                new SqlParameter("@IdCiudad",        (object)c.IdCiudad ?? DBNull.Value),
                new SqlParameter("@FechaNacimiento", (object)c.FechaNacimiento ?? DBNull.Value)
            };
            if (!incluirId) p.Add(new SqlParameter("@FechaAlta", c.FechaAlta));
            else            p.Add(new SqlParameter("@IdCliente", c.IdCliente));
            return p.ToArray();
        }

        private BE.Cliente Mapear(DataRow row)
        {
            var cliente = new BE.Cliente
            {
                IdCliente       = Convert.ToInt32(row["IdCliente"]),
                Nombre          = row["Nombre"].ToString(),
                Apellido        = row["Apellido"].ToString(),
                DNI             = Seguridad.Encriptador.TryDesencriptar(row["DNI"].ToString()),
                Email           = row["Email"] != DBNull.Value ? row["Email"].ToString() : null,
                IdCiudad        = row["IdCiudad"] != DBNull.Value ? (int?)Convert.ToInt32(row["IdCiudad"]) : null,
                NombreCiudad    = row["NombreCiudad"] != DBNull.Value ? row["NombreCiudad"].ToString() : null,
                FechaAlta       = Convert.ToDateTime(row["FechaAlta"]),
                FechaNacimiento = row["FechaNacimiento"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["FechaNacimiento"]) : null
            };

            if (row["IdSuscripcion"] != DBNull.Value)
                cliente.Suscripcion = new BE.Suscripcion
                {
                    IdSuscripcion          = Convert.ToInt32(row["IdSuscripcion"]),
                    IdCliente              = cliente.IdCliente,
                    IdPlan                 = Convert.ToInt32(row["IdPlan"]),
                    FechaInicio            = Convert.ToDateTime(row["FechaInicio"]),
                    FechaVencimiento       = row["FechaVencimiento"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["FechaVencimiento"]) : null,
                    Estado                 = (BE.EstadoSuscripcion)Convert.ToInt32(row["EstadoSus"]),
                    ReservasConsumidasMes  = Convert.ToInt32(row["ReservasConsumidasMes"]),
                    NombrePlan             = row["NombrePlan"].ToString(),
                    ReservasDelPlan        = Convert.ToInt32(row["ReservasMensuales"]),
                    AnticipacionMaxDelPlan = Convert.ToInt32(row["AnticipacionMaximaDias"]),
                    AccesoPremiumDelPlan   = Convert.ToBoolean(row["AccesoPremium"])
                };

            return cliente;
        }
    }
}

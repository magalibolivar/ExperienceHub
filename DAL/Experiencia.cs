using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Capa de Acceso a Datos — Experiencia. Opera sobre la tabla [Experiencia].
    /// Hereda de <see cref="BaseDAL{T}"/>.
    /// </summary>
    public class Experiencia : BaseDAL<BE.Experiencia>
    {
        // T07 — Dígito Verificador de esta tabla (fuente única).
        public const  string   DV_Tabla    = "Experiencia";
        public const  string   DV_Pk       = "IdExperiencia";
        public static readonly string[] DV_Columnas =
            { "Nombre", "IdCategoria", "IdCiudad", "Fecha", "HoraInicio", "CupoMaximo", "IdOrganizador", "EdadMinima", "Premium" };

        private void RecalcularDV()
        {
            try { new DigitoVerificador().RecalcularTabla(DV_Tabla, DV_Pk, DV_Columnas); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("[DAL.Experiencia.RecalcularDV] " + ex.Message); }
        }

        private const string SELECT_BASE =
            "SELECT e.IdExperiencia, e.Nombre, e.Descripcion, e.IdCategoria, e.IdCiudad, " +
            "       e.Ubicacion, e.Fecha, e.HoraInicio, e.DuracionMinutos, e.IdOrganizador, " +
            "       e.CupoMaximo, e.CupoDisponible, e.EdadMinima, e.Premium, e.Estado, " +
            "       cat.Nombre AS NombreCategoria, ciu.Nombre AS NombreCiudad, org.Nombre AS NombreOrganizador " +
            "FROM Experiencia e " +
            "LEFT JOIN Categoria   cat ON cat.IdCategoria   = e.IdCategoria " +
            "LEFT JOIN Ciudad      ciu ON ciu.IdCiudad      = e.IdCiudad " +
            "LEFT JOIN Organizador org ON org.IdOrganizador = e.IdOrganizador ";

        public override List<BE.Experiencia> ObtenerTodos()
        {
            var lista = new List<BE.Experiencia>();
            try
            {
                DataTable tabla = acceso.Leer(SELECT_BASE + "ORDER BY e.Fecha, e.HoraInicio", null);
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener experiencias.", ex); }
            return lista;
        }

        /// <summary>Experiencias futuras, publicadas y con cupo (para reservar).</summary>
        public List<BE.Experiencia> ObtenerDisponibles()
        {
            var lista = new List<BE.Experiencia>();
            try
            {
                DataTable tabla = acceso.Leer(
                    SELECT_BASE +
                    "WHERE e.Estado = @Estado AND e.CupoDisponible > 0 " +
                    "AND (CAST(e.Fecha AS DATETIME) + CAST(e.HoraInicio AS DATETIME)) > GETDATE() " +
                    "ORDER BY e.Fecha, e.HoraInicio",
                    new[] { new SqlParameter("@Estado", (object)(int)BE.EstadoExperiencia.Programada) });
                foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            }
            catch (Exception ex) { throw new Exception("Error al obtener experiencias disponibles.", ex); }
            return lista;
        }

        /// <summary>Experiencias de una ciudad (para respetar la ciudad del cliente).</summary>
        public List<BE.Experiencia> ObtenerPorCiudad(int idCiudad)
        {
            var lista = new List<BE.Experiencia>();
            DataTable tabla = acceso.Leer(
                SELECT_BASE + "WHERE e.IdCiudad = @IdCiudad ORDER BY e.Fecha, e.HoraInicio",
                new[] { new SqlParameter("@IdCiudad", idCiudad) });
            foreach (DataRow row in tabla.Rows) lista.Add(Mapear(row));
            return lista;
        }

        public override BE.Experiencia ObtenerPorId(int idExperiencia)
        {
            DataTable tabla = acceso.Leer(
                SELECT_BASE + "WHERE e.IdExperiencia = @Id",
                new[] { new SqlParameter("@Id", idExperiencia) });
            return tabla.Rows.Count == 0 ? null : Mapear(tabla.Rows[0]);
        }

        public int Alta(BE.Experiencia e)
        {
            var p = new List<SqlParameter>(ParamsComunes(e))
            {
                new SqlParameter("@CupoDisponible", e.CupoDisponible),
                new SqlParameter("@Estado", (int)e.Estado)
            };
            DataTable t = acceso.Leer(
                "INSERT INTO Experiencia (Nombre, Descripcion, IdCategoria, IdCiudad, Ubicacion, Fecha, HoraInicio, " +
                "DuracionMinutos, IdOrganizador, CupoMaximo, CupoDisponible, EdadMinima, Premium, Estado) " +
                "VALUES (@Nombre, @Descripcion, @IdCategoria, @IdCiudad, @Ubicacion, @Fecha, @HoraInicio, " +
                "@DuracionMinutos, @IdOrganizador, @CupoMaximo, @CupoDisponible, @EdadMinima, @Premium, @Estado); " +
                "SELECT SCOPE_IDENTITY() AS IdNuevo",
                p.ToArray());
            int id = t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[0]["IdNuevo"]) : 0;
            RecalcularDV();
            return id;
        }

        /// <summary>Modifica datos descriptivos. No toca CupoDisponible (lo gestionan las reservas).</summary>
        public void Modificar(BE.Experiencia e)
        {
            var p = new List<SqlParameter>(ParamsComunes(e)) { new SqlParameter("@Id", e.IdExperiencia) };
            acceso.Escribir(
                "UPDATE Experiencia SET Nombre=@Nombre, Descripcion=@Descripcion, IdCategoria=@IdCategoria, " +
                "IdCiudad=@IdCiudad, Ubicacion=@Ubicacion, Fecha=@Fecha, HoraInicio=@HoraInicio, " +
                "DuracionMinutos=@DuracionMinutos, IdOrganizador=@IdOrganizador, CupoMaximo=@CupoMaximo, " +
                "EdadMinima=@EdadMinima, Premium=@Premium WHERE IdExperiencia=@Id",
                p.ToArray());
            RecalcularDV();
        }

        public void CambiarEstado(int idExperiencia, BE.EstadoExperiencia nuevoEstado)
        {
            acceso.Escribir("UPDATE Experiencia SET Estado=@Estado WHERE IdExperiencia=@Id",
                new[] { new SqlParameter("@Estado", (int)nuevoEstado), new SqlParameter("@Id", idExperiencia) });
            RecalcularDV();
        }

        public void RecalcularDVPublico() => RecalcularDV();

        /// <summary>
        /// Mantenimiento por fecha: marca como Finalizada las experiencias cuyo horario ya terminó
        /// y como EnCurso las que están ocurriendo. No toca las Canceladas. Devuelve filas finalizadas.
        /// </summary>
        public int ActualizarEstadosPorFecha()
        {
            const string FIN = "DATEADD(MINUTE, DuracionMinutos, CAST(Fecha AS DATETIME) + CAST(HoraInicio AS DATETIME))";
            var pFin = new[]
            {
                new SqlParameter("@Fin",   (object)(int)BE.EstadoExperiencia.Finalizada),
                new SqlParameter("@Prog",  (object)(int)BE.EstadoExperiencia.Programada),
                new SqlParameter("@Comp",  (object)(int)BE.EstadoExperiencia.Completa),
                new SqlParameter("@Curso", (object)(int)BE.EstadoExperiencia.EnCurso)
            };
            int finalizadas = acceso.Escribir(
                "UPDATE Experiencia SET Estado = @Fin " +
                "WHERE Estado IN (@Prog, @Comp, @Curso) AND " + FIN + " < GETDATE()", pFin);

            acceso.Escribir(
                "UPDATE Experiencia SET Estado = @Curso " +
                "WHERE Estado IN (@Prog, @Comp) " +
                "AND (CAST(Fecha AS DATETIME) + CAST(HoraInicio AS DATETIME)) <= GETDATE() " +
                "AND " + FIN + " >= GETDATE()",
                new[]
                {
                    new SqlParameter("@Curso", (object)(int)BE.EstadoExperiencia.EnCurso),
                    new SqlParameter("@Prog",  (object)(int)BE.EstadoExperiencia.Programada),
                    new SqlParameter("@Comp",  (object)(int)BE.EstadoExperiencia.Completa)
                });

            if (finalizadas > 0) RecalcularDV();
            return finalizadas;
        }

        private static SqlParameter[] ParamsComunes(BE.Experiencia e) => new[]
        {
            new SqlParameter("@Nombre", e.Nombre),
            new SqlParameter("@Descripcion", (object)e.Descripcion ?? DBNull.Value),
            new SqlParameter("@IdCategoria", e.IdCategoria),
            new SqlParameter("@IdCiudad", e.IdCiudad),
            new SqlParameter("@Ubicacion", (object)e.Ubicacion ?? DBNull.Value),
            new SqlParameter("@Fecha", e.Fecha.Date),
            new SqlParameter("@HoraInicio", e.HoraInicio),
            new SqlParameter("@DuracionMinutos", e.DuracionMinutos),
            new SqlParameter("@IdOrganizador", e.IdOrganizador),
            new SqlParameter("@CupoMaximo", e.CupoMaximo),
            new SqlParameter("@EdadMinima", e.EdadMinima),
            new SqlParameter("@Premium", e.Premium ? 1 : 0)
        };

        private BE.Experiencia Mapear(DataRow row) => new BE.Experiencia
        {
            IdExperiencia     = Convert.ToInt32(row["IdExperiencia"]),
            Nombre            = row["Nombre"].ToString(),
            Descripcion       = row["Descripcion"] != DBNull.Value ? row["Descripcion"].ToString() : null,
            IdCategoria       = Convert.ToInt32(row["IdCategoria"]),
            NombreCategoria   = row["NombreCategoria"] != DBNull.Value ? row["NombreCategoria"].ToString() : null,
            IdCiudad          = Convert.ToInt32(row["IdCiudad"]),
            NombreCiudad      = row["NombreCiudad"] != DBNull.Value ? row["NombreCiudad"].ToString() : null,
            Ubicacion         = row["Ubicacion"] != DBNull.Value ? row["Ubicacion"].ToString() : null,
            Fecha             = Convert.ToDateTime(row["Fecha"]),
            HoraInicio        = (TimeSpan)row["HoraInicio"],
            DuracionMinutos   = Convert.ToInt32(row["DuracionMinutos"]),
            IdOrganizador     = Convert.ToInt32(row["IdOrganizador"]),
            NombreOrganizador = row["NombreOrganizador"] != DBNull.Value ? row["NombreOrganizador"].ToString() : null,
            CupoMaximo        = Convert.ToInt32(row["CupoMaximo"]),
            CupoDisponible    = Convert.ToInt32(row["CupoDisponible"]),
            EdadMinima        = Convert.ToInt32(row["EdadMinima"]),
            Premium           = Convert.ToBoolean(row["Premium"]),
            Estado            = (BE.EstadoExperiencia)Convert.ToInt32(row["Estado"])
        };
    }
}

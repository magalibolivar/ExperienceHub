using System;
using System.Data;
using System.Data.SqlClient;

namespace DAL
{
    /// <summary>
    /// Capa de Acceso a Datos — Parámetros de negocio configurables (tabla [Parametro]).
    /// Clave/valor simple, leído por BLL.Configuracion. Tolera que la tabla aún no exista
    /// (base sin migrar): en ese caso devuelve el valor por defecto que pasa el llamador.
    /// </summary>
    public class Parametro
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        /// <summary>Valor entero de un parámetro, o <paramref name="defecto"/> si no existe,
        /// no es un entero válido o la tabla todavía no fue migrada.</summary>
        public int ObtenerEntero(string clave, int defecto)
        {
            try
            {
                DataTable dt = acceso.Leer("SELECT Valor FROM Parametro WHERE Clave=@C",
                    new[] { new SqlParameter("@C", clave) });
                if (dt.Rows.Count > 0 && int.TryParse(Convert.ToString(dt.Rows[0]["Valor"]), out int v))
                    return v;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[DAL.Parametro.ObtenerEntero] " + ex.Message);
            }
            return defecto;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DAL
{
    /// <summary>
    /// Aplicación IDEMPOTENTE, desde la app, de los objetos de esquema que la versión ACTUAL del
    /// binario necesita (PN03). Pensada para correr DESPUÉS de restaurar un backup anterior a la
    /// migración: deja la base al día (columna Reserva.IdSuscripcion, tabla Parametro, patentes,
    /// mapeos de botones, backfill y marcadores de re-sellado) antes de reiniciar la app.
    ///
    /// ÚNICA FUENTE DE VERDAD: ejecuta el MISMO script BD/16_PN03_Cancelacion.sql (embebido como
    /// recurso), partiéndolo en lotes por líneas "GO" (igual que SSMS/sqlcmd). No se replica la
    /// lógica en C#, para que el script del instalador y la migración en caliente nunca diverjan.
    /// </summary>
    public class Migracion
    {
        private readonly Acceso acceso = Acceso.GetInstance();

        // Recursos embebidos (ver DAL.csproj → EmbeddedResource/LogicalName), en orden de aplicación:
        //   PN03   — columna IdSuscripcion, Parametro, patentes, mapeos, backfill, re-sellado.
        //   Legacy — elimina tablas/patentes/DV heredados de WardrobeFlow (limpia un backup viejo).
        private static readonly string[] Recursos =
            { "DAL.Migracion.PN03.sql", "DAL.Migracion.Legacy.sql", "DAL.Migracion.Renovacion.sql" };

        /// <summary>
        /// Ejecuta los scripts de migración (PN03 + limpieza legacy) lote por lote. Devuelve la
        /// cantidad de lotes aplicados. Un lote que falle se registra (Trace) y no aborta el resto
        /// (los scripts son idempotentes).
        /// </summary>
        public int AsegurarEsquemaActual()
        {
            int lotes = 0;
            foreach (string recurso in Recursos)
                foreach (string lote in SplitEnBatches(LeerScript(recurso)))
                {
                    string sql = lote.Trim();
                    if (sql.Length == 0) continue;
                    try { acceso.Escribir(sql, null); lotes++; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError("[DAL.Migracion] lote falló: " + ex.Message);
                    }
                }
            return lotes;
        }

        private static string LeerScript(string recurso)
        {
            var asm = typeof(Migracion).Assembly;
            using (Stream stream = asm.GetManifestResourceStream(recurso))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        $"No se encontró el recurso embebido '{recurso}' con el script de migración.");
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                    return sr.ReadToEnd();
            }
        }

        // Separa el script en lotes por líneas que son exactamente "GO" (como sqlcmd/SSMS).
        private static IEnumerable<string> SplitEnBatches(string script)
        {
            var actual = new StringBuilder();
            foreach (string linea in script.Replace("\r\n", "\n").Split('\n'))
            {
                if (linea.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    yield return actual.ToString();
                    actual.Clear();
                }
                else actual.AppendLine(linea);
            }
            if (actual.Length > 0) yield return actual.ToString();
        }
    }
}

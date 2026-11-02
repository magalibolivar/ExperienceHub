using System.Windows.Forms;

namespace GUI.Exportacion
{
    /// <summary>
    /// Patrón FACTORY METHOD — rol "ConcreteProduct" (reemplaza a <see cref="ExportadorPdf"/>).
    ///
    /// Lleva un <see cref="ReporteExportable"/> al motor documental unificado (PDFsharp, archivo real,
    /// sin impresora virtual — ver <see cref="ExportadorPdfArchivo"/>), adaptando el modelo "plano" que
    /// usan Bitácora/Reporte de Jornada/Clientes al modelo por bloques <see cref="DocumentoReporte"/>
    /// que ya usan los documentos de PN01/PN02/PN03. Así los 10 documentos del sistema comparten el
    /// mismo renderer, identidad visual, paginación e i18n — sin tocar el modelo de entrada que arman
    /// los formularios (Factory Method intacto: siguen pidiendo un <see cref="Exportador"/> al
    /// <see cref="GeneradorReporte"/> correspondiente).
    /// </summary>
    public sealed class ExportadorPdfUnificado : Exportador
    {
        public ExportadorPdfUnificado(string origen)
        {
            _formato = "PDF";
            _origen  = origen;
        }

        public override string Exportar(ReporteExportable reporte, IWin32Window propietario)
        {
            var t = Servicios.Multiidioma.Traductor.ObtenerTraducciones(Servicios.Multiidioma.GestorIdioma.IdiomaActual);
            string T(string k, string fb) => t.ContainsKey(k) ? t[k].Texto : fb;

            if (reporte.EsTabular && (reporte.Datos == null || reporte.Datos.Rows.Count == 0))
            {
                MessageBox.Show(
                    T("err.pdf.sinDatos", "No hay datos para exportar."),
                    T("lbl.exportarpdf",  "Exportar PDF"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            var doc = new DocumentoReporte
            {
                NombreArchivo = reporte.NombreArchivo ?? reporte.Titulo,
                Modulo        = _origen,
                Titulo        = reporte.Titulo
            };

            if (reporte.EsTabular)
            {
                var filas = new System.Collections.Generic.List<string[]>();
                foreach (System.Data.DataRow fila in reporte.Datos.Rows)
                {
                    var vals = new string[reporte.Datos.Columns.Count];
                    for (int c = 0; c < vals.Length; c++) vals[c] = fila[c]?.ToString() ?? "";
                    filas.Add(vals);
                }
                doc.Parrafo(string.Format(T("msg.bit.registros", "{0} registro(s)"), reporte.Datos.Rows.Count).Trim());
                doc.Tabla(reporte.Encabezados, filas, null, null);
            }
            else
            {
                doc.TextoMono(reporte.TextoPlano ?? "");
            }

            return new ExportadorPdfArchivo().Exportar(doc, propietario);
        }
    }
}

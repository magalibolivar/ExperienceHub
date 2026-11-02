using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI.Exportacion
{
    /// <summary>
    /// Renderiza un <see cref="DocumentoReporte"/> a PDF vía PrintDocument + vista previa (impresión
    /// convencional — el usuario materializa el PDF con "Microsoft Print to PDF"; mecanismo aparte,
    /// independiente de <see cref="ExportadorPdfArchivo"/>, que genera el archivo directo). Produce un
    /// informe EJECUTIVO: encabezado
    /// institucional en cada página, jerarquía tipográfica (Montserrat), tablas reales con encabezado y
    /// filas alternadas, pares clave/valor, etiquetas de estado, un gráfico de variación y pie con
    /// numeración. Pagina sin cortar bloques (las tablas se parten por fila repitiendo su encabezado).
    /// Identidad ExperienceHub: azul marino en títulos, acentos coral moderados, legible en B&N.
    /// </summary>
    public sealed class ExportadorPdfDocumento
    {
        private DocumentoReporte _doc;
        private int _idx;          // próximo bloque a dibujar
        private int _filaTabla;    // próxima fila de la tabla en curso (si el bloque es tabla)
        private int _pagina;

        public void Exportar(DocumentoReporte doc, IWin32Window propietario)
        {
            _doc = doc;
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            string Tr(string k, string fb) => t.ContainsKey(k) ? t[k].Texto : fb;

            using (var pd = new PrintDocument())
            {
                pd.DocumentName = doc.NombreArchivo ?? doc.Titulo ?? "Informe";
                pd.DefaultPageSettings.Margins = new Margins(50, 50, 55, 50);
                pd.BeginPrint += (s, e) => { _idx = 0; _filaTabla = 0; _pagina = 1; };
                pd.PrintPage += (s, e) => DibujarPagina(e, Tr);

                using (var prev = new PrintPreviewDialog())
                {
                    prev.Document = pd;
                    prev.Width = 1050; prev.Height = 820;
                    prev.Text = $"{Tr("bit.pdf.vistaprevia", "Vista Previa")} — {doc.Titulo}";
                    prev.ShowDialog(propietario);
                }
            }
        }

        private void DibujarPagina(PrintPageEventArgs e, Func<string, string, string> Tr)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var m = e.MarginBounds;
            float y = m.Top;

            y = DibujarEncabezadoInstitucional(g, m);

            if (_pagina == 1)
            {
                using (var fTit = Estilo.SemiBold(18f))
                using (var brTit = new SolidBrush(Estilo.AzulOscuro))
                    g.DrawString(_doc.Titulo ?? "", fTit, brTit, m.Left, y);
                y += 30;
                if (!string.IsNullOrEmpty(_doc.Subtitulo))
                {
                    using (var fSub = Estilo.Normal(11f))
                    using (var brSub = new SolidBrush(Estilo.Gris700))
                        g.DrawString(_doc.Subtitulo, fSub, brSub, m.Left, y);
                    y += 22;
                }
                using (var pen = new Pen(Estilo.Coral, 2f))
                    g.DrawLine(pen, m.Left, y, m.Right, y);
                y += 12;
            }

            float bottom = m.Bottom - 24;
            while (_idx < _doc.Bloques.Count)
            {
                var b = _doc.Bloques[_idx];
                if (b is DocumentoReporte.BloqueTabla tabla)
                {
                    bool termino = DibujarTabla(g, tabla, m.Left, ref y, m.Width, bottom);
                    if (!termino) { DibujarPie(g, m, Tr); e.HasMorePages = true; _pagina++; return; }
                    _idx++; _filaTabla = 0;
                }
                else
                {
                    float alto = MedirBloque(g, b, m.Width);
                    if (y + alto > bottom && y > m.Top + 40) { DibujarPie(g, m, Tr); e.HasMorePages = true; _pagina++; return; }
                    DibujarBloque(g, b, m.Left, ref y, m.Width);
                    _idx++;
                }
            }
            DibujarPie(g, m, Tr);
            e.HasMorePages = false;
        }

        // ── Encabezado institucional (discreto, en todas las páginas) ──────────────
        private float DibujarEncabezadoInstitucional(Graphics g, Rectangle m)
        {
            float y = m.Top;
            using (var fInst = Estilo.SemiBold(11f))
            using (var fMod = Estilo.Normal(9f))
            using (var brAzul = new SolidBrush(Estilo.AzulOscuro))
            using (var brGris = new SolidBrush(Estilo.Gris700))
            {
                g.DrawString(_doc.Institucion, fInst, brAzul, m.Left, y);
                if (!string.IsNullOrEmpty(_doc.Modulo))
                {
                    var sz = g.MeasureString(_doc.Institucion, fInst);
                    g.DrawString("·  " + _doc.Modulo, fMod, brGris, m.Left + sz.Width + 6, y + 3);
                }
                using (var sf = new StringFormat { Alignment = StringAlignment.Far })
                    g.DrawString(_doc.Titulo ?? "", fMod, brGris, new RectangleF(m.Left, y + 2, m.Width, 16), sf);
            }
            y += 20;
            using (var pen = new Pen(Estilo.Gris200, 1f))
                g.DrawLine(pen, m.Left, y, m.Right, y);
            return y + 10;
        }

        private void DibujarPie(Graphics g, Rectangle m, Func<string, string, string> Tr)
        {
            using (var pen = new Pen(Estilo.Gris200, 1f))
            using (var f = Estilo.Normal(8f))
            using (var br = new SolidBrush(Estilo.Gris400))
            {
                g.DrawLine(pen, m.Left, m.Bottom - 18, m.Right, m.Bottom - 18);
                g.DrawString(string.Format(Tr("bit.pdf.pagina", "ExperienceHub — Página {0}"), _pagina), f, br, m.Left, m.Bottom - 14);
                using (var sf = new StringFormat { Alignment = StringAlignment.Far })
                    g.DrawString($"{Tr("rpt.txt.generado", "Generado")}: {DateTime.Now:dd/MM/yyyy HH:mm}", f, br, new RectangleF(m.Left, m.Bottom - 14, m.Width, 14), sf);
            }
        }

        // ── Medición ───────────────────────────────────────────────────────────────
        private float MedirBloque(Graphics g, DocumentoReporte.Bloque b, float ancho)
        {
            switch (b)
            {
                case DocumentoReporte.BloqueSeccion _: return 30;
                case DocumentoReporte.BloqueEspacio es: return es.Alto;
                case DocumentoReporte.BloqueEtiquetas _: return 26;
                case DocumentoReporte.BloqueParrafo p:
                    using (var f = FuenteParrafo(p))
                        return g.MeasureString(p.Texto ?? "", f, (int)ancho).Height + 6;
                case DocumentoReporte.BloqueClaveValor kv:
                    return kv.Items.Count * 18 + 6;
                case DocumentoReporte.BloqueBarrasVariacion bv:
                    return 24 + bv.Datos.Count * 20 + 8;
                default: return 20;
            }
        }

        private Font FuenteParrafo(DocumentoReporte.BloqueParrafo p)
            => p.Nota ? Estilo.Crear(8.5f, FontStyle.Italic) : (p.Enfasis ? Estilo.SemiBold(10f) : Estilo.Normal(10f));

        // ── Dibujo de bloques no-tabla ──────────────────────────────────────────────
        private void DibujarBloque(Graphics g, DocumentoReporte.Bloque b, float x, ref float y, float ancho)
        {
            switch (b)
            {
                case DocumentoReporte.BloqueSeccion s:
                    using (var f = Estilo.Bold(8.5f))
                    using (var br = new SolidBrush(Estilo.AzulOscuro))
                        g.DrawString((s.Texto ?? "").ToUpperInvariant(), f, br, x, y + 8);
                    using (var pen = new Pen(Estilo.Gris200, 1f))
                        g.DrawLine(pen, x, y + 26, x + ancho, y + 26);
                    y += 30;
                    break;

                case DocumentoReporte.BloqueParrafo p:
                    using (var f = FuenteParrafo(p))
                    using (var br = new SolidBrush(p.Nota ? Estilo.Gris400 : Estilo.Gris700))
                    {
                        var rect = new RectangleF(x, y, ancho, g.MeasureString(p.Texto ?? "", f, (int)ancho).Height);
                        g.DrawString(p.Texto ?? "", f, br, rect);
                        y += rect.Height + 6;
                    }
                    break;

                case DocumentoReporte.BloqueClaveValor kv:
                    using (var fK = Estilo.Normal(9.5f))
                    using (var fV = Estilo.SemiBold(9.5f))
                    using (var brK = new SolidBrush(Estilo.Gris700))
                    using (var brV = new SolidBrush(Estilo.AzulOscuro))
                    {
                        float colK = Math.Min(190, ancho * 0.32f);
                        foreach (var it in kv.Items)
                        {
                            g.DrawString(it.Clave, fK, brK, x, y);
                            g.DrawString(it.Valor ?? "", fV, brV, x + colK, y);
                            y += 18;
                        }
                        y += 6;
                    }
                    break;

                case DocumentoReporte.BloqueEtiquetas et:
                    float ex = x;
                    using (var f = Estilo.Bold(8f))
                        foreach (var it in et.Items)
                        {
                            var sz = g.MeasureString(it.Texto, f);
                            float w = sz.Width + 16;
                            using (var path = Redondeado(new RectangleF(ex, y, w, 20), 9))
                            using (var bg = new SolidBrush(it.Color))
                                g.FillPath(bg, path);
                            using (var brW = new SolidBrush(Color.White))
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                g.DrawString(it.Texto, f, brW, new RectangleF(ex, y, w, 20), sf);
                            ex += w + 8;
                        }
                    y += 26;
                    break;

                case DocumentoReporte.BloqueBarrasVariacion bv:
                    DibujarBarrasVariacion(g, bv, x, ref y, ancho);
                    break;

                case DocumentoReporte.BloqueEspacio es:
                    y += es.Alto;
                    break;
            }
        }

        // ── Tabla (partible por fila, repite encabezado en cada página) ─────────────
        private bool DibujarTabla(Graphics g, DocumentoReporte.BloqueTabla t, float x, ref float y, float ancho, float bottom)
        {
            int nCols = t.Headers.Length;
            float pesoTotal = t.Pesos != null ? t.Pesos.Sum() : nCols;
            float[] anchos = new float[nCols];
            for (int c = 0; c < nCols; c++) anchos[c] = ancho * ((t.Pesos != null ? t.Pesos[c] : 1) / pesoTotal);

            float hHeader = 26, hFila = 22;

            // Encabezado de la tabla (se repite al continuar en otra página). Devuelve el nuevo y
            // (no captura el parámetro ref en una función local → evita CS1628).
            float Header(float yy)
            {
                using (var bg = new SolidBrush(Estilo.AzulOscuro))
                    g.FillRectangle(bg, x, yy, ancho, hHeader);
                using (var f = Estilo.Bold(9f))
                using (var br = new SolidBrush(Color.White))
                {
                    float cx = x;
                    for (int c = 0; c < nCols; c++)
                    {
                        var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap,
                            Alignment = (t.AlinearDerecha != null && t.AlinearDerecha[c]) ? StringAlignment.Far : StringAlignment.Near };
                        g.DrawString(t.Headers[c], f, br, new RectangleF(cx + 6, yy, anchos[c] - 12, hHeader), sf);
                        cx += anchos[c];
                    }
                }
                return yy + hHeader;
            }

            y = Header(y);   // dibuja el encabezado de la tabla al entrar a la página

            using (var fCel = Estilo.Normal(9f))
            using (var brTxt = new SolidBrush(Estilo.Gris700))
            using (var penSep = new Pen(Estilo.Gris200, 0.5f))
            {
                while (_filaTabla < t.Filas.Count)
                {
                    if (y + hFila > bottom) return false;   // no entra → continuar en la próxima página
                    var fila = t.Filas[_filaTabla];
                    if (_filaTabla % 2 == 1)
                        using (var bgA = new SolidBrush(Estilo.Gris100))
                            g.FillRectangle(bgA, x, y, ancho, hFila);
                    float cx = x;
                    for (int c = 0; c < nCols; c++)
                    {
                        var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap,
                            Alignment = (t.AlinearDerecha != null && t.AlinearDerecha[c]) ? StringAlignment.Far : StringAlignment.Near };
                        string val = c < fila.Length ? fila[c] : "";
                        g.DrawString(val, fCel, brTxt, new RectangleF(cx + 6, y, anchos[c] - 12, hFila), sf);
                        cx += anchos[c];
                    }
                    g.DrawLine(penSep, x, y + hFila, x + ancho, y + hFila);
                    y += hFila;
                    _filaTabla++;
                }
            }
            y += 10;
            return true;
        }

        // ── Gráfico de variación (barras horizontales +/- desde un eje central) ─────
        private void DibujarBarrasVariacion(Graphics g, DocumentoReporte.BloqueBarrasVariacion bv, float x, ref float y, float ancho)
        {
            using (var fTit = Estilo.SemiBold(9.5f))
            using (var brTit = new SolidBrush(Estilo.AzulOscuro))
                g.DrawString(bv.Titulo, fTit, brTit, x, y);
            y += 22;

            decimal maxAbs = bv.Datos.Count > 0 ? Math.Max(1m, bv.Datos.Max(d => Math.Abs(d.Pct))) : 1m;
            float labelW = Math.Min(190, ancho * 0.34f);
            float ejeX = x + labelW + (ancho - labelW) / 2f;   // eje 0 en el centro de la zona de barras
            float semilargo = (ancho - labelW) / 2f - 48;

            using (var fLbl = Estilo.Normal(8.5f))
            using (var fVal = Estilo.Bold(8.5f))
            using (var brTxt = new SolidBrush(Estilo.Gris700))
            using (var penEje = new Pen(Estilo.Gris400, 1f))
            {
                g.DrawLine(penEje, ejeX, y, ejeX, y + bv.Datos.Count * 20);
                foreach (var d in bv.Datos)
                {
                    var sfR = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(d.Etiqueta, fLbl, brTxt, new RectangleF(x, y, labelW - 6, 20), sfR);
                    float w = (float)(semilargo * (double)(Math.Abs(d.Pct) / maxAbs));
                    Color col = d.Pct >= 0 ? Estilo.Verde : Estilo.Coral;
                    var rect = d.Pct >= 0 ? new RectangleF(ejeX, y + 4, w, 12) : new RectangleF(ejeX - w, y + 4, w, 12);
                    using (var bg = new SolidBrush(col))
                        g.FillRectangle(bg, rect);
                    using (var sfV = new StringFormat { LineAlignment = StringAlignment.Center })
                        g.DrawString(d.Pct.ToString("+0;-0;0") + "%", fVal, brTxt,
                            new RectangleF(d.Pct >= 0 ? ejeX + w + 4 : ejeX - w - 44, y + 2, 42, 16),
                            new StringFormat { LineAlignment = StringAlignment.Center, Alignment = d.Pct >= 0 ? StringAlignment.Near : StringAlignment.Far });
                    y += 20;
                }
            }
            y += 8;
        }

        private static GraphicsPath Redondeado(RectangleF r, float radio)
        {
            float d = radio * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}

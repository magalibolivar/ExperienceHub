using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Servicios.Multiidioma;

namespace GUI.Exportacion
{
    /// <summary>
    /// Renderiza un <see cref="DocumentoReporte"/> a un archivo PDF REAL usando PDFsharp (MIT, C# puro,
    /// sin dependencias nativas) — a diferencia de <see cref="ExportadorPdfDocumento"/>, que depende de
    /// la impresora virtual "Microsoft Print to PDF" vía PrintPreview. Genera el archivo directamente,
    /// deja elegir ubicación/nombre, pagina en A4 sin cortar bloques (las tablas se parten por fila
    /// repitiendo su encabezado), numera "Página N de M", y ofrece abrir el PDF al terminar.
    ///
    /// Reutiliza el MISMO modelo documental que el resto del sistema, así que cubre TODOS los documentos
    /// (PN01 comprobantes/constancias, PN02 reservas, PN03 tablero/plan) sin lógica por tipo. La impresión
    /// convencional (vista previa) sigue disponible en <see cref="ExportadorPdfDocumento"/>.
    /// Tipografía: "Segoe UI" (del sistema, siempre presente en Windows) para resolución de fuente estable.
    /// </summary>
    public sealed class ExportadorPdfArchivo
    {
        private const string Fuente = "Segoe UI";
        // Geometría A4 en puntos (1/72"): PDFsharp trabaja en puntos por defecto.
        private const double PageW = 595.28, PageH = 841.89;
        private const double MgL = 50, MgR = 50, MgTop = 50, MgBottom = 50;
        private double X0 => MgL;
        private double Ancho => PageW - MgL - MgR;
        private double Bottom => PageH - MgBottom - 22;   // deja lugar al pie

        private DocumentoReporte _doc;
        private PdfDocument _pdf;
        private PdfPage _page;
        private XGraphics _g;
        private double _y;

        private Func<string, string, string> _tr;

        // Colores de marca (System.Drawing → XColor).
        private static XColor X(System.Drawing.Color c) => XColor.FromArgb(c.A, c.R, c.G, c.B);
        private XColor AzulOscuro => X(Estilo.AzulOscuro);
        private XColor Coral => X(Estilo.Coral);
        private XColor Gris700 => X(Estilo.Gris700);
        private XColor Gris400 => X(Estilo.Gris400);
        private XColor Gris200 => X(Estilo.Gris200);
        private XColor Gris100 => X(Estilo.Gris100);

        /// <summary>Muestra un diálogo para guardar y genera el PDF en la ruta elegida. Devuelve la ruta
        /// (o null si el usuario canceló o falló). Ofrece abrir el documento al terminar.</summary>
        public string Exportar(DocumentoReporte doc, IWin32Window propietario)
        {
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            _tr = (k, fb) => t.ContainsKey(k) ? t[k].Texto : fb;

            using (var sfd = new SaveFileDialog
            {
                Title = _tr("eh.pdf.guardar", "Guardar PDF"),
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = SanearArchivo(doc.NombreArchivo ?? doc.Titulo ?? "Documento") + ".pdf",
                OverwritePrompt = true
            })
            {
                if (sfd.ShowDialog(propietario) != DialogResult.OK) return null;
                try
                {
                    Generar(doc, sfd.FileName);
                }
                catch (IOException ex)
                {
                    Estilo.Error(propietario as Form, _tr("eh.pdf.guardar", "Guardar PDF"),
                        string.Format(_tr("eh.pdf.err.escritura", "No se pudo escribir el PDF (¿está abierto en otro programa o sin permisos?).\n{0}"), ex.Message));
                    return null;
                }
                catch (UnauthorizedAccessException ex)
                {
                    Estilo.Error(propietario as Form, _tr("eh.pdf.guardar", "Guardar PDF"),
                        string.Format(_tr("eh.pdf.err.permiso", "Sin permisos para escribir en esa ubicación.\n{0}"), ex.Message));
                    return null;
                }

                if (Estilo.Confirmar(propietario as Form, _tr("eh.pdf.guardar", "Guardar PDF"),
                        string.Format(_tr("eh.pdf.abrir", "PDF generado en:\n{0}\n\n¿Abrirlo ahora?"), sfd.FileName)))
                {
                    try { Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true }); } catch { }
                }
                return sfd.FileName;
            }
        }

        /// <summary>Genera el PDF directamente en la ruta dada (sin diálogo). Útil para pruebas/automatización.</summary>
        public void Generar(DocumentoReporte doc, string ruta)
        {
            if (_tr == null)
            {
                var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
                _tr = (k, fb) => t.ContainsKey(k) ? t[k].Texto : fb;
            }
            _doc = doc;
            _pdf = new PdfDocument();
            _pdf.Info.Title = doc.Titulo ?? "Documento";
            _pdf.Info.Author = "ExperienceHub";
            NuevaPagina(primera: true);

            foreach (var b in doc.Bloques)
                DibujarBloque(b);

            _g?.Dispose(); _g = null;   // liberar el XGraphics de la última página antes de la 2da pasada
            PiesConTotal();
            _pdf.Save(ruta);
            _pdf.Dispose();
        }

        // ── Páginas ────────────────────────────────────────────────────────────────
        private void NuevaPagina(bool primera)
        {
            _g?.Dispose();
            _page = _pdf.AddPage();
            _page.Size = PdfSharp.PageSize.A4;
            _g = XGraphics.FromPdfPage(_page);
            _y = MgTop;
            DibujarEncabezado();
            if (primera) DibujarTitulo();
        }

        private void AsegurarEspacio(double alto)
        {
            if (_y + alto > Bottom && _y > MgTop + 30)
                NuevaPagina(primera: false);
        }

        private void DibujarEncabezado()
        {
            var fInst = new XFont(Fuente, 11, XFontStyle.Bold);
            var fMod = new XFont(Fuente, 8.5, XFontStyle.Regular);
            {
                _g.DrawString(_doc.Institucion ?? "ExperienceHub", fInst, new XSolidBrush(AzulOscuro), X0, _y + 9);
                if (!string.IsNullOrEmpty(_doc.Modulo))
                {
                    double w = _g.MeasureString(_doc.Institucion ?? "ExperienceHub", fInst).Width;
                    _g.DrawString("·  " + _doc.Modulo, fMod, new XSolidBrush(Gris700), X0 + w + 6, _y + 9);
                }
                _g.DrawString(_doc.Titulo ?? "", fMod, new XSolidBrush(Gris400),
                    new XRect(X0, _y, Ancho, 14), new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Center });
            }
            _y += 18;
            _g.DrawLine(new XPen(Gris200, 1), X0, _y, X0 + Ancho, _y);
            _y += 10;
        }

        private void DibujarTitulo()
        {
            var fTit = new XFont(Fuente, 18, XFontStyle.Bold);
            var fSub = new XFont(Fuente, 11, XFontStyle.Regular);
            {
                _g.DrawString(_doc.Titulo ?? "", fTit, new XSolidBrush(AzulOscuro), X0, _y + 16);
                _y += 28;
                if (!string.IsNullOrEmpty(_doc.Subtitulo))
                {
                    _g.DrawString(_doc.Subtitulo, fSub, new XSolidBrush(Gris700), X0, _y + 10);
                    _y += 20;
                }
                _g.DrawLine(new XPen(Coral, 2), X0, _y, X0 + Ancho, _y);
                _y += 12;
            }
        }

        // Pie con numeración "Página N de M" (segunda pasada: ya se conoce el total).
        private void PiesConTotal()
        {
            int total = _pdf.PageCount;
            var f = new XFont(Fuente, 8, XFontStyle.Regular);
                for (int i = 0; i < total; i++)
                {
                    var pg = _pdf.Pages[i];
                    using (var g = XGraphics.FromPdfPage(pg, XGraphicsPdfPageOptions.Append))
                    {
                        double yPie = PageH - MgBottom - 6;
                        g.DrawLine(new XPen(Gris200, 1), MgL, yPie - 4, PageW - MgR, yPie - 4);
                        g.DrawString(string.Format(_tr("eh.pdf.paginaNM", "ExperienceHub — Página {0} de {1}"), i + 1, total),
                            f, new XSolidBrush(Gris400), MgL, yPie + 4);
                        g.DrawString($"{_tr("rpt.txt.generado", "Generado")}: {DateTime.Now:dd/MM/yyyy HH:mm}",
                            f, new XSolidBrush(Gris400), new XRect(MgL, yPie - 2, PageW - MgL - MgR, 12),
                            new XStringFormat { Alignment = XStringAlignment.Far });
                    }
                }
        }

        // ── Bloques ──────────────────────────────────────────────────────────────────
        private void DibujarBloque(DocumentoReporte.Bloque b)
        {
            switch (b)
            {
                case DocumentoReporte.BloqueSeccion s:        DibujarSeccion(s.Texto); break;
                case DocumentoReporte.BloqueParrafo p:        DibujarParrafo(p); break;
                case DocumentoReporte.BloqueClaveValor kv:    DibujarClaveValor(kv); break;
                case DocumentoReporte.BloqueTabla t:          DibujarTabla(t); break;
                case DocumentoReporte.BloqueEtiquetas e:      DibujarEtiquetas(e); break;
                case DocumentoReporte.BloqueBarrasVariacion v: DibujarBarras(v); break;
                case DocumentoReporte.BloqueEspacio es:       _y += es.Alto; break;
                case DocumentoReporte.BloqueTextoMono m:      DibujarTextoMono(m); break;
            }
        }

        // Texto preformateado (alineación manual por espacios, p. ej. Reporte de Jornada): una línea
        // de origen = una línea impresa, en fuente monoespaciada, sin envolver ni truncar — es el mismo
        // criterio que ya regía para este reporte en el motor anterior (preserva su alineación).
        private void DibujarTextoMono(DocumentoReporte.BloqueTextoMono m)
        {
            var f = new XFont("Consolas", 8, XFontStyle.Regular);
            var brush = new XSolidBrush(Gris700);
            foreach (var linea in (m.Texto ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                AsegurarEspacio(12);
                _g.DrawString(linea, f, brush, X0, _y + 9);
                _y += 12;
            }
            _y += 4;
        }

        private void DibujarSeccion(string texto)
        {
            AsegurarEspacio(30);
            var f = new XFont(Fuente, 9, XFontStyle.Bold);
                _g.DrawString((texto ?? "").ToUpperInvariant(), f, new XSolidBrush(AzulOscuro), X0, _y + 14);
            _y += 20;
            _g.DrawLine(new XPen(Gris200, 1), X0, _y, X0 + Ancho, _y);
            _y += 8;
        }

        private void DibujarParrafo(DocumentoReporte.BloqueParrafo p)
        {
            var style = p.Nota ? XFontStyle.Italic : (p.Enfasis ? XFontStyle.Bold : XFontStyle.Regular);
            var f = new XFont(Fuente, p.Nota ? 8.5 : 10, style);
            {
                var brush = new XSolidBrush(p.Nota ? Gris400 : Gris700);
                foreach (var linea in EnvolverTexto(p.Texto ?? "", f, Ancho))
                {
                    AsegurarEspacio(15);
                    _g.DrawString(linea, f, brush, X0, _y + 11);
                    _y += 15;
                }
                _y += 4;
            }
        }

        private void DibujarClaveValor(DocumentoReporte.BloqueClaveValor kv)
        {
            var fK = new XFont(Fuente, 9.5, XFontStyle.Regular);
            var fV = new XFont(Fuente, 9.5, XFontStyle.Bold);
            {
                double colK = Math.Min(190, Ancho * 0.34);
                foreach (var it in kv.Items)
                {
                    AsegurarEspacio(17);
                    _g.DrawString(it.Clave ?? "", fK, new XSolidBrush(Gris700), X0, _y + 11);
                    _g.DrawString(Truncar(it.Valor ?? "", fV, Ancho - colK), fV, new XSolidBrush(AzulOscuro), X0 + colK, _y + 11);
                    _y += 17;
                }
                _y += 6;
            }
        }

        private void DibujarEtiquetas(DocumentoReporte.BloqueEtiquetas e)
        {
            AsegurarEspacio(24);
            double x = X0;
            var f = new XFont(Fuente, 8, XFontStyle.Bold);
                foreach (var it in e.Items)
                {
                    double w = _g.MeasureString(it.Texto, f).Width + 16;
                    _g.DrawRoundedRectangle(new XSolidBrush(X(it.Color)), x, _y, w, 18, 9, 9);
                    _g.DrawString(it.Texto, f, XBrushes.White, new XRect(x, _y, w, 18),
                        new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center });
                    x += w + 8;
                }
            _y += 26;
        }

        private void DibujarBarras(DocumentoReporte.BloqueBarrasVariacion v)
        {
            AsegurarEspacio(24 + v.Datos.Count * 18 + 8);
            var fTit = new XFont(Fuente, 9.5, XFontStyle.Bold);
                _g.DrawString(v.Titulo ?? "", fTit, new XSolidBrush(AzulOscuro), X0, _y + 11);
            _y += 20;

            decimal maxAbs = v.Datos.Count > 0 ? Math.Max(1m, v.Datos.Max(d => Math.Abs(d.Pct))) : 1m;
            double labelW = Math.Min(190, Ancho * 0.34);
            double ejeX = X0 + labelW + (Ancho - labelW) / 2.0;
            double semi = (Ancho - labelW) / 2.0 - 46;

            var fL = new XFont(Fuente, 8.5, XFontStyle.Regular);
            var fV = new XFont(Fuente, 8.5, XFontStyle.Bold);
            {
                _g.DrawLine(new XPen(Gris400, 1), ejeX, _y, ejeX, _y + v.Datos.Count * 18);
                foreach (var d in v.Datos)
                {
                    _g.DrawString(Truncar(d.Etiqueta, fL, labelW - 6), fL, new XSolidBrush(Gris700),
                        new XRect(X0, _y, labelW - 6, 16), new XStringFormat { LineAlignment = XLineAlignment.Center });
                    double w = semi * (double)(Math.Abs(d.Pct) / maxAbs);
                    var col = d.Pct >= 0 ? X(Estilo.Verde) : Coral;
                    double bx = d.Pct >= 0 ? ejeX : ejeX - w;
                    _g.DrawRectangle(new XSolidBrush(col), bx, _y + 3, Math.Max(0, w), 11);
                    _g.DrawString(d.Pct.ToString("+0;-0;0") + "%", fV, new XSolidBrush(Gris700),
                        new XRect(d.Pct >= 0 ? ejeX + w + 3 : ejeX - w - 44, _y, 42, 16),
                        new XStringFormat { Alignment = d.Pct >= 0 ? XStringAlignment.Near : XStringAlignment.Far, LineAlignment = XLineAlignment.Center });
                    _y += 18;
                }
            }
            _y += 8;
        }

        private void DibujarTabla(DocumentoReporte.BloqueTabla t)
        {
            int n = t.Headers.Length;
            double pesoTotal = t.Pesos != null ? t.Pesos.Sum() : n;
            var anchos = new double[n];
            for (int c = 0; c < n; c++) anchos[c] = Ancho * ((t.Pesos != null ? t.Pesos[c] : 1) / pesoTotal);
            double hHeader = 24, lineaFila = 13;

            void Header()
            {
                AsegurarEspacio(hHeader + lineaFila);
                _g.DrawRectangle(new XSolidBrush(AzulOscuro), X0, _y, Ancho, hHeader);
                var f = new XFont(Fuente, 9, XFontStyle.Bold);
                {
                    double cx = X0;
                    for (int c = 0; c < n; c++)
                    {
                        var al = (t.AlinearDerecha != null && t.AlinearDerecha[c]) ? XStringAlignment.Far : XStringAlignment.Near;
                        _g.DrawString(Truncar(t.Headers[c], f, anchos[c] - 12), f, XBrushes.White,
                            new XRect(cx + 6, _y, anchos[c] - 12, hHeader), new XStringFormat { Alignment = al, LineAlignment = XLineAlignment.Center });
                        cx += anchos[c];
                    }
                }
                _y += hHeader;
            }

            Header();
            var fC = new XFont(Fuente, 9, XFontStyle.Regular);
            {
                for (int r = 0; r < t.Filas.Count; r++)
                {
                    var fila = t.Filas[r];
                    // Envuelve cada celda a su ancho real (en vez de truncar con "…"): ninguna columna
                    // pierde texto, solo ocupa más alto cuando hace falta.
                    var lineasPorCol = new System.Collections.Generic.List<string>[n];
                    int maxLineas = 1;
                    for (int c = 0; c < n; c++)
                    {
                        string val = c < fila.Length ? (fila[c] ?? "") : "";
                        lineasPorCol[c] = EnvolverTexto(val, fC, anchos[c] - 12);
                        if (lineasPorCol[c].Count > maxLineas) maxLineas = lineasPorCol[c].Count;
                    }
                    double hFila = maxLineas * lineaFila + 7;

                    if (_y + hFila > Bottom) { NuevaPagina(primera: false); Header(); }
                    if (r % 2 == 1) _g.DrawRectangle(new XSolidBrush(Gris100), X0, _y, Ancho, hFila);
                    double cx = X0;
                    for (int c = 0; c < n; c++)
                    {
                        var al = (t.AlinearDerecha != null && t.AlinearDerecha[c]) ? XStringAlignment.Far : XStringAlignment.Near;
                        double ly = _y + 4;
                        foreach (var linea in lineasPorCol[c])
                        {
                            _g.DrawString(linea, fC, new XSolidBrush(Gris700),
                                new XRect(cx + 6, ly, anchos[c] - 12, lineaFila), new XStringFormat { Alignment = al, LineAlignment = XLineAlignment.Near });
                            ly += lineaFila;
                        }
                        cx += anchos[c];
                    }
                    _g.DrawLine(new XPen(Gris200, 0.5), X0, _y + hFila, X0 + Ancho, _y + hFila);
                    _y += hFila;
                }
            }
            _y += 10;
        }

        // ── Texto: envoltura y truncado por medición real ───────────────────────────
        private System.Collections.Generic.List<string> EnvolverTexto(string texto, XFont f, double ancho)
        {
            var salida = new System.Collections.Generic.List<string>();
            foreach (var parrafo in (texto ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var palabras = parrafo.Split(' ');
                var linea = "";
                foreach (var p in palabras)
                {
                    // Palabra más ancha que una línea entera (URL/email/código sin espacios): partirla
                    // por carácter en vez de dejarla desbordar sobre el contenido vecino.
                    if (_g.MeasureString(p, f).Width > ancho)
                    {
                        if (linea.Length > 0) { salida.Add(linea); linea = ""; }
                        var trozo = "";
                        foreach (char ch in p)
                        {
                            string pruebaTrozo = trozo + ch;
                            if (_g.MeasureString(pruebaTrozo, f).Width > ancho && trozo.Length > 0)
                            {
                                salida.Add(trozo);
                                trozo = ch.ToString();
                            }
                            else trozo += ch;
                        }
                        linea = trozo;
                        continue;
                    }

                    string prueba = linea.Length == 0 ? p : linea + " " + p;
                    if (_g.MeasureString(prueba, f).Width > ancho && linea.Length > 0)
                    {
                        salida.Add(linea);
                        linea = p;
                    }
                    else linea = prueba;
                }
                salida.Add(linea);
            }
            return salida;
        }

        private string Truncar(string s, XFont f, double ancho)
        {
            s = s ?? "";
            if (_g.MeasureString(s, f).Width <= ancho) return s;
            while (s.Length > 1 && _g.MeasureString(s + "…", f).Width > ancho) s = s.Substring(0, s.Length - 1);
            return s + "…";
        }

        private static string SanearArchivo(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = (s ?? "").Replace(c, '_');
            return s;
        }
    }
}

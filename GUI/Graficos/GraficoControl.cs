using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace GUI
{
    /// <summary>
    /// PN03 — Control de visualización NATIVO (GDI+), sin librerías de terceros. Dibuja barras
    /// verticales/horizontales, torta, líneas y barras agrupadas a partir de puntos (etiqueta+valor+color)
    /// o series. Diseñado para ser LEGIBLE y adaptable:
    ///   • Métricas escaladas por DPI (<see cref="_esc"/>) y fuentes de tamaño razonable.
    ///   • Barras horizontales: reserva ancho REAL para los nombres (medido, con tope) y alto mínimo por
    ///     fila; si hay muchas categorías, el control crece y su contenedor (AutoScroll) permite recorrerlas.
    ///   • Tooltips: al pasar el mouse sobre una barra/porción/punto muestra "etiqueta: valor".
    ///   • Estado "sin datos" con mensaje centrado.
    /// Para el scroll, montar con <see cref="Montar"/> dentro de un Panel AutoScroll.
    /// </summary>
    public class GraficoControl : Panel
    {
        public enum TipoGrafico { BarrasVertical, BarrasHorizontal, Torta, Lineas, BarrasAgrupadas }

        /// <summary>Un punto del gráfico: etiqueta, valor y color de la marca.</summary>
        public sealed class Punto
        {
            public string Etiqueta { get; set; }
            public double Valor    { get; set; }
            public Color  Color    { get; set; }
            public string Sufijo   { get; set; } = "";   // p. ej. "%" para ocupación
            public Punto() { }
            public Punto(string etiqueta, double valor, Color color, string sufijo = "")
            { Etiqueta = etiqueta; Valor = valor; Color = color; Sufijo = sufijo; }
        }

        /// <summary>Serie para el gráfico de barras agrupadas (ej. "Antes" / "Después").</summary>
        public sealed class Serie
        {
            public string Nombre { get; set; } = "";
            public Color  Color  { get; set; }
            public List<double> Valores { get; set; } = new List<double>();
            public Serie() { }
            public Serie(string nombre, Color color, IEnumerable<double> valores)
            { Nombre = nombre; Color = color; Valores = new List<double>(valores ?? new double[0]); }
        }

        private string _titulo = "";
        private string _subtitulo = "";
        private TipoGrafico _tipo = TipoGrafico.BarrasVertical;
        private List<Punto> _datos = new List<Punto>();
        private List<string> _categorias = new List<string>();
        private List<Serie>  _series     = new List<Serie>();
        private double? _maxForzado;
        private readonly float _esc;                       // factor DPI (1.0 @ 96dpi)
        private readonly ToolTip _tip = new ToolTip { ShowAlways = true, AutoPopDelay = 8000 };
        private readonly List<KeyValuePair<RectangleF, string>> _hits = new List<KeyValuePair<RectangleF, string>>();
        private string _tipActual = "";
        private Panel _hostScroll;                         // contenedor AutoScroll (si se montó)

        public GraficoControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            _esc = DeviceDpi / 96f;
            Padding = new Padding(E(12));
            Font = Estilo.Normal(8.5f);
        }

        private int E(float px) => (int)Math.Round(px * _esc);   // escala por DPI

        public string Titulo { get => _titulo; set { _titulo = value ?? ""; Invalidate(); } }
        public string Subtitulo { get => _subtitulo; set { _subtitulo = value ?? ""; Invalidate(); } }
        public TipoGrafico Tipo { get => _tipo; set { _tipo = value; Reajustar(); Invalidate(); } }
        public double? MaxForzado { get => _maxForzado; set { _maxForzado = value; Invalidate(); } }

        public void SetDatos(IEnumerable<Punto> puntos)
        {
            _datos = puntos?.Where(p => p != null).ToList() ?? new List<Punto>();
            Reajustar();
            Invalidate();
        }

        public void SetSeries(IEnumerable<string> categorias, params Serie[] series)
        {
            _categorias = categorias?.ToList() ?? new List<string>();
            _series = (series ?? new Serie[0]).Where(s => s != null).ToList();
            Reajustar();
            Invalidate();
        }

        public static readonly Color[] Paleta =
        {
            Estilo.AzulDeSeleccion, Estilo.Coral, Estilo.Naranja, Estilo.Verde,
            Estilo.Amarillo, Color.FromArgb(139, 92, 246), Color.FromArgb(20, 184, 166),
            Color.FromArgb(217, 70, 239)
        };
        public static Color ColorPorIndice(int i) => Paleta[((i % Paleta.Length) + Paleta.Length) % Paleta.Length];

        // ── Montaje con scroll ────────────────────────────────────────────────────
        /// <summary>Monta el gráfico dentro de un Panel con AutoScroll: el gráfico se dimensiona al
        /// contenido (alto real por categoría en barras H), y el panel permite recorrer todas las
        /// categorías sin comprimirlas. Reemplaza el contenido del host.</summary>
        public static GraficoControl Montar(Panel host, GraficoControl g)
        {
            host.AutoScroll = true;
            host.Controls.Clear();
            g._hostScroll = host;
            g.Location = new Point(0, 0);
            host.Controls.Add(g);
            host.SizeChanged += (s, e) => g.Reajustar();
            g.Reajustar();
            return g;
        }

        private void Reajustar()
        {
            if (_hostScroll == null) return;
            var vis = _hostScroll.ClientSize;
            if (vis.Width < 8 || vis.Height < 8) return;
            Size = MedirPreferido(vis);
        }

        /// <summary>Tamaño que el gráfico necesita dado el área visible del contenedor. Para barras con
        /// muchas categorías devuelve un tamaño MAYOR al visible en el eje de las categorías (dispara el
        /// scroll); para el resto, llena el área visible.</summary>
        public Size MedirPreferido(Size visible)
        {
            int header = Padding.Vertical
                + (string.IsNullOrEmpty(_titulo) ? 0 : E(24))
                + (string.IsNullOrEmpty(_subtitulo) ? 0 : E(17));
            if (_tipo == TipoGrafico.BarrasHorizontal)
            {
                int n = Math.Max(1, _datos.Count);
                int alto = header + n * E(34) + E(16);
                return new Size(visible.Width, Math.Max(visible.Height, alto));
            }
            if (_tipo == TipoGrafico.BarrasVertical)
            {
                int n = Math.Max(1, _datos.Count);
                int ancho = Padding.Horizontal + n * E(56);
                return new Size(Math.Max(visible.Width, ancho), visible.Height);
            }
            if (_tipo == TipoGrafico.Lineas)
            {
                int n = Math.Max(1, _datos.Count);
                int ancho = Padding.Horizontal + n * E(52);
                return new Size(Math.Max(visible.Width, ancho), visible.Height);
            }
            return visible;   // torta / agrupadas: llenan el área
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            _hits.Clear();
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(Padding.Left, Padding.Top,
                Math.Max(1, Width - Padding.Horizontal), Math.Max(1, Height - Padding.Vertical));

            float yTop = rect.Top;
            if (!string.IsNullOrEmpty(_titulo))
            {
                using (var fTit = Estilo.SemiBold(10.5f))
                using (var br = new SolidBrush(Estilo.AzulOscuro))
                    g.DrawString(_titulo, fTit, br, rect.Left, yTop);
                yTop += E(23);
            }
            if (!string.IsNullOrEmpty(_subtitulo))
            {
                using (var fSub = Estilo.Normal(8f))
                using (var br = new SolidBrush(Estilo.Gris400))
                    g.DrawString(_subtitulo, fSub, br, rect.Left, yTop);
                yTop += E(16);
            }

            var area = Rectangle.FromLTRB(rect.Left, (int)yTop + E(4), rect.Right, rect.Bottom);

            if (SinDatos())
            {
                using (var f = Estilo.Normal(10f))
                using (var br = new SolidBrush(Estilo.Gris400))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(I18n.T("eh.graf.sindatos", "Sin datos en el período"), f, br, area, sf);
                return;
            }

            switch (_tipo)
            {
                case TipoGrafico.BarrasHorizontal: DibujarBarrasH(g, area); break;
                case TipoGrafico.Torta:            DibujarTorta(g, area);   break;
                case TipoGrafico.Lineas:           DibujarLineas(g, area);  break;
                case TipoGrafico.BarrasAgrupadas:  DibujarBarrasAgrupadas(g, area); break;
                default:                           DibujarBarrasV(g, area); break;
            }
        }

        private bool SinDatos()
        {
            if (_tipo == TipoGrafico.BarrasAgrupadas)
                return _categorias.Count == 0 || _series.Count == 0 || _series.All(s => s.Valores.All(v => v <= 0));
            if (_tipo == TipoGrafico.Lineas) return _datos.Count == 0;
            return _datos.Count == 0 || _datos.All(p => p.Valor <= 0);
        }

        private double MaxValor() => _maxForzado ?? Math.Max(1d, _datos.Max(p => p.Valor));

        // ── Barras horizontales (recomendado para nombres de entidades) ───────────
        private void DibujarBarrasH(Graphics g, Rectangle area)
        {
            double max = MaxValor();
            int n = _datos.Count;
            float filaH = area.Height / (float)n;
            if (filaH > E(46)) filaH = E(46);

            using (var fLbl = Estilo.Normal(9f))
            using (var fVal = Estilo.Bold(9f))
            {
                // Ancho de etiquetas: medido real, con tope (42% del área) y piso.
                float maxMed = 0;
                foreach (var p in _datos) maxMed = Math.Max(maxMed, g.MeasureString(p.Etiqueta ?? "", fLbl).Width);
                float labelW = Math.Min(area.Width * 0.42f, Math.Max(E(80), maxMed + E(10)));
                float valW = E(52);
                float x0 = area.Left + labelW + E(8);
                float anchoBarras = area.Right - x0 - valW;
                if (anchoBarras < E(40)) anchoBarras = Math.Max(E(40), area.Width * 0.3f);

                using (var brTxt = new SolidBrush(Estilo.Gris700))
                using (var penBase = new Pen(Estilo.Gris200))
                using (var sfR = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                using (var sfVal = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                {
                    g.DrawLine(penBase, x0, area.Top, x0, area.Top + filaH * n);
                    for (int i = 0; i < n; i++)
                    {
                        var p = _datos[i];
                        float cy = area.Top + filaH * i + filaH / 2f;
                        float w = (float)(anchoBarras * (p.Valor / max));
                        if (w < 1 && p.Valor > 0) w = 1;
                        float barH = Math.Min(E(22), filaH * 0.62f);
                        g.DrawString(p.Etiqueta, fLbl, brTxt, new RectangleF(area.Left, cy - filaH / 2f, labelW, filaH), sfR);
                        var barRect = new RectangleF(x0, cy - barH / 2f, Math.Max(0, w), barH);
                        using (var br = new SolidBrush(p.Color))
                            g.FillRectangle(br, barRect);
                        g.DrawString(Formatear(p), fVal, brTxt, new RectangleF(x0 + Math.Max(0, w) + E(5), cy - filaH / 2f, valW + E(10), filaH), sfVal);
                        _hits.Add(new KeyValuePair<RectangleF, string>(
                            new RectangleF(area.Left, cy - filaH / 2f, area.Width, filaH),
                            $"{p.Etiqueta}: {Formatear(p)}"));
                    }
                }
            }
        }

        // ── Barras verticales ─────────────────────────────────────────────────────
        private void DibujarBarrasV(Graphics g, Rectangle area)
        {
            double max = MaxValor();
            int n = _datos.Count;
            float ejeY = area.Bottom - E(38);
            float ejeTop = area.Top + E(8);
            float alto = ejeY - ejeTop;
            float ancho = area.Width / (float)n;
            float barW = Math.Min(E(56), ancho * 0.6f);

            using (var fLbl = Estilo.Normal(8f))
            using (var fVal = Estilo.Bold(8.5f))
            using (var brTxt = new SolidBrush(Estilo.Gris700))
            using (var penEje = new Pen(Estilo.Gris200))
            using (var sfC = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawLine(penEje, area.Left, ejeY, area.Right, ejeY);
                for (int i = 0; i < n; i++)
                {
                    var p = _datos[i];
                    float cx = area.Left + ancho * i + ancho / 2f;
                    float h = (float)(alto * (p.Valor / max));
                    if (h < 1 && p.Valor > 0) h = 1;
                    using (var br = new SolidBrush(p.Color))
                        g.FillRectangle(br, cx - barW / 2f, ejeY - h, barW, h);
                    g.DrawString(Formatear(p), fVal, brTxt, new RectangleF(cx - ancho / 2f, ejeY - h - E(17), ancho, E(15)), sfC);
                    g.DrawString(p.Etiqueta, fLbl, brTxt, new RectangleF(cx - ancho / 2f, ejeY + E(4), ancho, E(32)), sfC);
                    _hits.Add(new KeyValuePair<RectangleF, string>(
                        new RectangleF(cx - ancho / 2f, area.Top, ancho, area.Height), $"{p.Etiqueta}: {Formatear(p)}"));
                }
            }
        }

        // ── Torta ─────────────────────────────────────────────────────────────────
        private void DibujarTorta(Graphics g, Rectangle area)
        {
            double total = _datos.Sum(p => p.Valor);
            if (total <= 0) return;

            float d = Math.Min(area.Height - E(10), area.Width * 0.5f);
            var circ = new RectangleF(area.Left + E(6), area.Top + (area.Height - d) / 2f, d, d);
            float ini = -90f;
            for (int i = 0; i < _datos.Count; i++)
            {
                float barrido = (float)(360.0 * _datos[i].Valor / total);
                using (var br = new SolidBrush(_datos[i].Color))
                    g.FillPie(br, circ.X, circ.Y, circ.Width, circ.Height, ini, barrido);
                ini += barrido;
            }
            float lx = circ.Right + E(18);
            float ly = area.Top + E(6);
            using (var fLbl = Estilo.Normal(9f))
            using (var brTxt = new SolidBrush(Estilo.Gris700))
            {
                for (int i = 0; i < _datos.Count; i++)
                {
                    var p = _datos[i];
                    using (var br = new SolidBrush(p.Color))
                        g.FillRectangle(br, lx, ly + E(2), E(13), E(13));
                    double pct = 100.0 * p.Valor / total;
                    g.DrawString($"{p.Etiqueta}: {p.Valor:0} ({pct:0}%)", fLbl, brTxt, lx + E(19), ly);
                    _hits.Add(new KeyValuePair<RectangleF, string>(
                        new RectangleF(lx, ly, area.Right - lx, E(20)), $"{p.Etiqueta}: {p.Valor:0} ({pct:0}%)"));
                    ly += E(22);
                }
            }
        }

        // ── Líneas ──────────────────────────────────────────────────────────────
        private void DibujarLineas(Graphics g, Rectangle area)
        {
            int n = _datos.Count;
            double max = _maxForzado ?? Math.Max(1d, _datos.Max(p => p.Valor));
            float ejeY = area.Bottom - E(28);
            float ejeTop = area.Top + E(16);
            float alto = ejeY - ejeTop;
            float ancho = area.Width - E(10);
            float x0 = area.Left + E(6);
            Func<int, float> cx = i => n <= 1 ? area.Left + area.Width / 2f : x0 + ancho * i / (n - 1);
            Func<double, float> cy = v => ejeY - (float)(alto * (v / max));

            using (var penEje = new Pen(Estilo.Gris200))
            using (var penLinea = new Pen(Estilo.AzulDeSeleccion, 2.2f))
            using (var brPunto = new SolidBrush(Estilo.AzulDeSeleccion))
            using (var fLbl = Estilo.Normal(8f))
            using (var fVal = Estilo.Bold(8f))
            using (var brTxt = new SolidBrush(Estilo.Gris700))
            using (var sfC = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawLine(penEje, area.Left, ejeY, area.Right, ejeY);
                var pts = new PointF[n];
                for (int i = 0; i < n; i++) pts[i] = new PointF(cx(i), cy(_datos[i].Valor));
                if (n >= 2) g.DrawLines(penLinea, pts);
                for (int i = 0; i < n; i++)
                {
                    g.FillEllipse(brPunto, pts[i].X - E(4), pts[i].Y - E(4), E(8), E(8));
                    g.DrawString(((long)Math.Round(_datos[i].Valor)).ToString(), fVal, brTxt,
                        new RectangleF(pts[i].X - E(22), pts[i].Y - E(18), E(44), E(14)), sfC);
                    g.DrawString(_datos[i].Etiqueta, fLbl, brTxt,
                        new RectangleF(cx(i) - E(28), ejeY + E(4), E(56), E(20)), sfC);
                    _hits.Add(new KeyValuePair<RectangleF, string>(
                        new RectangleF(cx(i) - E(20), area.Top, E(40), area.Height),
                        $"{_datos[i].Etiqueta}: {_datos[i].Valor:0}"));
                }
            }
        }

        // ── Barras agrupadas (Antes/Después) ──────────────────────────────────────
        private void DibujarBarrasAgrupadas(Graphics g, Rectangle area)
        {
            int nCat = _categorias.Count;
            int nSer = _series.Count;
            double max = _maxForzado ?? Math.Max(1d, _series.SelectMany(s => s.Valores).DefaultIfEmpty(0).Max());
            float ejeY = area.Bottom - E(34);
            float ejeTop = area.Top + E(26);
            float alto = ejeY - ejeTop;
            float anchoGrupo = area.Width / (float)Math.Max(1, nCat);
            float barW = Math.Min(E(30), (anchoGrupo * 0.7f) / Math.Max(1, nSer));

            using (var penEje = new Pen(Estilo.Gris200))
            using (var fLbl = Estilo.Normal(8f))
            using (var fVal = Estilo.Bold(8f))
            using (var brTxt = new SolidBrush(Estilo.Gris700))
            using (var sfC = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                float lx = area.Left;
                for (int s = 0; s < nSer; s++)
                {
                    using (var br = new SolidBrush(_series[s].Color))
                        g.FillRectangle(br, lx, area.Top + E(4), E(12), E(12));
                    using (var brL = new SolidBrush(Estilo.Gris700))
                        g.DrawString(_series[s].Nombre, fLbl, brL, lx + E(16), area.Top + E(2));
                    lx += E(16) + Math.Max(E(55), g.MeasureString(_series[s].Nombre, fLbl).Width + E(20));
                }

                g.DrawLine(penEje, area.Left, ejeY, area.Right, ejeY);
                for (int c = 0; c < nCat; c++)
                {
                    float gc = area.Left + anchoGrupo * c + anchoGrupo / 2f;
                    float startX = gc - (barW * nSer) / 2f;
                    for (int s = 0; s < nSer; s++)
                    {
                        double val = c < _series[s].Valores.Count ? _series[s].Valores[c] : 0;
                        float hh = (float)(alto * (val / max));
                        if (hh < 1 && val > 0) hh = 1;
                        float bx = startX + barW * s;
                        using (var br = new SolidBrush(_series[s].Color))
                            g.FillRectangle(br, bx, ejeY - hh, barW, hh);
                        g.DrawString(Entero(val), fVal, brTxt, new RectangleF(bx - E(6), ejeY - hh - E(14), barW + E(12), E(13)), sfC);
                    }
                    g.DrawString(_categorias[c], fLbl, brTxt, new RectangleF(gc - anchoGrupo / 2f, ejeY + E(5), anchoGrupo, E(26)), sfC);
                }
            }
        }

        // ── Tooltips ──────────────────────────────────────────────────────────────
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string hit = "";
            for (int i = _hits.Count - 1; i >= 0; i--)
                if (_hits[i].Key.Contains(e.Location)) { hit = _hits[i].Value; break; }
            if (hit != _tipActual)
            {
                _tipActual = hit;
                _tip.SetToolTip(this, hit);
            }
        }

        private static string Entero(double v) => ((long)Math.Round(v)).ToString();

        private static string Formatear(Punto p)
        {
            string v = Math.Abs(p.Valor - Math.Round(p.Valor)) < 0.05 ? ((long)Math.Round(p.Valor)).ToString() : p.Valor.ToString("0.0");
            return v + (p.Sufijo ?? "");
        }
    }
}

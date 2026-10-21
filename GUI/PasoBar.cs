using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GUI
{
    /// <summary>
    /// Barra de pasos horizontal tipo asistente (wizard), pintada a mano con la paleta de
    /// ExperienceHub. Muestra N pasos como "pills" con un círculo numerado + etiqueta; resalta el
    /// paso actual (Coral), marca los completados (✓) y atenúa los pendientes. Sin dependencias
    /// externas; se redibuja al cambiar <see cref="Current"/> o el tamaño.
    /// </summary>
    internal sealed class PasoBar : Panel
    {
        private string[] _pasos = new string[0];
        private int _current;

        public PasoBar()
        {
            Height = 66;
            DoubleBuffered = true;
            BackColor = Estilo.Lienzo;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        /// <summary>Etiquetas de los pasos, en orden.</summary>
        public string[] Pasos { get => _pasos; set { _pasos = value ?? new string[0]; Invalidate(); } }

        /// <summary>Paso actual (0-based).</summary>
        public int Current { get => _current; set { _current = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_pasos.Length == 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int n = _pasos.Length;
            int pad = 16;
            int gap = 12;
            int pillH = 40;
            int totalGap = gap * (n - 1);
            int pillW = (Width - pad * 2 - totalGap) / n;
            int y = (Height - pillH) / 2;

            using (var fNum  = Estilo.SemiBold(10f))
            using (var fTxt  = Estilo.Normal(10f))
            using (var fTxtB = Estilo.SemiBold(10f))
            {
                for (int i = 0; i < n; i++)
                {
                    int x = pad + i * (pillW + gap);
                    var rect = new Rectangle(x, y, pillW, pillH);

                    bool activo    = i == _current;
                    bool completado = i < _current;

                    Color fondo = activo ? Estilo.Coral
                                 : completado ? Estilo.Gris100
                                 : Estilo.BlancoCalido;
                    Color borde = activo ? Estilo.Coral
                                 : completado ? Estilo.Verde
                                 : Estilo.Gris200;
                    Color txtCol = activo ? Color.White
                                 : completado ? Estilo.Gris700
                                 : Estilo.Gris400;

                    using (var path = Redondeado(rect, 20))
                    using (var bFondo = new SolidBrush(fondo))
                    using (var pBorde = new Pen(borde, activo ? 2f : 1.5f))
                    {
                        g.FillPath(bFondo, path);
                        g.DrawPath(pBorde, path);
                    }

                    // Círculo numerado a la izquierda del pill.
                    int d = 24;
                    var circ = new Rectangle(x + 10, y + (pillH - d) / 2, d, d);
                    Color circBg = activo ? Color.White : (completado ? Estilo.Verde : Estilo.Gris200);
                    Color circFg = activo ? Estilo.Coral : (completado ? Color.White : Estilo.Gris700);
                    using (var bCirc = new SolidBrush(circBg))
                        g.FillEllipse(bCirc, circ);
                    string num = completado ? "✓" : (i + 1).ToString();
                    using (var bNum = new SolidBrush(circFg))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(num, fNum, bNum, circ, sf);

                    // Etiqueta del paso a la derecha del círculo.
                    var txtRect = new Rectangle(circ.Right + 8, y, rect.Right - circ.Right - 14, pillH);
                    using (var bTxt = new SolidBrush(txtCol))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(_pasos[i], activo ? fTxtB : fTxt, bTxt, txtRect, sf);
                }
            }
        }

        private static GraphicsPath Redondeado(Rectangle r, int radio)
        {
            int d = radio * 2;
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

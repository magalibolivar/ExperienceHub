using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GUI
{
    /// <summary>
    /// Tarjeta blanca con esquinas redondeadas y borde suave — "Nivel 1" (contenedor) del
    /// sistema de diseño ExperienceHub/Stitch. Se usa para KPIs y contenedores del Dashboard.
    /// Pintado con GDI+ anti-aliasing (sin sombras pesadas, que renderizan mal en WinForms).
    /// </summary>
    internal class TarjetaPanel : Panel
    {
        public int Radio { get; set; } = 12;
        public Color BordeColor { get; set; } = Estilo.Gris200;
        public float BordeGrosor { get; set; } = 1f;   // se engrosa al enfocar (estado de foco)
        public Color RellenoColor { get; set; } = Color.White;
        /// <summary>Franja de acento a la izquierda (0 = sin franja). Emula el realce de las tarjetas.</summary>
        public int FranjaAncho { get; set; } = 0;
        public Color FranjaColor { get; set; } = Estilo.Coral;

        public TarjetaPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            // Pinta primero el fondo del ancestro opaco para que las esquinas (fuera del path)
            // no queden negras cuando el panel es "transparente".
            g.Clear(FondoAncestro());
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Redondeado(rect, Radio))
            using (var fill = new SolidBrush(RellenoColor))
            using (var pen = new Pen(BordeColor, BordeGrosor))
            {
                g.FillPath(fill, path);
                if (FranjaAncho > 0)
                {
                    var clip = g.Clip;
                    g.SetClip(path);
                    using (var fb = new SolidBrush(FranjaColor))
                        g.FillRectangle(fb, 0, 0, FranjaAncho, Height);
                    g.Clip = clip;
                }
                g.DrawPath(pen, path);
            }
            base.OnPaint(e);
        }

        /// <summary>Color de fondo del primer ancestro con color opaco (para pintar las esquinas).</summary>
        private Color FondoAncestro()
        {
            Control p = Parent;
            while (p != null)
            {
                if (p.BackColor.A == 255 && p.BackColor != Color.Transparent) return p.BackColor;
                p = p.Parent;
            }
            return Estilo.Lienzo;
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

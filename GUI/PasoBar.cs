using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GUI
{
    /// <summary>
    /// Barra de pasos horizontal tipo asistente (wizard), pintada a mano con la paleta de
    /// ExperienceHub. Muestra N pasos como "pills" con un círculo numerado + etiqueta.
    ///
    /// Dos modos (el segundo NO rompe el primero):
    ///  • SIMPLE (PN01/PN02): se setea <see cref="Pasos"/> + <see cref="Current"/>. El paso actual va
    ///    en Coral, los anteriores como completados (✓) y los siguientes atenuados. No es clickeable.
    ///  • RICO (PN03): además se setea <see cref="Estados"/> (un <see cref="EstadoPaso"/> por paso) para
    ///    reflejar el estado REAL del trabajo (actual / completado / futuro / bloqueado), un badge de
    ///    "pendientes" por paso (<see cref="Pendientes"/>) y navegación por clic (<see cref="PasoClicado"/>).
    ///    El color NUNCA es la única señal: hay número, ✓ (completado), candado (bloqueado) y etiqueta.
    /// </summary>
    internal sealed class PasoBar : Panel
    {
        public enum EstadoPaso { Futuro, Actual, Completado, Bloqueado }

        private string[] _pasos = new string[0];
        private EstadoPaso[] _estados;      // null = modo simple (derivado de Current)
        private bool[] _pendientes;
        private int _current;
        private int _seccion = -1;          // sección que el usuario está CONSULTANDO (marca azul); -1 = ninguna

        /// <summary>Se dispara al hacer clic en un paso NO bloqueado (índice 0-based). El formulario
        /// decide si la navegación es válida; la barra no ejecuta ninguna regla por sí misma.</summary>
        public event Action<int> PasoClicado;

        public PasoBar()
        {
            Height = 66;
            DoubleBuffered = true;
            BackColor = Estilo.Lienzo;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public string[] Pasos { get => _pasos; set { _pasos = value ?? new string[0]; Invalidate(); } }
        public int Current { get => _current; set { _current = value; Invalidate(); } }

        /// <summary>Estado explícito por paso (modo rico). Si es null, se usa <see cref="Current"/>.</summary>
        public EstadoPaso[] Estados { get => _estados; set { _estados = value; Invalidate(); } }

        /// <summary>Badge discreto de "operaciones pendientes" por paso (modo rico).</summary>
        public bool[] Pendientes { get => _pendientes; set { _pendientes = value; Invalidate(); } }

        /// <summary>Sección que el usuario está CONSULTANDO ahora (se marca con un indicador azul debajo del
        /// pill). Es INDEPENDIENTE del progreso: ver una etapa no cambia su estado. -1 = ninguna.</summary>
        public int Seccion { get => _seccion; set { _seccion = value; Invalidate(); } }

        private EstadoPaso EstadoDe(int i)
        {
            if (_estados != null && i < _estados.Length) return _estados[i];
            if (i == _current) return EstadoPaso.Actual;
            return i < _current ? EstadoPaso.Completado : EstadoPaso.Futuro;
        }

        private bool PendienteDe(int i) => _pendientes != null && i < _pendientes.Length && _pendientes[i];

        // Geometría de un pill (compartida por Paint y hit-testing). El alto del pill se adapta a la
        // altura del control (deja ~22px para la marca de sección), con tope 40 → no cambia a 66px (Reservas).
        private Rectangle RectDe(int i, int n)
        {
            int pad = 16, gap = 12;
            int pillH = Math.Max(26, Math.Min(40, Height - 22));
            int pillW = (Width - pad * 2 - gap * (n - 1)) / Math.Max(1, n);
            int y = Math.Max(2, (Height - pillH) / 2 - 3);
            return new Rectangle(pad + i * (pillW + gap), y, pillW, pillH);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int n = _pasos.Length;
            for (int i = 0; i < n; i++)
                if (RectDe(i, n).Contains(e.Location))
                {
                    if (EstadoDe(i) != EstadoPaso.Bloqueado) PasoClicado?.Invoke(i);
                    return;
                }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_estados == null && PasoClicado == null) { Cursor = Cursors.Default; return; }
            int n = _pasos.Length;
            var cur = Cursors.Default;
            for (int i = 0; i < n; i++)
                if (RectDe(i, n).Contains(e.Location) && EstadoDe(i) != EstadoPaso.Bloqueado) { cur = Cursors.Hand; break; }
            Cursor = cur;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_pasos.Length == 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int n = _pasos.Length;

            using (var fNum = Estilo.SemiBold(10f))
            using (var fTxt = Estilo.Normal(10f))
            using (var fTxtB = Estilo.SemiBold(10f))
            {
                for (int i = 0; i < n; i++)
                {
                    var rect = RectDe(i, n);
                    var est = EstadoDe(i);
                    bool activo = est == EstadoPaso.Actual;
                    bool completado = est == EstadoPaso.Completado;
                    bool bloqueado = est == EstadoPaso.Bloqueado;

                    // "Actual" usa AZUL (progreso/navegación), NO Coral: Coral es prácticamente idéntico
                    // a Rojo (peligro/error) y un paso normal del recorrido no debe leerse como alerta.
                    Color fondo = activo ? Estilo.AzulDeSeleccion : completado ? Estilo.Gris100 : Estilo.BlancoCalido;
                    Color borde = activo ? Estilo.AzulDeSeleccion : completado ? Estilo.Verde : Estilo.Gris200;
                    Color txtCol = activo ? Color.White : completado ? Estilo.Gris700 : bloqueado ? Estilo.Gris400 : Estilo.Gris700;

                    using (var path = Redondeado(rect, 20))
                    using (var bFondo = new SolidBrush(fondo))
                    using (var pBorde = new Pen(borde, activo ? 2f : 1.5f))
                    {
                        g.FillPath(bFondo, path);
                        g.DrawPath(pBorde, path);
                    }

                    // Círculo con el indicador de estado (número / ✓ / candado).
                    int d = 24;
                    var circ = new Rectangle(rect.X + 10, rect.Y + (rect.Height - d) / 2, d, d);
                    Color circBg = activo ? Color.White : completado ? Estilo.Verde : Estilo.Gris200;
                    Color circFg = activo ? Estilo.AzulDeSeleccion : completado ? Color.White : Estilo.Gris700;
                    using (var bCirc = new SolidBrush(circBg))
                        g.FillEllipse(bCirc, circ);

                    if (bloqueado)
                        DibujarCandado(g, circ, Estilo.Gris400);
                    else
                    {
                        string ind = completado ? "✓" : (i + 1).ToString();
                        using (var bNum = new SolidBrush(circFg))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString(ind, fNum, bNum, circ, sf);
                    }

                    // Etiqueta.
                    var txtRect = new Rectangle(circ.Right + 8, rect.Y, rect.Right - circ.Right - 14, rect.Height);
                    using (var bTxt = new SolidBrush(txtCol))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(_pasos[i], activo ? fTxtB : fTxt, bTxt, txtRect, sf);

                    // Badge discreto de "pendientes" (punto naranja arriba a la derecha).
                    if (PendienteDe(i) && !bloqueado)
                        using (var bBadge = new SolidBrush(Estilo.Naranja))
                            g.FillEllipse(bBadge, rect.Right - 16, rect.Y + 6, 9, 9);

                    // Marca de SECCIÓN consultada: indicador azul debajo del pill (tipo "tab"), INDEPENDIENTE
                    // del color de estado — ver una etapa no altera su progreso.
                    if (i == _seccion)
                    {
                        int mw = (int)(rect.Width * 0.5f);
                        var marca = new Rectangle(rect.X + (rect.Width - mw) / 2, rect.Bottom + 5, mw, 4);
                        using (var path = Redondeado(marca, 2))
                        using (var bMarca = new SolidBrush(Estilo.AzulDeSeleccion))
                            g.FillPath(bMarca, path);
                    }
                }
            }
        }

        // Candado simple dibujado a mano (evita depender de fuentes de emoji).
        private static void DibujarCandado(Graphics g, Rectangle circ, Color color)
        {
            int w = 11, h = 8;
            int x = circ.X + (circ.Width - w) / 2;
            int y = circ.Y + (circ.Height - h) / 2 + 2;
            using (var pen = new Pen(color, 1.6f))
            {
                g.DrawArc(pen, x + 1, y - 7, w - 2, 9, 180, 180);   // arco del candado
                using (var br = new SolidBrush(color))
                    g.FillRectangle(br, x, y, w, h);                 // cuerpo
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

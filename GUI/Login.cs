using BLL;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// ExperienceHub — Login de Empleados. Rediseño 1:1 con el mockup de marca:
    /// panel izquierdo con foto de atardecer + tarjeta glass (logo + marca + slogan);
    /// panel derecho con "Portal de Administración", campos con ícono y placeholder,
    /// botón coral "Ingresar", link de contraseña y footer idioma/versión.
    ///
    /// PATRÓN OBSERVER — T05: implementa IIdiomaObserver (se suscribe al GestorIdioma).
    /// Toda la lógica de autenticación/seguridad se conserva intacta.
    /// </summary>
    public partial class Login : Form, IIdiomaObserver
    {
        private readonly Usuario usuarioBLL = new Usuario();

        private ComboBox _cmbIdiomaLogin;
        private bool _suprimirIdiomaChange = false;
        private LinkLabel _lnkEmergencia;

        private bool _uiBuilt;
        private TarjetaPanel _fUser, _fPass;
        private Label _lblIdioma, _lblVersion;
        private Image _bgImg, _markImg;
        private bool _btnHover;

        // Placeholder nativo (cue banner) para los TextBox — .NET Framework no trae PlaceholderText.
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;
        private static void SetPlaceholder(TextBox tb, string text)
        {
            try { SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, text); } catch { }
        }

        private const string REG_KEY = @"Software\ExperienceHub";

        public Login()
        {
            InitializeComponent();
            try { string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico"); if (File.Exists(ico)) this.Icon = new Icon(ico); } catch { }

            pnlLeft.Paint += PnlLeft_Paint;
            // Decoración circular (estética WardrobeFlow) sobre el fondo del panel derecho —
            // se dibuja DETRÁS de los controles (opacos, van encima). No intercepta clics.
            pnlCard.Paint  += PnlCard_Paint;
            pnlCard.Resize += (s, e) => pnlCard.Invalidate();
            this.AcceptButton = btnIngresar;
        }

        // ── Construcción del layout (en OnLoad, después de Estilo.Aplicar) ────────────────────
        private void ConstruirUI()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;

            try { string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login-bg.png");   if (File.Exists(p)) _bgImg   = Image.FromFile(p); } catch { }
            try { string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login-mark.png"); if (File.Exists(p)) _markImg = Image.FromFile(p); } catch { }

            // Ventana más grande, split 50/50 (foto | formulario), como el mockup.
            this.ClientSize    = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            pnlLeft.Location = new Point(0, 0);   pnlLeft.Size = new Size(500, 600);
            pnlCard.Location = new Point(500, 0); pnlCard.Size = new Size(500, 600);
            pnlCard.BackColor = Color.White;

            // Controles del layout viejo que no van.
            lblTitle.Visible = false; lblSubtitulo.Visible = false;
            lblDivider.Visible = false; btnSalir.Visible = false;

            const int X = 60, W = 380;

            // Encabezado.
            lblAccent.Location = new Point(X, 122); lblAccent.Size = new Size(430, 40);
            lblAccent.Font = Estilo.SemiBold(22f); lblAccent.ForeColor = Estilo.AzulOscuro;
            lblAccent.TextAlign = ContentAlignment.MiddleLeft;
            lblLoginSub.Visible = false;

            // Campos con ícono (Segoe MDL2 Assets) y placeholder.
            _fUser = CampoRedondeado(new Point(X, 214), W,"");   // contacto
            ReubicarTextBox(txtUsuario, _fUser, "Usuario", conAccion: false, tagKey: "lbl.usuario");
            _fPass = CampoRedondeado(new Point(X, 276), W,"");   // candado
            ReubicarTextBox(txtContraseña, _fPass, "Contraseña", conAccion: true, tagKey: "lbl.contrasena");
            txtContraseña.PasswordChar = '●';

            // Mostrar/ocultar contraseña (ojito) — misma funcionalidad que el login de WardrobeFlow.
            var btnOjo = new Label
            {
                Text = ((char)0xE7B3).ToString(), Font = new Font("Segoe MDL2 Assets", 11f),
                AutoSize = false, Size = new Size(30, 26), Location = new Point(_fPass.Width - 38, 11),
                TextAlign = ContentAlignment.MiddleCenter, ForeColor = Estilo.Gris700,
                BackColor = Color.White, Cursor = Cursors.Hand
            };
            btnOjo.Click += (s, e) =>
            {
                if (txtContraseña.PasswordChar == '\0') { txtContraseña.PasswordChar = '●'; btnOjo.ForeColor = Estilo.Gris700; }
                else                                     { txtContraseña.PasswordChar = '\0'; btnOjo.ForeColor = Estilo.Coral; }
                txtContraseña.Focus();
            };
            _fPass.Controls.Add(btnOjo);
            btnOjo.BringToFront();

            // Botón "Ingresar" (coral sólido redondeado).
            lblUsuario.Visible = false; lblContraseña.Visible = false;
            btnIngresar.Location = new Point(X, 376); btnIngresar.Size = new Size(W, 50);
            btnIngresar.FlatStyle = FlatStyle.Flat; btnIngresar.FlatAppearance.BorderSize = 0;
            btnIngresar.BackColor = Color.White; btnIngresar.Cursor = Cursors.Hand;
            btnIngresar.MouseEnter += (s, e) => { _btnHover = true;  btnIngresar.Invalidate(); };
            btnIngresar.MouseLeave += (s, e) => { _btnHover = false; btnIngresar.Invalidate(); };
            btnIngresar.Paint += BtnIngresar_Paint;

            // Link "¿Olvidaste tu contraseña?" centrado.
            lnkOlvidaste.Location = new Point(X, 440); lnkOlvidaste.Size = new Size(W, 22);
            lnkOlvidaste.TextAlign = ContentAlignment.MiddleCenter;
            lnkOlvidaste.Font = Estilo.Normal(10f);
            lnkOlvidaste.LinkColor = Color.FromArgb(47, 111, 191);
            lnkOlvidaste.ActiveLinkColor = Color.FromArgb(47, 111, 191);

            // Link de emergencia (RF-10): oculto; aparece solo si la cuenta queda bloqueada.
            _lnkEmergencia = new LinkLabel
            {
                Text = "¿Cuenta bloqueada? Usar clave de emergencia",
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(X, 510), Size = new Size(W, 18), Visible = false,
                BackColor = Color.White, Font = Estilo.Normal(8.25f),
                LinkColor = Estilo.Coral, ActiveLinkColor = Estilo.Coral, Tag = "emg.link"
            };
            _lnkEmergencia.LinkClicked += LnkEmergencia_LinkClicked;
            pnlCard.Controls.Add(_lnkEmergencia);

            // Cartel de error.
            lblError.Location = new Point(X, 466); lblError.Size = new Size(W, 24); lblError.BackColor = Color.White;

            // Footer centrado y alineado: "Idioma  [combo]  |  Versión 1.0.0" — misma línea base,
            // misma tipografía/tamaño y mismo color (sin bloques sueltos).
            const int footY = 556, footH = 24;
            _lblIdioma = new Label
            {
                Text = "Idioma", Tag = "lbl.idioma", AutoSize = false, Size = new Size(56, footH), Location = new Point(108, footY),
                TextAlign = ContentAlignment.MiddleRight, ForeColor = Estilo.Gris700, BackColor = Color.White,
                Font = Estilo.Normal(9f)
            };
            pnlCard.Controls.Add(_lblIdioma);
            AgregarComboIdioma();   // crea _cmbIdiomaLogin en el footer (alineado con las labels)
            _lblVersion = new Label
            {
                Text = "|   Versión 1.0.0", AutoSize = false, Size = new Size(130, footH), Location = new Point(266, footY),
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = Estilo.Gris700, BackColor = Color.White,
                Font = Estilo.Normal(9f)
            };
            pnlCard.Controls.Add(_lblVersion);

            CargarRecordado();
            Estilo.AplicarFuente(this);   // Montserrat en los controles nuevos (saltea íconos)
        }

        // Campo redondeado blanco con ícono a la izquierda (realce coral al enfocar).
        private TarjetaPanel CampoRedondeado(Point loc, int w, string glyph)
        {
            var p = new TarjetaPanel
            {
                Location = loc, Size = new Size(w, 48), Radio = 9,
                RellenoColor = Color.White, BordeColor = Estilo.Gris200
            };
            var ico = new Label
            {
                Text = glyph, Font = new Font("Segoe MDL2 Assets", 12f), AutoSize = false, Size = new Size(30, 26),
                Location = new Point(14, 11), TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White, ForeColor = Estilo.Gris700
            };
            p.Controls.Add(ico);
            pnlCard.Controls.Add(p);
            p.BringToFront();
            return p;
        }

        private void ReubicarTextBox(TextBox tb, TarjetaPanel panel, string placeholder, bool conAccion, string tagKey = null)
        {
            tb.Parent      = panel;
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor   = Color.White;
            tb.ForeColor   = Estilo.AzulOscuro;
            tb.Font        = Estilo.Normal(11f);
            tb.Location    = new Point(48, 15);
            tb.Width       = panel.Width - 48 - (conAccion ? 42 : 16);
            tb.Anchor      = AnchorStyles.Left | AnchorStyles.Top;

            // Ícono del campo (se resalta en coral al enfocar).
            Label ico = null;
            foreach (Control c in panel.Controls) if (c is Label l) { ico = l; break; }

            // Placeholder por OVERLAY: un label encima del textbox. No toca tb.Text (seguro para la
            // lógica de login) y usa un gris #6B7280 con contraste ≥4.5:1 sobre blanco (WCAG AA).
            var ph = new Label
            {
                Text = placeholder, Tag = tagKey, AutoSize = false, Location = new Point(48, 13),
                Size = new Size(tb.Width, 22), Font = Estilo.Normal(11f),
                ForeColor = Color.FromArgb(107, 114, 128), BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.IBeam
            };
            ph.Click += (s, e) => tb.Focus();
            panel.Controls.Add(ph);
            ph.BringToFront();
            void RefrescarPh() { ph.Visible = !tb.Focused && tb.TextLength == 0; }
            tb.TextChanged += (s, e) => RefrescarPh();
            RefrescarPh();

            // Estado de foco visible (accesible por teclado y mouse): borde coral 2px + ícono coral.
            tb.Enter += (s, e) => { panel.BordeColor = Estilo.Coral;   panel.BordeGrosor = 2f; if (ico != null) ico.ForeColor = Estilo.Coral;   ph.Visible = false; panel.Invalidate(); };
            tb.Leave += (s, e) => { panel.BordeColor = Estilo.Gris200; panel.BordeGrosor = 1f; if (ico != null) ico.ForeColor = Estilo.Gris700; RefrescarPh();     panel.Invalidate(); };
        }

        // ── Botón "Ingresar": azul marino sólido redondeado (CTA de máximo contraste) ─────────
        private void BtnIngresar_Paint(object sender, PaintEventArgs ev)
        {
            var b = (Button)sender;
            var g = ev.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            using (var path = BuildRoundedRect(new Rectangle(0, 0, b.Width - 1, b.Height - 1), 10))
            using (var br = new SolidBrush(_btnHover ? Color.FromArgb(52, 80, 110) : Estilo.AzulOscuro))
                g.FillPath(br, path);

            string txt = b.Text;
            if (!string.IsNullOrEmpty(txt) && txt.Length > 1 && txt == txt.ToUpperInvariant())
                txt = char.ToUpper(txt[0]) + txt.Substring(1).ToLowerInvariant();
            TextRenderer.DrawText(g, txt, b.Font, b.ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ── Panel izquierdo: foto de atardecer + tarjeta glass (logo + marca + slogan) ────────
        private void PnlLeft_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int W = pnlLeft.Width, H = pnlLeft.Height;

            // 1) Foto de atardecer (cover-fill). Fallback: degradé cálido.
            if (_bgImg != null)
            {
                float scale = Math.Max((float)W / _bgImg.Width, (float)H / _bgImg.Height);
                int sw = (int)(_bgImg.Width * scale), sh = (int)(_bgImg.Height * scale);
                g.DrawImage(_bgImg, (W - sw) / 2, (H - sh) / 2, sw, sh);
            }
            else
            {
                using (var lg = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                           Color.FromArgb(255, 138, 101), Color.FromArgb(255, 209, 150), 70f))
                    g.FillRectangle(lg, 0, 0, W, H);
            }

            // 2) Tarjeta glass centrada.
            int cw = 340, ch = 400, cx = (W - cw) / 2, cy = (H - ch) / 2;
            using (var sh = BuildRoundedRect(new Rectangle(cx, cy + 10, cw, ch), 26))
            using (var sb = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
                g.FillPath(sb, sh);
            using (var card = BuildRoundedRect(new Rectangle(cx, cy, cw, ch), 26))
            {
                // Fondo BLANCO SÓLIDO (#FFFFFF): contraste limpio para el logo y el texto.
                using (var b = new SolidBrush(Color.White))
                    g.FillPath(b, card);
                using (var pen = new Pen(Color.FromArgb(235, 235, 238), 1.2f))
                    g.DrawPath(pen, card);
            }

            // 3) Logo (marca coral) centrado.
            int ls = 132, lx = cx + (cw - ls) / 2, ly = cy + 52;
            if (_markImg != null) g.DrawImage(_markImg, new Rectangle(lx, ly, ls, ls));

            // 4) Wordmark "ExperienceHub" (Experience navy + Hub coral, según guía de marca) + slogan.
            using (var fWord = Estilo.SemiBold(22f))
            using (var fSlogan = Estilo.Crear(10.5f, FontStyle.Regular))
            using (var bNavy = new SolidBrush(Color.FromArgb(36, 59, 83)))
            using (var bCoral = new SolidBrush(Estilo.Coral))
            using (var bGrey = new SolidBrush(Color.FromArgb(120, 130, 145)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                string a = "Experience", b2 = "Hub";
                var stf = StringFormat.GenericTypographic;
                SizeF sa = g.MeasureString(a, fWord, 1000, stf), sb2 = g.MeasureString(b2, fWord, 1000, stf);
                float total = sa.Width + sb2.Width;
                float wy = cy + 236;
                float startX = cx + (cw - total) / 2f;
                g.DrawString(a, fWord, bNavy, startX, wy, stf);
                g.DrawString(b2, fWord, bCoral, startX + sa.Width, wy, stf);
                var trad = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
                string slogan = (trad != null && trad.ContainsKey("lbl.brand.slogan"))
                    ? trad["lbl.brand.slogan"].Texto : "Descubrí experiencias. Viví momentos.";
                g.DrawString(slogan, fSlogan, bGrey,
                    new RectangleF(cx + 16, cy + 286, cw - 32, 40), sf);
            }
        }

        // ── Decoración circular del panel derecho (estética WardrobeFlow, paleta SUNSET) ──────
        // Panel.Paint + GDI+ (FillEllipse/DrawEllipse), alfa muy bajo. Patrón coherente (un solo
        // tono coral): washes grandes + aros finos, parcialmente cortados por los bordes y SOLO en
        // zonas libres (esquinas y sobre el título). El formulario central queda limpio y legible.
        private void PnlCard_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = pnlCard.Width, h = pnlCard.Height;

            using (var b = new SolidBrush(Color.FromArgb(14, 255, 107, 107)))
                g.FillEllipse(b, w - 140, -105, 215, 215);           // wash superior-derecho (sobre el título)
            using (var b = new SolidBrush(Color.FromArgb(12, 255, 107, 107)))
                g.FillEllipse(b, -120, h - 140, 230, 230);            // wash inferior-izquierdo (esquina)

            using (var pen = new Pen(Color.FromArgb(26, 255, 107, 107), 1.2f))
                g.DrawEllipse(pen, -45, 28, 105, 105);                // aro superior-izquierdo (cortado)
            using (var pen = new Pen(Color.FromArgb(22, 255, 107, 107), 1.2f))
                g.DrawEllipse(pen, w - 55, h - 70, 115, 115);         // aro inferior-derecho (cortado)
        }

        private static GraphicsPath BuildRoundedRect(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.Left,          rect.Top,            r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Top,            r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0,   90);
            path.AddArc(rect.Left,          rect.Bottom - r * 2, r * 2, r * 2, 90,  90);
            path.CloseFigure();
            return path;
        }

        // ── Selector de idioma (footer) ───────────────────────────────────────────────────────
        private void AgregarComboIdioma()
        {
            _cmbIdiomaLogin = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size          = new Size(92, 22),
                Location      = new Point(170, 557),
                FlatStyle     = FlatStyle.Flat,
                Font          = Estilo.Normal(9f),
                ForeColor     = Estilo.AzulOscuro,
                BackColor     = Color.White,
                TabStop       = false
            };
            _cmbIdiomaLogin.SelectedIndexChanged += CmbIdiomaLogin_Changed;
            pnlCard.Controls.Add(_cmbIdiomaLogin);
            ConstruirComboIdioma(Traductor.ObtenerIdiomas());
        }

        private void ConstruirComboIdioma(IList<Idioma> idiomas)
        {
            _suprimirIdiomaChange = true;
            _cmbIdiomaLogin.DataSource    = null;
            _cmbIdiomaLogin.DisplayMember = "Nombre";
            _cmbIdiomaLogin.ValueMember   = "Id";
            _cmbIdiomaLogin.DataSource    = new List<Idioma>(idiomas);

            string cod = GestorIdioma.IdiomaActual?.Id ?? "ES";
            for (int i = 0; i < idiomas.Count; i++)
                if (string.Equals(idiomas[i].Id, cod, StringComparison.OrdinalIgnoreCase))
                { _cmbIdiomaLogin.SelectedIndex = i; break; }
            _suprimirIdiomaChange = false;
        }

        private void CmbIdiomaLogin_Changed(object sender, EventArgs e)
        {
            if (_suprimirIdiomaChange) return;
            var idioma = _cmbIdiomaLogin.SelectedItem as Idioma;
            if (idioma == null) return;
            try
            {
                var dict = new BLL.IdiomaService().CargarTraducciones(idioma.Id);
                GestorIdioma.CambiarIdioma(idioma, dict);
            }
            catch { GestorIdioma.CambiarIdioma(idioma); }
        }

        // ── Último usuario: precarga el nombre del último empleado que ingresó (HKCU) ───────────
        private void CargarRecordado()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(REG_KEY))
                {
                    var u = k?.GetValue("LastUser") as string;
                    if (!string.IsNullOrEmpty(u)) txtUsuario.Text = u;
                }
            }
            catch { }
        }

        private void GuardarRecordado()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(REG_KEY))
                {
                    k.SetValue("LastUser", txtUsuario.Text.Trim());
                }
            }
            catch { }
        }

        // ── Ciclo de vida ─────────────────────────────────────────────────────────
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                Estilo.Aplicar(this);
                this.Text = "ExperienceHub";
                ConstruirUI();
                pnlCard.BackColor = Color.White;
            }
            catch { }

            GestorIdioma.SuscribirObservador(this);

            try
            {
                var idiomas = new BLL.IdiomaService().ObtenerIdiomasActivosComoIdioma();
                if (idiomas.Count > 0)
                {
                    GestorIdioma.SetIdiomasDisponibles(idiomas);
                    ConstruirComboIdioma(idiomas);
                }
            }
            catch { }

            Traducir(GestorIdioma.IdiomaActual);

            try
            {
                this.Text = "ExperienceHub";
                if (lblAccent != null) lblAccent.Text = "Portal de Administración";
                btnIngresar.Invalidate();
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        // ── OBSERVER — T05 ──────────────────────────────────────────────────────────
        public void UpdateLanguage(Idioma idioma) => Traducir(idioma);

        private void Traducir(Idioma idioma)
        {
            var t = Traductor.ObtenerTraducciones(idioma);
            string Tx(string tag) => (tag != null && t.ContainsKey(tag)) ? t[tag].Texto : null;

            var tituloForm = Tx(this.Tag?.ToString());
            if (tituloForm != null) this.Text = tituloForm;

            // Re-traduce TODO control con un Tag que sea una clave conocida: el título, los links,
            // las labels del footer y los placeholders superpuestos (Usuario/Contraseña).
            TraducirArbol(this, t);

            // Footer "Versión": la palabra se traduce; el número de versión es fijo.
            if (_lblVersion != null) _lblVersion.Text = "|   " + (Tx("lbl.version") ?? "Versión 1.0.0");

            // Slogan de marca del panel izquierdo (lo dibuja PnlLeft_Paint por GDI): forzar el
            // re-dibujo para que tome el idioma nuevo.
            pnlLeft.Invalidate();

            btnIngresar.Invalidate();
        }

        // Recorre el árbol de controles y asigna el texto traducido a cada uno cuyo Tag sea una
        // clave presente en el diccionario. Así los controles creados por código (placeholders,
        // idioma, etc.) participan del cambio de idioma igual que los del Designer.
        private static void TraducirArbol(Control root, System.Collections.Generic.IDictionary<string, Traduccion> t)
        {
            foreach (Control c in root.Controls)
            {
                if (c.Tag is string clave && t.ContainsKey(clave))
                {
                    c.Text = t[clave].Texto;
                    if (c is Button) c.Invalidate();
                }
                if (c.HasChildren) TraducirArbol(c, t);
            }
        }

        // ── Eventos de negocio ────────────────────────────────────────────────────
        private void btnIngresar_Click(object sender, EventArgs e)
        {
            lblError.Text = string.Empty;
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            string Tx(string key, string fallback) => t.ContainsKey(key) ? t[key].Texto : fallback;

            try
            {
                if (usuarioBLL.Login(this.Text, txtUsuario.Text, txtContraseña.Text))
                {
                    var u = usuarioBLL.ObtenerUsuarioActivo();
                    if (u != null && u.RequiereCambioClave)
                    {
                        using (var dlg = new CambioClaveObligatorioForm())
                        {
                            if (dlg.ShowDialog(this) != DialogResult.OK)
                            {
                                usuarioBLL.Logout(this.Text);
                                MostrarErrorLogin(
                                    Tx("err.login.debecambiarclave",
                                       "Debés cambiar tu contraseña temporal para ingresar."),
                                    bloqueado: false);
                                return;
                            }
                        }
                    }

                    GuardarRecordado();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (BE.LoginException ex) when (ex.Tipo == BE.LoginException.TipoError.LimiteAlcanzado)
            {
                string titulo = Tx("dlg.login.sesion.titulo", "Sesión terminada");
                string cuerpo = Tx("err.login.limitesesion",  "Demasiados intentos fallidos en esta sesión.");
                string cierre = Tx("dlg.login.sesion.cierre", "La aplicación se cerrará.");
                MessageBox.Show(cuerpo + "\n\n" + cierre, titulo, MessageBoxButtons.OK, MessageBoxIcon.Stop);
                Application.Exit();
            }
            catch (BE.LoginException ex) when (ex.Tipo == BE.LoginException.TipoError.CuentaBloqueada)
            {
                MostrarErrorLogin(Tx("err.login.bloqueada", ex.Message), bloqueado: true);
            }
            catch (BE.LoginException ex) when (ex.Tipo == BE.LoginException.TipoError.CredencialesInvalidas)
            {
                string msg = ex.IntentosRestantes.HasValue
                    ? string.Format(Tx("err.login.intentos", "Usuario o contraseña incorrectos.\nIntentos restantes: {0}."), ex.IntentosRestantes.Value)
                    : Tx("err.login.credenciales", "Usuario o contraseña incorrectos.");
                MostrarErrorLogin(msg, bloqueado: false);
            }
            catch (BE.LoginException ex) when (ex.Tipo == BE.LoginException.TipoError.CamposVacios)
            {
                MostrarErrorLogin(Tx("err.login.camposvacio", ex.Message), bloqueado: false);
            }
            catch (BE.LoginException ex)
            {
                MostrarErrorLogin(ex.Message, bloqueado: false);
            }
        }

        private void MostrarErrorLogin(string mensaje, bool bloqueado)
        {
            lblError.ForeColor = bloqueado ? Color.FromArgb(140, 0, 0) : Color.FromArgb(229, 72, 77);
            lblError.Text = mensaje;
            lblError.Refresh();

            if (bloqueado)
            {
                txtUsuario.Enabled    = false;
                txtContraseña.Enabled = false;
                btnIngresar.Enabled   = false;
                this.AcceptButton     = null;
                if (_lnkEmergencia != null) _lnkEmergencia.Visible = true;   // RF-10 disponible al bloquearse
            }
            else
            {
                txtContraseña.Clear();
                txtContraseña.Focus();
            }
        }

        private void btnSalir_Click(object sender, EventArgs e) => Application.Exit();

        private void lnkOlvidaste_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            using (var form = new OlvideContrasenaForm())
                form.ShowDialog(this);
        }

        private void LnkEmergencia_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            using (var form = new DesbloqueoEmergenciaForm(txtUsuario.Text))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    txtUsuario.Enabled    = true;
                    txtContraseña.Enabled = true;
                    btnIngresar.Enabled   = true;
                    this.AcceptButton     = btnIngresar;
                    lblError.Text         = string.Empty;
                    _lnkEmergencia.Visible = false;
                    txtContraseña.Focus();
                }
            }
        }
    }
}

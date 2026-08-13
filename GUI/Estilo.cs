using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace GUI
{
    /// <summary>
    /// Identidad visual de ExperienceHub — paleta SUNSET.
    /// Fuente única de colores/estilos + un re-estilizador recursivo que moderniza cualquier
    /// formulario WinForms (viejo o nuevo) sin tocar su Designer: se invoca desde FormBase.OnLoad.
    /// Filosofía: interfaz luminosa, cálida y clara (nada de fondos oscuros).
    /// </summary>
    internal static class Estilo
    {
        // ── Paleta SUNSET (tokens de rol) — según el sistema de diseño Stitch/ExperienceHub ──
        // Coral primario · naranja hover · amarillo badges · azul oscuro para headers/texto ·
        // blanco cálido de superficie. Se conservan los NOMBRES como tokens de rol.
        public static readonly Color Coral       = Hex("#FF6B6B"); // PRIMARIO — acción principal, selección
        public static readonly Color Naranja      = Hex("#FF8C42"); // HOVER / secundario
        public static readonly Color Amarillo     = Hex("#FFD166"); // badges / advertencia suave
        public static readonly Color AzulOscuro   = Hex("#243B53"); // TEXTO, headers de grilla, títulos
        public static readonly Color BlancoCalido = Hex("#FFFDF9"); // superficie de forms/tarjetas
        public static readonly Color Lienzo       = Hex("#F8F9FF"); // fondo del área de trabajo (MDI)
        public static readonly Color Gris100      = Hex("#F1F5F9");
        public static readonly Color Gris200      = Hex("#E2E8F0"); // bordes (outline-variant)
        public static readonly Color Gris400      = Hex("#94A3B8");
        public static readonly Color Gris700      = Hex("#475569");
        public static readonly Color Verde        = Hex("#2E9E5B"); // acciones positivas
        public static readonly Color Rojo         = Hex("#E5484D"); // errores

        // Tipografía del sistema de diseño: MONTSERRAT, embebida con la app (carpeta Fonts\ junto
        // al ejecutable) y cargada vía PrivateFontCollection → no requiere instalarla en el sistema.
        // Si no se encuentra, cae a Segoe UI para no romper nada.
        private static readonly PrivateFontCollection _pfc = new PrivateFontCollection();
        public static readonly FontFamily FamiliaFuente   = CargarFamilia();
        public static readonly FontFamily FamiliaSemiBold = BuscarEnPfc("Montserrat SemiBold", FamiliaFuente);
        public static readonly string Fuente = FamiliaFuente.Name;

        private static FontFamily BuscarEnPfc(string nombre, FontFamily fallback)
        {
            try { foreach (var fam in _pfc.Families) if (fam.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase)) return fam; }
            catch { }
            return fallback;
        }

        private static FontFamily CargarFamilia()
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
                if (Directory.Exists(dir))
                {
                    foreach (var ttf in Directory.GetFiles(dir, "*.ttf"))
                        try { _pfc.AddFontFile(ttf); } catch { }
                    foreach (var fam in _pfc.Families)
                        if (fam.Name.Equals("Montserrat", StringComparison.OrdinalIgnoreCase)) return fam;
                    if (_pfc.Families.Length > 0) return _pfc.Families[0];
                }
            }
            catch { }
            return new FontFamily("Segoe UI");
        }

        /// <summary>Crea una fuente Montserrat (con fallback de estilo/familia si algo no está).</summary>
        public static Font Crear(float pt, FontStyle style = FontStyle.Regular)
        {
            try { return new Font(FamiliaFuente, pt, style); }
            catch { try { return new Font(FamiliaFuente, pt, FontStyle.Regular); } catch { return new Font("Segoe UI", pt, style); } }
        }

        /// <summary>Montserrat SemiBold (peso intermedio, ideal para títulos).</summary>
        public static Font SemiBold(float pt)
        {
            try { return new Font(FamiliaSemiBold, pt, FontStyle.Regular); }
            catch { return Crear(pt, FontStyle.Bold); }
        }

        public static Font Titulo(float pt = 16f)   => SemiBold(pt);          // títulos → SemiBold
        public static Font Normal(float pt = 9.5f)  => Crear(pt, FontStyle.Regular);
        public static Font Bold(float pt = 9.5f)    => Crear(pt, FontStyle.Bold);

        // ── Swap de tipografía: reemplaza la familia de CADA control por Montserrat, conservando
        //    tamaño y estilo. Cubre incluso los fonts "Segoe UI" hardcodeados en los Designer.
        //    Saltea los controles con fuente de emoji (para no romper los íconos/emojis).
        public static void AplicarFuente(Control root)
        {
            try { root.Font = ClonarEnMontserrat(root.Font); } catch { }
            SwapFuenteRecursivo(root);
        }

        private static void SwapFuenteRecursivo(Control cont)
        {
            foreach (Control c in cont.Controls)
            {
                try
                {
                    var f = c.Font;
                    // Saltea fuentes de íconos/emoji y cualquier variante Montserrat ya asignada.
                    if (f != null && !EsFuenteEspecial(f.Name)
                                  && f.FontFamily.Name.IndexOf("Montserrat", StringComparison.OrdinalIgnoreCase) < 0)
                        c.Font = ClonarEnMontserrat(f);
                }
                catch { }
                if (c.HasChildren) SwapFuenteRecursivo(c);
            }
        }

        private static Font ClonarEnMontserrat(Font f)
            => f == null ? Crear(9f) : Crear(f.Size, f.Style);

        // Fuentes de íconos/emoji que NO deben reemplazarse por Montserrat (romperían los glifos).
        private static bool EsFuenteEspecial(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return false;
            foreach (var k in new[] { "Emoji", "MDL2", "Fluent Icons", "Symbol", "Wingdings", "Webdings" })
                if (nombre.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static Color Hex(string h)
        {
            h = h.TrimStart('#');
            return Color.FromArgb(
                Convert.ToInt32(h.Substring(0, 2), 16),
                Convert.ToInt32(h.Substring(2, 2), 16),
                Convert.ToInt32(h.Substring(4, 2), 16));
        }

        // ── Colores e íconos por categoría de experiencia ─────────────────────
        // Se identifican por palabra clave (tolerante a los nombres sembrados).
        public static (Color Color, string Emoji) Categoria(string nombre)
        {
            string n = (nombre ?? "").ToLowerInvariant();
            if (Contiene(n, "gastro", "cata", "cocina", "vino")) return (Naranja,        "🍷");
            if (Contiene(n, "arte", "pintura"))                  return (Hex("#F06BA8"), "🎨");
            if (Contiene(n, "teatro", "cultura", "cultural"))    return (Hex("#8B5CF6"), "🎭");
            if (Contiene(n, "aventura", "aire", "natur"))        return (Verde,          "🏕️");
            if (Contiene(n, "tecno", "código", "codigo", "dev")) return (Hex("#3B82F6"), "💻");
            if (Contiene(n, "bienestar", "yoga", "salud", "medit")) return (Hex("#14B8A6"), "🧘");
            if (Contiene(n, "educa", "taller", "clase", "curso")) return (Hex("#38BDF8"), "📚");
            if (Contiene(n, "música", "musica", "concierto"))    return (Hex("#D946EF"), "🎵");
            if (Contiene(n, "foto"))                             return (Coral,          "📷");
            if (Contiene(n, "escape"))                           return (Amarillo,       "🧩");
            return (Coral, "✨");
        }

        private static bool Contiene(string s, params string[] keys)
        { foreach (var k in keys) if (s.Contains(k)) return true; return false; }

        // ── Re-estilizador recursivo ──────────────────────────────────────────
        /// <summary>Moderniza un formulario y todos sus controles con la identidad SUNSET.</summary>
        public static void Aplicar(Control root)
        {
            if (root is Form f)
            {
                // Lienzo lavanda (#F8F9FF): las tarjetas/inputs blancos "levantan" sobre él.
                f.BackColor = Lienzo;

                // Las toolbars/franjas de edición (FlowLayoutPanel) se funden con el lienzo
                // para que solo las tarjetas y los inputs blancos destaquen (look Stitch).
                foreach (Control c in f.Controls)
                    if (c is FlowLayoutPanel flp && (flp.Dock == DockStyle.Top || flp.Dock == DockStyle.Bottom))
                        flp.BackColor = Lienzo;

                // La grilla principal (Dock.Fill) se envuelve en una tarjeta redondeada blanca.
                EnvolverGrillaEnTarjeta(f);
            }
            AplicarFuente(root);   // Montserrat en todos los controles (incluidos los del Designer)
            EstilizarRecursivo(root);
        }

        /// <summary>
        /// Reparenta la grilla principal (DataGridView Dock.Fill, hija directa del form) dentro de
        /// una <see cref="TarjetaPanel"/> redondeada, dándole el aspecto de tarjeta flotante del
        /// sistema de diseño. Idempotente y sin alterar la posición de las barras Top/Bottom.
        /// </summary>
        private static void EnvolverGrillaEnTarjeta(Form f)
        {
            DataGridView grid = null;
            foreach (Control c in f.Controls)
                if (c is DataGridView g && g.Dock == DockStyle.Fill) { grid = g; break; }

            if (grid == null || grid.Parent is TarjetaPanel) return;

            var card = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, Padding = new Padding(12) };
            f.Controls.Add(card);
            grid.Parent = card;    // mueve la grilla dentro de la tarjeta (Dock.Fill se conserva)
            // El control Dock.Fill debe quedar en index 0 (frente) para que las barras Top/Bottom
            // reserven su borde y la tarjeta ocupe SOLO el espacio restante (sin solaparlas).
            card.BringToFront();
        }

        private static void EstilizarRecursivo(Control cont)
        {
            foreach (Control c in cont.Controls)
            {
                switch (c)
                {
                    case Button b:            EstilizarBoton(b); break;
                    case DataGridView g:      EstilizarGrid(g); break;
                    case TextBox tb:          tb.BorderStyle = BorderStyle.FixedSingle; tb.BackColor = Color.White; tb.ForeColor = AzulOscuro; break;
                    case ComboBox cb:         cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Color.White; cb.ForeColor = AzulOscuro; break;
                    case NumericUpDown nud:   nud.BorderStyle = BorderStyle.FixedSingle; nud.BackColor = Color.White; nud.ForeColor = AzulOscuro; break;
                    case CheckedListBox clb:  clb.BorderStyle = BorderStyle.FixedSingle; clb.BackColor = Color.White; clb.ForeColor = AzulOscuro; break;
                    case GroupBox gb:         gb.ForeColor = AzulOscuro; gb.Font = Bold(); break;
                    case Label lbl:
                        // Solo recolorear textos "por defecto" (negro/control) — respeta labels ya pintados.
                        if (lbl.ForeColor == SystemColors.ControlText || lbl.ForeColor.ToArgb() == Color.Black.ToArgb())
                            lbl.ForeColor = AzulOscuro;
                        break;
                }
                if (c.HasChildren) EstilizarRecursivo(c);
            }
        }

        // ── Botones ───────────────────────────────────────────────────────────
        private enum RolBoton { Primario, Secundario, Exito, Peligro }

        public static void EstilizarBoton(Button b)
        {
            var rol = ClasificarBoton(b);

            b.FlatStyle = FlatStyle.Flat;
            b.Font = Bold(9.5f);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.Padding = new Padding(12, 0, 12, 0);

            // En barras de flujo (BarraAcciones / BarraFiltros / KPIs) el botón se AUTO-DIMENSIONA a
            // su texto: con Montserrat (más ancha que Segoe) esto evita que el texto se corte y, a la
            // vez, no desperdicia ancho — y se re-ajusta solo al cambiar de idioma. En posiciones
            // absolutas (Designer) se conserva el tamaño fijo para no solaparse con los vecinos.
            if (b.Parent is FlowLayoutPanel)
            {
                b.AutoSize = true;
                b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                b.MinimumSize = new Size(100, 34);
            }
            else
            {
                b.AutoSize = false;
                if (b.Height < 34) b.Height = 34;             // altura uniforme para todos los botones
            }

            Color bg, fg, bgHover, borde; int bordeSize;
            switch (rol)
            {
                case RolBoton.Secundario: bordeSize = 1; bg = Color.White; fg = AzulOscuro;  bgHover = Gris100;        borde = Gris200; break; // neutro → outline
                case RolBoton.Peligro:    bordeSize = 0; bg = Rojo;        fg = Color.White; bgHover = Hex("#C83A3F"); borde = Rojo;    break; // destructiva → rojo
                case RolBoton.Exito:      bordeSize = 0; bg = Verde;       fg = Color.White; bgHover = Hex("#278A4E"); borde = Verde;   break; // positiva → verde
                default:                  bordeSize = 0; bg = Coral;       fg = Color.White; bgHover = Naranja;        borde = Coral;   break; // principal → coral
            }
            b.FlatAppearance.BorderSize = bordeSize;

            // Estado habilitado/DESHABILITADO: un botón deshabilitado se ve gris (no conserva su color),
            // para que se perciba claramente que no está disponible.
            void AplicarEstado()
            {
                if (b.Enabled) { b.BackColor = bg;      b.ForeColor = fg;      b.FlatAppearance.BorderColor = borde;  b.Cursor = Cursors.Hand; }
                else           { b.BackColor = Gris100; b.ForeColor = Gris400; b.FlatAppearance.BorderColor = Gris200; b.Cursor = Cursors.Default; }
            }
            b.EnabledChanged += (s, e) => AplicarEstado();
            b.MouseEnter += (s, e) => { if (b.Enabled) b.BackColor = bgHover; };
            b.MouseLeave += (s, e) => { if (b.Enabled) b.BackColor = bg; };
            AplicarEstado();
        }

        // Deduce el rol visual del botón a partir de su Tag ("eh.btn.xxx") o su texto.
        // Así la jerarquía de colores se aplica sola en todos los forms, sin marcar cada botón.
        private static RolBoton ClasificarBoton(Button b)
        {
            // Rol EXPLÍCITO vía AccessibleDescription (lo setea el Design System UI.*), sin pisar
            // la clave i18n que vive en Tag. Tiene prioridad sobre la inferencia por palabra.
            switch (b.AccessibleDescription)
            {
                case "rol:primario":   return RolBoton.Primario;
                case "rol:secundario": return RolBoton.Secundario;
                case "rol:peligro":    return RolBoton.Peligro;
                case "rol:exito":      return RolBoton.Exito;
            }
            if (b.DialogResult == DialogResult.Cancel)   return RolBoton.Secundario; // Cancelar de diálogo
            if ((b.Tag as string) == "secundario")       return RolBoton.Secundario;
            if ((b.Tag as string) == "primario")         return RolBoton.Primario;

            string t = TokenBoton(b);
            if (Contiene(t, "baja", "elimin", "borr", "suspend", "archiv", "purg", "quit", "rechaz", "revoc", "cancel", "desactiv"))
                return RolBoton.Peligro;
            if (Contiene(t, "activ", "reactiv", "aprob", "confirm", "desbloq", "habilit"))
                return RolBoton.Exito;
            if (Contiene(t, "guard", "crear", "reserv", "recomend", "gener", "restaur", "aplic", "agreg", "acept", "ingres", "promov", "calific", "renov", "contrat", "asign", "registrar"))
                return RolBoton.Primario;
            return RolBoton.Secundario;                  // nuevo, refrescar, buscar, limpiar, ver, cerrar, volver…
        }

        // Token normalizado (sin acentos) desde el Tag i18n del botón o, si no tiene, su texto.
        private static string TokenBoton(Button b)
        {
            string s = b.Tag as string;
            if (!string.IsNullOrEmpty(s) && s.StartsWith("eh."))
            {
                int i = s.LastIndexOf('.');
                s = i >= 0 ? s.Substring(i + 1) : s;
            }
            else s = b.Text;
            return QuitarAcentos((s ?? "").ToLowerInvariant());
        }

        private static string QuitarAcentos(string s)
        {
            var norm = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(norm.Length);
            foreach (char ch in norm)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            return sb.ToString();
        }

        private static void Hover(Button b, Color entra, Color sale)
        {
            b.MouseEnter += (s, e) => b.BackColor = entra;
            b.MouseLeave += (s, e) => b.BackColor = sale;
        }

        // ── DataGridView moderno ──────────────────────────────────────────────
        public static void EstilizarGrid(DataGridView g)
        {
            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = Gris200;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToResizeRows = false;
            g.RowTemplate.Height = 32;
            g.ColumnHeadersHeight = 40;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            g.ColumnHeadersDefaultCellStyle.BackColor = AzulOscuro;   // header azul oscuro (Stitch)
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.Font = Bold(9.5f);
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = AzulOscuro;

            g.DefaultCellStyle.BackColor = Color.White;
            g.DefaultCellStyle.ForeColor = AzulOscuro;
            g.DefaultCellStyle.Font = Normal(9.5f);
            g.DefaultCellStyle.SelectionBackColor = Coral;            // selección coral (Stitch)
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.White; // filas blancas (sin alternado)

            // Declutter automático: oculta columnas técnicas (Id*, _*, DVH) al bindear datos.
            g.DataBindingComplete -= OcultarColumnasTecnicas;
            g.DataBindingComplete += OcultarColumnasTecnicas;
        }

        private static void OcultarColumnasTecnicas(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            var g = (DataGridView)sender;
            foreach (DataGridViewColumn col in g.Columns)
            {
                string n = col.Name ?? "";
                if (n.StartsWith("Id") || n.StartsWith("_") || n.IndexOf("DVH", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.Equals("DuracionExperienciaMinutos") || n.Equals("FechaHoraExperiencia") || n.Equals("Suscripcion")
                    || n.Equals("Intereses") || n.Equals("Hijos"))
                    col.Visible = false;
            }
            TraducirEncabezados(g);
        }

        /// <summary>
        /// Traduce los encabezados de las grillas bindeadas a ENTIDADES usando el motor de idiomas.
        /// Se invoca al bindear datos (DataBindingComplete) y también al cambiar de idioma en caliente
        /// (desde I18n.TraducirControles), para que los títulos de columna sigan el idioma activo.
        /// Las grillas sobre DataTable/DataView ya traducen sus columnas en su propio formulario y se
        /// excluyen, para no pisarlas.
        /// </summary>
        public static void TraducirEncabezados(DataGridView g)
        {
            if (g == null || g.DataSource is System.Data.DataTable || g.DataSource is System.Data.DataView)
                return;
            foreach (DataGridViewColumn col in g.Columns)
                if (_encabezados.TryGetValue(col.Name ?? "", out var meta))
                    col.HeaderText = I18n.T(meta.Key, meta.Es);
        }

        // Encabezado de columna por nombre de propiedad → (clave i18n, texto por defecto en español).
        // Único lugar para nombrar de forma prolija y traducible las columnas de las grillas de
        // entidades (Experiencias, Reservas, Categorías, Ciudades, Organizadores, Lista de Espera,
        // Recomendaciones). Las claves col.eh.* se auto-siembran en la BD desde traducciones.tsv.
        private static readonly System.Collections.Generic.Dictionary<string, (string Key, string Es)> _encabezados =
            new System.Collections.Generic.Dictionary<string, (string Key, string Es)>(StringComparer.Ordinal)
            {
                // Comunes
                { "Nombre",            ("col.eh.nombre",            "Nombre") },
                { "Descripcion",       ("col.eh.descripcion",       "Descripción") },
                { "Estado",            ("col.eh.estado",            "Estado") },
                { "Resumen",           ("col.eh.resumen",           "Resumen") },
                { "ResumenEstado",     ("col.eh.resumen",           "Resumen") },
                // Experiencia
                { "NombreCategoria",   ("col.eh.categoria",         "Categoría") },
                { "NombreCiudad",      ("col.eh.ciudad",            "Ciudad") },
                { "Ubicacion",         ("col.eh.ubicacion",         "Ubicación") },
                { "Fecha",             ("col.eh.fecha",             "Fecha") },
                { "HoraInicio",        ("col.eh.hora",              "Hora") },
                { "DuracionMinutos",   ("col.eh.duracion",          "Duración (min)") },
                { "NombreOrganizador", ("col.eh.organizador",       "Organizador") },
                { "CupoMaximo",        ("col.eh.cupomax",           "Cupo máx.") },
                { "CupoDisponible",    ("col.eh.cupodisp",          "Cupo disp.") },
                { "EdadMinima",        ("col.eh.edadmin",           "Edad mín.") },
                { "Premium",           ("col.eh.premium",           "Premium") },
                // Reserva
                { "FechaReserva",      ("col.eh.fechareserva",      "Fecha de reserva") },
                { "FechaCancelacion",  ("col.eh.fechacancelacion",  "Fecha de cancelación") },
                { "MotivoCancelacion", ("col.eh.motivocancelacion", "Motivo de cancelación") },
                { "CantidadInvitados", ("col.eh.invitados",         "Invitados") },
                { "NombreCliente",     ("col.eh.cliente",           "Cliente") },
                { "NombreExperiencia", ("col.eh.experiencia",       "Experiencia") },
                { "NombreEmpleado",    ("col.eh.empleado",          "Empleado") },
                // Ciudad / Organizador
                { "Provincia",         ("col.eh.provincia",         "Provincia") },
                { "Contacto",          ("col.eh.contacto",          "Contacto") },
                { "Telefono",          ("col.eh.telefono",          "Teléfono") },
                { "Mail",              ("col.eh.mail",              "Mail") },
                // Lista de espera
                { "Posicion",          ("col.eh.posicion",          "Posición") },
                { "FechaIngreso",      ("col.eh.fechaingreso",      "Fecha de ingreso") },
            };

        // ── Mensajes elegantes (reemplazo de MessageBox) ──────────────────────
        public enum Tono { Info, Exito, Error, Pregunta }

        public static DialogResult Info(IWin32Window owner, string titulo, string mensaje)     => Mensaje(owner, titulo, mensaje, Tono.Info);
        public static DialogResult Exito(IWin32Window owner, string titulo, string mensaje)    => Mensaje(owner, titulo, mensaje, Tono.Exito);
        public static DialogResult Error(IWin32Window owner, string titulo, string mensaje)    => Mensaje(owner, titulo, mensaje, Tono.Error);
        public static bool Confirmar(IWin32Window owner, string titulo, string mensaje)        => Mensaje(owner, titulo, mensaje, Tono.Pregunta) == DialogResult.OK;

        public static DialogResult Mensaje(IWin32Window owner, string titulo, string mensaje, Tono tono)
        {
            Color acento; string icono;
            switch (tono)
            {
                case Tono.Exito:    acento = Verde;    icono = "✓"; break;
                case Tono.Error:    acento = Rojo;     icono = "✕"; break;
                case Tono.Pregunta: acento = Naranja;  icono = "?"; break;
                default:            acento = Coral;    icono = "★"; break;
            }

            using (var dlg = new Form
            {
                FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent,
                Size = new Size(420, 210), BackColor = BlancoCalido, ShowInTaskbar = false
            })
            {
                var barra = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = acento };
                var lblIco = new Label { Text = icono, Font = SemiBold(26f), ForeColor = acento,
                    AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(60, 60), Location = new Point(24, 30) };
                var lblTit = new Label { Text = titulo, Font = Bold(13f), ForeColor = AzulOscuro,
                    AutoSize = false, Location = new Point(96, 34), Size = new Size(300, 28) };
                var lblMsg = new Label { Text = mensaje, Font = Normal(10f), ForeColor = Gris700,
                    AutoSize = false, Location = new Point(96, 66), Size = new Size(300, 70) };

                var ok = new Button { Text = tono == Tono.Pregunta ? "Confirmar" : "Aceptar", DialogResult = DialogResult.OK,
                    Size = new Size(120, 36), Location = new Point(dlg.Width - 140, dlg.Height - 52) };
                EstilizarBoton(ok);

                dlg.Controls.Add(lblIco); dlg.Controls.Add(lblTit); dlg.Controls.Add(lblMsg);
                dlg.Controls.Add(ok); dlg.Controls.Add(barra);
                dlg.AcceptButton = ok;

                if (tono == Tono.Pregunta)
                {
                    var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Tag = "secundario",
                        Size = new Size(110, 36), Location = new Point(dlg.Width - 258, dlg.Height - 52) };
                    EstilizarBoton(cancel);
                    dlg.Controls.Add(cancel);
                    dlg.CancelButton = cancel;
                }

                return dlg.ShowDialog(owner);
            }
        }
    }
}

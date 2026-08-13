using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GUI
{
    /// <summary>
    /// Design System de ExperienceHub — componentes de UI reutilizables para que TODOS los
    /// formularios compartan la misma jerarquía visual y comportamiento:
    ///   Nivel 1  Encabezado de pantalla (título + descripción)
    ///   Nivel 2  Títulos de sección
    ///   Nivel 3  Campos alineados (label + control) sobre TableLayoutPanel
    ///   Nivel 4  Ayudas, estados y barra de acciones con jerarquía
    /// Construye sobre los tokens de <see cref="Estilo"/> (colores, fuentes, botones). No contiene
    /// lógica de negocio: solo presentación.
    /// </summary>
    public static class UI
    {
        // Espaciado base (múltiplos de 8) — un solo lugar para el ritmo visual de toda la app.
        public const int Gap   = 8;
        public const int GapL  = 16;
        public const int GapXL = 24;

        // ── Nivel 1 — Encabezado de pantalla ─────────────────────────────────────
        /// <summary>Cabecera con título grande + descripción breve y una línea divisoria.</summary>
        public static Panel Encabezado(string titulo, string descripcion, string tagTitulo = null, string tagDesc = null)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = string.IsNullOrEmpty(descripcion) ? 58 : 76,
                                Padding = new Padding(GapXL, GapL, GapXL, Gap), BackColor = Estilo.Lienzo };

            var linea = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Estilo.Gris200 };

            var lblSub = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 18, Text = descripcion ?? "",
                                     ForeColor = Estilo.Gris700, Font = Estilo.Normal(9f), Tag = tagDesc };
            var lblTit = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 32, Text = titulo,
                                     ForeColor = Estilo.AzulOscuro, Font = Estilo.SemiBold(17f), Tag = tagTitulo };

            if (!string.IsNullOrEmpty(descripcion)) p.Controls.Add(lblSub);
            p.Controls.Add(lblTit);
            p.Controls.Add(linea);
            return p;
        }

        // ── Nivel 2 — Título de sección ──────────────────────────────────────────
        public static Label TituloSeccion(string texto, string tag = null)
        {
            return new Label
            {
                Text = texto.ToUpperInvariant(), Dock = DockStyle.Top, Height = 28, Tag = tag,
                Font = Estilo.Bold(8.5f), ForeColor = Estilo.Gris700,
                Padding = new Padding(0, 10, 0, 2), TextAlign = ContentAlignment.BottomLeft
            };
        }

        // ── Nivel 3 — Grilla de campos alineados (label | control) ───────────────
        public static TableLayoutPanel GrillaCampos(int anchoLabel = 120)
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, Padding = new Padding(0, 2, 0, 6), Margin = new Padding(0)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, anchoLabel));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        /// <summary>Agrega una fila "Label | control" alineada. Marca obligatorios con " *".</summary>
        public static void Campo(TableLayoutPanel t, string label, Control input, bool obligatorio = false, string tagLabel = null)
        {
            int r = t.RowCount;
            t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            var lbl = new Label
            {
                Text = obligatorio ? label + " *" : label, Dock = DockStyle.Fill, Tag = tagLabel,
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = Estilo.AzulOscuro,
                Font = Estilo.Normal(9.5f), Margin = new Padding(0, 0, Gap, 0)
            };
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 3, 0, 3);

            t.Controls.Add(lbl, 0, r);
            t.Controls.Add(input, 1, r);
        }

        /// <summary>Agrega un título de sección (Nivel 2) como fila que ocupa las 2 columnas.</summary>
        public static void Seccion(TableLayoutPanel t, string titulo, string tag = null)
        {
            int r = t.RowCount; t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            var lbl = TituloSeccion(titulo, tag);
            lbl.Dock = DockStyle.Fill;
            t.Controls.Add(lbl, 0, r);
            t.SetColumnSpan(lbl, 2);
        }

        /// <summary>Fila de un control alto (ej. un panel de resumen) que ocupa las 2 columnas y se
        /// auto-ajusta en altura (no lo recorta como CampoAncho).</summary>
        public static void CampoBloque(TableLayoutPanel t, Control input)
        {
            int r = t.RowCount; t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            input.Dock = DockStyle.Top;
            input.Margin = new Padding(0, 2, 0, 6);
            t.Controls.Add(input, 0, r);
            t.SetColumnSpan(input, 2);
        }

        /// <summary>Fila de un solo control que ocupa las 2 columnas (ej. un CheckBox).</summary>
        public static void CampoAncho(TableLayoutPanel t, Control input)
        {
            int r = t.RowCount;
            t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 2, 0, 2);
            t.Controls.Add(input, 0, r);
            t.SetColumnSpan(input, 2);
        }

        // ── Panel de edición lateral (tarjeta a la derecha) ──────────────────────
        /// <summary>Tarjeta blanca redondeada anclada a la derecha, con scroll y padding.</summary>
        public static Panel PanelEdicion(int ancho = 360)
        {
            return new TarjetaPanel
            {
                Dock = DockStyle.Right, Width = ancho, Radio = 10, Padding = new Padding(GapL, Gap, GapL, Gap),
                Margin = new Padding(0), AutoScroll = true
            };
        }

        // ── Nivel 4 — Barra de acciones inferior (alineada a la derecha) ─────────
        /// <summary>Franja inferior con los botones alineados a la derecha (primario a la derecha del todo).</summary>
        public static Panel BarraAcciones(params Button[] botones)
        {
            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(GapL, GapL, GapL, GapL), BackColor = Estilo.Lienzo, WrapContents = false
            };
            foreach (var b in botones) { b.Margin = new Padding(Gap, 0, 0, 0); flp.Controls.Add(b); }
            return flp;
        }

        /// <summary>Barra inferior full-width con acciones a la izquierda (navegación) y a la derecha
        /// (acciones sobre el registro, con el primario más a la derecha).</summary>
        public static Panel BarraAccionesLR(Button[] izquierda, Button[] derecha)
        {
            var p = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Estilo.Lienzo, Padding = new Padding(GapXL, GapL, GapXL, GapL) };

            var der = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false, BackColor = Estilo.Lienzo };
            foreach (var b in derecha) { b.Margin = new Padding(Gap, 0, 0, 0); der.Controls.Add(b); }

            var izq = new FlowLayoutPanel { Dock = DockStyle.Left, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, BackColor = Estilo.Lienzo };
            foreach (var b in izquierda) { b.Margin = new Padding(0, 0, Gap, 0); izq.Controls.Add(b); }

            p.Controls.Add(der);
            p.Controls.Add(izq);
            return p;
        }

        // ── Botones con rol explícito (el color lo aplica Estilo.EstilizarBoton) ──
        private static Button Boton(string texto, string tagI18n, string rol, EventHandler onClick)
        {
            // Ancho inicial orientativo; en las barras de flujo el botón se auto-dimensiona a su
            // texto (ver Estilo.EstilizarBoton), así que no se corta ni desperdicia ancho.
            int ancho = Math.Max(110, (texto ?? "").Length * 9 + 26);
            var b = new Button
            {
                Text = texto, Tag = tagI18n, AccessibleDescription = rol, AutoSize = false,
                Width = ancho, Height = 36, MinimumSize = new Size(110, 36),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            if (onClick != null) b.Click += onClick;
            return b;
        }

        public static Button Primario(string texto, string tagI18n, EventHandler onClick)  => Boton(texto, tagI18n, "rol:primario",   onClick);
        public static Button Secundario(string texto, string tagI18n, EventHandler onClick) => Boton(texto, tagI18n, "rol:secundario", onClick);
        public static Button Peligro(string texto, string tagI18n, EventHandler onClick)    => Boton(texto, tagI18n, "rol:peligro",    onClick);
        public static Button Exito(string texto, string tagI18n, EventHandler onClick)      => Boton(texto, tagI18n, "rol:exito",      onClick);

        // ── Tira de KPIs (formularios analíticos, regla 36) ──────────────────────
        /// <summary>
        /// Tarjetas "valor grande + etiqueta" alineadas en una fila superior. Devuelve el panel y,
        /// por <paramref name="valores"/>, los Labels de valor para actualizarlos luego por índice.
        /// Pensada para los formularios de PdN 3 (Analítica): resume los indicadores clave.
        /// </summary>
        public static Panel TiraKPIs(out Label[] valores, params string[] etiquetas)
        {
            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 96, Padding = new Padding(GapXL, Gap, GapXL, Gap),
                BackColor = Estilo.Lienzo, WrapContents = false, AutoScroll = false
            };
            valores = new Label[etiquetas.Length];
            for (int i = 0; i < etiquetas.Length; i++)
            {
                var card = new TarjetaPanel
                {
                    Radio = 10, Width = 200, Height = 74, Margin = new Padding(0, 0, GapL, 0),
                    BackColor = Color.White, Padding = new Padding(GapL, Gap, GapL, Gap)
                };
                var lbl = new Label { Text = etiquetas[i], Dock = DockStyle.Bottom, Height = 20,
                                      Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris700, AutoEllipsis = true };
                // AutoEllipsis: si el valor es un texto largo (p. ej. el nombre de una experiencia)
                // se corta con "…" en vez de truncarse a la mitad; los números entran holgados.
                var val = new Label { Text = "—", Dock = DockStyle.Fill,
                                      Font = Estilo.SemiBold(19f), ForeColor = Estilo.AzulOscuro,
                                      TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
                card.Controls.Add(val);
                card.Controls.Add(lbl);
                valores[i] = val;
                flp.Controls.Add(card);
            }
            return flp;
        }

        // ── Barra de filtros / búsqueda (arriba de la grilla) ────────────────────
        public static FlowLayoutPanel BarraFiltros(params Control[] controles)
        {
            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 66, Padding = new Padding(GapXL, Gap, GapXL, Gap),
                BackColor = Estilo.Lienzo, WrapContents = false, AutoScroll = false
            };
            foreach (var c in controles) flp.Controls.Add(c);
            return flp;
        }

        /// <summary>Campo de filtro: label arriba + control abajo, listo para la barra horizontal.</summary>
        public static Panel FiltroCampo(string label, Control input, int ancho, string tagLabel = null)
        {
            var p = new Panel { Width = ancho, Height = 48, Margin = new Padding(0, 0, GapL, 0) };
            input.Dock = DockStyle.Top; input.Height = 26;
            p.Controls.Add(input);
            p.Controls.Add(new Label { Text = label, Tag = tagLabel, Dock = DockStyle.Top, Height = 18,
                                       ForeColor = Estilo.Gris700, Font = Estilo.Normal(9f) });
            return p;
        }

        // ── DataGridView listo para listado (selección de fila, sin columnas técnicas) ──
        public static DataGridView Grilla()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
        }

        // ── Curado de columnas: solo las relevantes, con orden y ancho relativo ──
        /// <summary>
        /// Deja visibles SOLO las columnas indicadas (por nombre de propiedad), en ese orden y con
        /// el peso relativo dado; oculta el resto (incluye Id*, técnicas, redundantes). Los encabezados
        /// los sigue traduciendo <see cref="Estilo.TraducirEncabezados"/>. Reutilizable por toda grilla.
        /// </summary>
        public static void Columnas(DataGridView g, params (string Prop, int Peso)[] visibles)
        {
            var pesos = new Dictionary<string, int>(StringComparer.Ordinal);
            var orden = new List<string>();
            foreach (var v in visibles) { pesos[v.Prop] = v.Peso; orden.Add(v.Prop); }

            foreach (DataGridViewColumn c in g.Columns)
            {
                if (pesos.TryGetValue(c.Name, out int w)) { c.Visible = true; c.FillWeight = w; }
                else c.Visible = false;
            }
            for (int i = 0; i < orden.Count; i++)
                if (g.Columns.Contains(orden[i]) && g.Columns[orden[i]].Visible)
                    g.Columns[orden[i]].DisplayIndex = i;
        }

        // ── Estado vacío: overlay sobre la grilla cuando no hay resultados ───────
        /// <summary>Muestra un mensaje centrado sobre la grilla cuando queda sin filas.</summary>
        public static void ConectarEstadoVacio(DataGridView g, string mensaje, string tag = null)
        {
            var lbl = new Label
            {
                Text = mensaje, Tag = tag, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Estilo.Gris400, Font = Estilo.Normal(11f), Visible = false, BackColor = Color.White
            };
            g.Controls.Add(lbl);

            void Ubicar()
            {
                int top = g.ColumnHeadersVisible ? g.ColumnHeadersHeight : 0;
                lbl.Bounds = new Rectangle(0, top, g.ClientSize.Width, Math.Max(0, g.ClientSize.Height - top));
            }
            void Refrescar() { Ubicar(); lbl.Visible = g.Rows.Count == 0; lbl.BringToFront(); }

            g.DataBindingComplete += (s, e) => Refrescar();
            g.SizeChanged        += (s, e) => Ubicar();
            Refrescar();
        }

        // ── Fila resumen clave: "Etiqueta: valor" para paneles de resumen ────────
        public static TableLayoutPanel Resumen()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, Padding = new Padding(0), Margin = new Padding(0)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        public static void FilaResumen(TableLayoutPanel t, string etiqueta, string valor)
        {
            FilaResumenValor(t, etiqueta).Text = valor;
        }

        /// <summary>Agrega una fila de resumen y devuelve el Label de valor para actualizarlo luego.</summary>
        public static Label FilaResumenValor(TableLayoutPanel t, string etiqueta)
        {
            int r = t.RowCount; t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            t.Controls.Add(new Label { Text = etiqueta, Dock = DockStyle.Fill, ForeColor = Estilo.Gris700, Font = Estilo.Normal(9f), TextAlign = ContentAlignment.MiddleLeft }, 0, r);
            var v = new Label { Text = "—", Dock = DockStyle.Fill, ForeColor = Estilo.AzulOscuro, Font = Estilo.Bold(9.5f), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            t.Controls.Add(v, 1, r);
            return v;
        }
    }
}

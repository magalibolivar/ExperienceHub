using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Regla 11 — recomienda experiencias a un cliente según su perfil, en cards con % de afinidad.</summary>
    public class RecomendacionesForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.Recomendacion _bll    = new BLL.Recomendacion();
        private readonly BLL.Cliente       _bllCli = new BLL.Cliente();

        private ComboBox _cboCliente;
        private FlowLayoutPanel _cards;
        private Label _lblVacio;

        public RecomendacionesForm()
        {
            Text = "Recomendaciones"; Size = new Size(960, 620); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(800, 520);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _cards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoScroll = true, BackColor = Estilo.Lienzo, Padding = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL),
                Visible = false
            };
            _cards.SizeChanged += (s, e) => AjustarAnchoCards();

            _lblVacio = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Estilo.Gris400, Font = Estilo.Normal(11f),
                Text = "Elegí un cliente y tocá «Recomendar»\npara ver experiencias afines a su perfil."
            };

            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            cont.Controls.Add(_cards);
            cont.Controls.Add(_lblVacio);

            _cboCliente = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            var btn = UI.Primario("Recomendar", "eh.btn.recomendar", (s, e) => Recomendar());
            btn.Margin = new Padding(0, 18, 0, 0);
            var filtros = UI.BarraFiltros(UI.FiltroCampo("Cliente", _cboCliente, 300, "eh.lbl.cliente"), btn);

            var header = UI.Encabezado("Recomendaciones",
                "Sugerencias de experiencias afines al perfil del cliente, ordenadas por % de afinidad.",
                "eh.frm.recomendaciones", "eh.rec.header.desc");

            Controls.Add(cont);
            Controls.Add(filtros);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.recomendaciones", this.Text); I18n.TraducirControles(this); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            try { _cboCliente.DataSource = _bllCli.ObtenerTodos(); _cboCliente.DisplayMember = "NombreCompleto"; _cboCliente.ValueMember = "IdCliente"; }
            catch (Exception ex) { MostrarError(ex); }
            Traducir();
        }

        private void Recomendar()
        {
            if (!(_cboCliente.SelectedValue is int idCli)) { MostrarError("Seleccioná un cliente."); return; }
            try
            {
                var recs = _bll.RecomendarDetallado(idCli, 12);
                _cards.SuspendLayout();
                _cards.Controls.Clear();
                foreach (var r in recs) _cards.Controls.Add(CrearCard(r));
                _cards.ResumeLayout();
                AjustarAnchoCards();

                bool hay = recs.Count > 0;
                _cards.Visible = hay;
                _lblVacio.Visible = !hay;
                if (!hay)
                    _lblVacio.Text = "No encontramos experiencias afines para este cliente por ahora.\nProbá completar sus intereses o su ciudad.";
                else
                    _lblVacio.BringToFront(); // asegurar orden coherente
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void AjustarAnchoCards()
        {
            int w = _cards.ClientSize.Width - _cards.Padding.Horizontal - 6;
            foreach (Control c in _cards.Controls) c.Width = Math.Max(420, w);
        }

        // Card de recomendación (paleta Sunset): nombre + subtítulo + motivos | % afinidad + "Ver experiencia".
        private Control CrearCard(BE.RecomendacionExperiencia r)
        {
            Color col = r.Afinidad >= 66 ? Estilo.Coral      // afinidad alta → coral (primario)
                      : r.Afinidad >= 33 ? Estilo.Naranja    // media → naranja
                      :                    Estilo.Gris400;    // baja → gris

            var card = new TarjetaPanel
            {
                Radio = 10, Height = 120, Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(16, 12, 16, 12), BackColor = Color.White
            };

            // Zona derecha: % de afinidad + acción
            var der = new Panel { Dock = DockStyle.Right, Width = 156, BackColor = Color.White };
            var lblAf  = new Label { Text = r.Afinidad + "%", Dock = DockStyle.Top, Height = 44, Font = Estilo.SemiBold(22f), ForeColor = col, TextAlign = ContentAlignment.MiddleRight };
            var lblCap = new Label { Text = "afinidad", Dock = DockStyle.Top, Height = 16, Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris400, TextAlign = ContentAlignment.TopRight };
            var btnVer = UI.Secundario("Ver experiencia", "eh.btn.verexp", (s, e) => VerExperiencia(r.Experiencia));
            btnVer.Dock = DockStyle.Bottom;
            der.Controls.Add(btnVer);
            der.Controls.Add(lblCap);
            der.Controls.Add(lblAf);
            Estilo.EstilizarBoton(btnVer);   // las cards se crean dinámicamente → estilizar a mano

            // Zona izquierda: nombre + subtítulo + motivos
            var lblNom = new Label { Text = r.Experiencia.Nombre, Dock = DockStyle.Top, Height = 26, Font = Estilo.SemiBold(12.5f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true };
            string sub = $"{r.Experiencia.NombreCategoria}   ·   {r.Experiencia.NombreCiudad}   ·   {r.Experiencia.Fecha:dd/MM/yyyy}";
            var lblSub = new Label { Text = sub, Dock = DockStyle.Top, Height = 18, Font = Estilo.Normal(9f), ForeColor = Estilo.Gris700 };
            var lblMot = new Label { Text = "•  " + string.Join("\n•  ", r.Motivos), Dock = DockStyle.Fill, Font = Estilo.Normal(9f), ForeColor = Estilo.Gris700 };

            card.Controls.Add(lblMot);
            card.Controls.Add(lblSub);
            card.Controls.Add(lblNom);
            card.Controls.Add(der);
            return card;
        }

        // "Consultar la experiencia": muestra el detalle reutilizando el diálogo del sistema.
        private void VerExperiencia(BE.Experiencia e)
        {
            string detalle =
                $"Categoría:  {e.NombreCategoria}\n" +
                $"Ciudad:  {e.NombreCiudad}\n" +
                $"Fecha:  {e.Fecha:dd/MM/yyyy}   {e.HoraInicio:hh\\:mm} hs\n" +
                $"Cupo disponible:  {e.CupoDisponible} / {e.CupoMaximo}\n" +
                $"Edad mínima:  {e.EdadMinima} años" +
                (e.Premium ? "\nExperiencia premium" : "") +
                (string.IsNullOrWhiteSpace(e.Descripcion) ? "" : $"\n\n{e.Descripcion}");
            Estilo.Info(this, e.Nombre, detalle);
        }
    }
}

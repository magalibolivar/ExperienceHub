using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Módulo de gestión de Ciudades (code-only).</summary>
    public class Ciudades : FormBase, IIdiomaObserver
    {
        private readonly BLL.Ciudad _bll = new BLL.Ciudad();
        private const string MODULO = "Ciudades";

        private DataGridView _grid;
        private TextBox _txtNombre, _txtProvincia;
        private Label _lblFormTitulo;
        private int _idSel;

        public Ciudades()
        {
            Text = "Ciudades"; Size = new Size(880, 520); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(720, 440);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            _grid.SelectionChanged += (s, e) => CargarSeleccion();
            UI.ConectarEstadoVacio(_grid, "No hay ciudades cargadas todavía.\nCreá la primera con «Guardar».", "eh.empty.ciudades");

            var panel = UI.PanelEdicion(330);
            _lblFormTitulo = new Label { Dock = DockStyle.Top, Height = 30, Text = "Nueva ciudad", Tag = "eh.ciu.form.nueva",
                                         Font = Estilo.SemiBold(12f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.MiddleLeft };
            _txtNombre = new TextBox();
            _txtProvincia = new TextBox();

            var campos = UI.GrillaCampos(100);
            UI.Seccion(campos, "Datos", "eh.sec.datos");
            UI.Campo(campos, "Nombre",    _txtNombre,    true,  "eh.lbl.nombre");
            UI.Campo(campos, "Provincia", _txtProvincia, false, "eh.lbl.provincia");

            var contenido = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            contenido.Controls.Add(campos);
            contenido.Controls.Add(_lblFormTitulo);
            panel.Controls.Add(contenido);

            var barra = UI.BarraAccionesLR(
                new[] { UI.Secundario("Refrescar", "eh.btn.refrescar", (s, e) => Cargar()) },
                new[] {
                    UI.Primario("Guardar",  "eh.btn.guardar",  (s, e) => Guardar()),
                    UI.Peligro ("Baja",     "eh.btn.baja",     (s, e) => CambiarEstado(false)),
                    UI.Exito   ("Activar",  "eh.btn.activar",  (s, e) => CambiarEstado(true)),
                    UI.Secundario("Nueva",  "eh.btn.nueva",    (s, e) => Limpiar())
                });

            var header = UI.Encabezado("Ciudades",
                "Catálogo de ciudades donde se ofrecen las experiencias.",
                "eh.frm.ciudades", "eh.ciu.header.desc");

            Controls.Add(_grid);
            Controls.Add(panel);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.ciudades", this.Text); I18n.TraducirControles(this); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            Cargar();
            Traducir();
        }

        private void Cargar()
        {
            try
            {
                _grid.DataSource = _bll.ObtenerTodos();
                UI.Columnas(_grid, ("Nombre", 42), ("Provincia", 43), ("Estado", 15));
                Limpiar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void CargarSeleccion()
        {
            if (_grid.CurrentRow?.DataBoundItem is BE.Ciudad c)
            {
                _idSel = c.IdCiudad; _txtNombre.Text = c.Nombre; _txtProvincia.Text = c.Provincia;
                if (_lblFormTitulo != null) _lblFormTitulo.Text = "Editar: " + c.Nombre;
            }
        }

        private void Limpiar()
        {
            _idSel = 0; _txtNombre.Clear(); _txtProvincia.Clear();
            if (_lblFormTitulo != null) _lblFormTitulo.Text = "Nueva ciudad";
            _grid.ClearSelection();
        }

        private void Guardar()
        {
            if (string.IsNullOrWhiteSpace(_txtNombre.Text)) { MostrarError("Ingresá el nombre de la ciudad."); return; }
            try
            {
                var c = new BE.Ciudad { IdCiudad = _idSel, Nombre = _txtNombre.Text.Trim(), Provincia = _txtProvincia.Text.Trim(), Estado = true };
                if (_idSel == 0) _bll.Alta(MODULO, c); else _bll.Modificar(MODULO, c);
                MostrarOk(_idSel == 0 ? "Ciudad creada." : "Ciudad actualizada.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void CambiarEstado(bool activar)
        {
            if (_idSel == 0) { MostrarError("Seleccioná una ciudad del listado."); return; }
            if (!activar && !Estilo.Confirmar(this, "Dar de baja",
                    $"¿Dar de baja la ciudad «{_txtNombre.Text}»?")) return;
            try
            {
                if (activar) _bll.Activar(MODULO, _idSel); else _bll.Baja(MODULO, _idSel);
                MostrarOk(activar ? "Ciudad activada." : "Ciudad dada de baja.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

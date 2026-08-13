using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Módulo de gestión de Categorías de experiencias (code-only).</summary>
    public class Categorias : FormBase, IIdiomaObserver
    {
        private readonly BLL.Categoria _bll = new BLL.Categoria();
        private const string MODULO = "Categorias";

        private DataGridView _grid;
        private TextBox _txtNombre, _txtDescripcion;
        private Label _lblFormTitulo;
        private int _idSel;

        public Categorias()
        {
            Text = "Categorías"; Size = new Size(900, 540); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(740, 460);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            _grid.SelectionChanged += (s, e) => CargarSeleccion();
            UI.ConectarEstadoVacio(_grid, "No hay categorías cargadas todavía.\nCreá la primera con «Guardar».", "eh.empty.categorias");

            var panel = UI.PanelEdicion(330);
            _lblFormTitulo = new Label { Dock = DockStyle.Top, Height = 30, Text = "Nueva categoría", Tag = "eh.cat.form.nueva",
                                         Font = Estilo.SemiBold(12f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.MiddleLeft };
            _txtNombre = new TextBox();
            _txtDescripcion = new TextBox();

            var campos = UI.GrillaCampos(100);
            UI.Seccion(campos, "Datos", "eh.sec.datos");
            UI.Campo(campos, "Nombre",      _txtNombre,      true,  "eh.lbl.nombre");
            UI.Campo(campos, "Descripción", _txtDescripcion, false, "eh.lbl.descripcion");

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

            var header = UI.Encabezado("Categorías",
                "Catálogo de categorías para clasificar las experiencias.",
                "eh.frm.categorias", "eh.cat.header.desc");

            Controls.Add(_grid);
            Controls.Add(panel);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.categorias", this.Text); I18n.TraducirControles(this); }

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
                UI.Columnas(_grid, ("Nombre", 38), ("Descripcion", 47), ("Estado", 15));
                Limpiar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void CargarSeleccion()
        {
            if (_grid.CurrentRow?.DataBoundItem is BE.Categoria c)
            {
                _idSel = c.IdCategoria; _txtNombre.Text = c.Nombre; _txtDescripcion.Text = c.Descripcion;
                if (_lblFormTitulo != null) _lblFormTitulo.Text = "Editar: " + c.Nombre;
            }
        }

        private void Limpiar()
        {
            _idSel = 0; _txtNombre.Clear(); _txtDescripcion.Clear();
            if (_lblFormTitulo != null) _lblFormTitulo.Text = "Nueva categoría";
            _grid.ClearSelection();
        }

        private void Guardar()
        {
            if (string.IsNullOrWhiteSpace(_txtNombre.Text)) { MostrarError("Ingresá el nombre de la categoría."); return; }
            try
            {
                var c = new BE.Categoria { IdCategoria = _idSel, Nombre = _txtNombre.Text.Trim(), Descripcion = _txtDescripcion.Text.Trim(), Estado = true };
                if (_idSel == 0) _bll.Alta(MODULO, c); else _bll.Modificar(MODULO, c);
                MostrarOk(_idSel == 0 ? "Categoría creada." : "Categoría actualizada.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void CambiarEstado(bool activar)
        {
            if (_idSel == 0) { MostrarError("Seleccioná una categoría del listado."); return; }
            if (!activar && !Estilo.Confirmar(this, "Dar de baja",
                    $"¿Dar de baja la categoría «{_txtNombre.Text}»?")) return;
            try
            {
                if (activar) _bll.Activar(MODULO, _idSel); else _bll.Baja(MODULO, _idSel);
                MostrarOk(activar ? "Categoría activada." : "Categoría dada de baja.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

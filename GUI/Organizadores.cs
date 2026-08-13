using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Módulo de gestión de Organizadores (code-only).</summary>
    public class Organizadores : FormBase, IIdiomaObserver
    {
        private readonly BLL.Organizador _bll = new BLL.Organizador();
        private const string MODULO = "Organizadores";

        private DataGridView _grid;
        private TextBox _txtNombre, _txtContacto, _txtTelefono, _txtMail;
        private Label _lblFormTitulo;
        private int _idSel;

        public Organizadores()
        {
            Text = "Organizadores"; Size = new Size(960, 560); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(780, 470);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            _grid.SelectionChanged += (s, e) => CargarSeleccion();
            UI.ConectarEstadoVacio(_grid, "No hay organizadores cargados todavía.\nCreá el primero con «Guardar».", "eh.empty.organizadores");

            var panel = UI.PanelEdicion(340);
            _lblFormTitulo = new Label { Dock = DockStyle.Top, Height = 30, Text = "Nuevo organizador", Tag = "eh.org.form.nuevo",
                                         Font = Estilo.SemiBold(12f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.MiddleLeft };
            _txtNombre = new TextBox();
            _txtContacto = new TextBox();
            _txtTelefono = new TextBox();
            _txtMail = new TextBox();

            var campos = UI.GrillaCampos(90);
            UI.Seccion(campos, "Organizador", "eh.sec.organizador");
            UI.Campo(campos, "Nombre",   _txtNombre,   true,  "eh.lbl.nombre");
            UI.Seccion(campos, "Contacto", "eh.sec.contacto");
            UI.Campo(campos, "Contacto", _txtContacto, false, "eh.lbl.contacto");
            UI.Campo(campos, "Teléfono", _txtTelefono, false, "eh.lbl.telefono");
            UI.Campo(campos, "Mail",     _txtMail,     false, "eh.lbl.mail");

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
                    UI.Secundario("Nuevo",  "eh.btn.nuevo",    (s, e) => Limpiar())
                });

            var header = UI.Encabezado("Organizadores",
                "Proveedores que organizan las experiencias del catálogo.",
                "eh.frm.organizadores", "eh.org.header.desc");

            Controls.Add(_grid);
            Controls.Add(panel);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.organizadores", this.Text); I18n.TraducirControles(this); }

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
                UI.Columnas(_grid, ("Nombre", 26), ("Contacto", 22), ("Telefono", 18), ("Mail", 24), ("Estado", 10));
                Limpiar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void CargarSeleccion()
        {
            if (_grid.CurrentRow?.DataBoundItem is BE.Organizador o)
            {
                _idSel = o.IdOrganizador;
                _txtNombre.Text = o.Nombre; _txtContacto.Text = o.Contacto;
                _txtTelefono.Text = o.Telefono; _txtMail.Text = o.Mail;
                if (_lblFormTitulo != null) _lblFormTitulo.Text = "Editar: " + o.Nombre;
            }
        }

        private void Limpiar()
        {
            _idSel = 0; _txtNombre.Clear(); _txtContacto.Clear(); _txtTelefono.Clear(); _txtMail.Clear();
            if (_lblFormTitulo != null) _lblFormTitulo.Text = "Nuevo organizador";
            _grid.ClearSelection();
        }

        private void Guardar()
        {
            if (string.IsNullOrWhiteSpace(_txtNombre.Text)) { MostrarError("Ingresá el nombre del organizador."); return; }
            try
            {
                var o = new BE.Organizador
                {
                    IdOrganizador = _idSel, Nombre = _txtNombre.Text.Trim(), Contacto = _txtContacto.Text.Trim(),
                    Telefono = _txtTelefono.Text.Trim(), Mail = _txtMail.Text.Trim(), Estado = true
                };
                if (_idSel == 0) _bll.Alta(MODULO, o); else _bll.Modificar(MODULO, o);
                MostrarOk(_idSel == 0 ? "Organizador creado." : "Organizador actualizado.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void CambiarEstado(bool activar)
        {
            if (_idSel == 0) { MostrarError("Seleccioná un organizador del listado."); return; }
            if (!activar && !Estilo.Confirmar(this, "Dar de baja",
                    $"¿Dar de baja al organizador «{_txtNombre.Text}»?")) return;
            try
            {
                if (activar) _bll.Activar(MODULO, _idSel); else _bll.Baja(MODULO, _idSel);
                MostrarOk(activar ? "Organizador activado." : "Organizador dado de baja.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

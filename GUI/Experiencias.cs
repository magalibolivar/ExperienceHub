using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Módulo de gestión de Experiencias (code-only). Catálogo + alta/edición por secciones.</summary>
    public class Experiencias : FormBase, IIdiomaObserver
    {
        private readonly BLL.Experiencia _bll     = new BLL.Experiencia();
        private readonly BLL.Categoria   _bllCat  = new BLL.Categoria();
        private readonly BLL.Ciudad      _bllCiu  = new BLL.Ciudad();
        private readonly BLL.Organizador _bllOrg  = new BLL.Organizador();
        private const string MODULO = "Experiencias";

        private DataGridView _grid;
        private TextBox _txtNombre, _txtUbicacion;
        private ComboBox _cboCategoria, _cboCiudad, _cboOrganizador, _cboEstado;
        private DateTimePicker _dtpFecha, _dtpHora;
        private NumericUpDown _numDuracion, _numCupo, _numEdad;
        private CheckBox _chkPremium;
        private Label _lblFormTitulo;
        private Panel _scrollEdicion;
        private ComboBox _cboFiltroEstado;
        private Label _lblContador;
        private Button _btnCancelarExp;
        private int _idSel;
        private System.Collections.Generic.List<BE.Experiencia> _todas = new System.Collections.Generic.List<BE.Experiencia>();

        public Experiencias()
        {
            // Name debe coincidir con [ControlMapeado].Formulario para gatear los botones por patente.
            Name = "Experiencias";
            Text = "Experiencias"; Size = new Size(1060, 640); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(880, 560);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            // ── Grilla (listado, izquierda) ──────────────────────────────────────
            _grid = UI.Grilla();
            _grid.SelectionChanged += (s, e) => CargarSeleccion();
            _grid.CellFormatting += FormatearCategoria;
            UI.ConectarEstadoVacio(_grid, "No hay experiencias en el catálogo todavía.\nCreá la primera con «Guardar».", "eh.empty.experiencias");

            // ── Tarjeta de edición (derecha) ─────────────────────────────────────
            var panel = UI.PanelEdicion(430);

            _lblFormTitulo = new Label
            {
                Dock = DockStyle.Top, Height = 30, Text = "Nueva experiencia", Tag = "eh.exp.form.nueva",
                Font = Estilo.SemiBold(12f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.MiddleLeft
            };

            _txtNombre      = new TextBox();
            _txtUbicacion   = new TextBox();
            _cboCategoria   = NuevoCombo();
            _cboCiudad      = NuevoCombo();
            _cboOrganizador = NuevoCombo();
            _cboEstado      = NuevoCombo();
            _dtpFecha    = new DateTimePicker { Format = DateTimePickerFormat.Short };
            _dtpHora     = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true };
            _numDuracion = new NumericUpDown { Minimum = 1, Maximum = 1440, Value = 60 };
            _numCupo     = new NumericUpDown { Minimum = 1, Maximum = 100000, Value = 10 };
            _numEdad     = new NumericUpDown { Minimum = 0, Maximum = 120, Value = 0 };
            _chkPremium  = new CheckBox { Text = "Experiencia premium", Tag = "eh.lbl.premium", AutoSize = true };
            _cboEstado.DataSource = Enum.GetValues(typeof(BE.EstadoExperiencia));

            var campos = UI.GrillaCampos(120);
            UI.Seccion(campos, "Información",     "eh.sec.informacion");
            UI.Campo(campos, "Nombre",       _txtNombre,      true, "eh.lbl.nombre");
            UI.Campo(campos, "Categoría",    _cboCategoria,   true, "eh.lbl.categoria");
            UI.Campo(campos, "Organizador",  _cboOrganizador, false, "eh.lbl.organizador");
            UI.Seccion(campos, "Ubicación",       "eh.sec.ubicacion");
            UI.Campo(campos, "Ciudad",       _cboCiudad,      true, "eh.lbl.ciudad");
            UI.Campo(campos, "Ubicación",    _txtUbicacion,   false, "eh.lbl.ubicacion");
            UI.Seccion(campos, "Fecha y horario", "eh.sec.fechahora");
            UI.Campo(campos, "Fecha",        _dtpFecha,       true, "eh.lbl.fecha");
            UI.Campo(campos, "Hora",         _dtpHora,        true, "eh.lbl.hora");
            UI.Campo(campos, "Duración (min)", _numDuracion,  false, "eh.lbl.duracion");
            UI.Seccion(campos, "Disponibilidad",  "eh.sec.disponibilidad");
            UI.Campo(campos, "Cupo",         _numCupo,        true, "eh.lbl.cupo");
            UI.Campo(campos, "Edad mínima",  _numEdad,        false, "eh.lbl.edadmin");
            UI.CampoAncho(campos, _chkPremium);
            UI.Campo(campos, "Estado",       _cboEstado,      false, "eh.lbl.estado");

            _scrollEdicion = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            _scrollEdicion.Controls.Add(campos);

            // Nombre estable para que ManejadorSeguridad lo oculte a quien no tenga mnuExperienciasCancelar.
            _btnCancelarExp = UI.Peligro("Cancelar experiencia", "eh.btn.cancelarexperiencia", (s, e) => CancelarExperiencia());
            _btnCancelarExp.Name = "btnCancelarExperiencia";
            var barra = UI.BarraAcciones(
                UI.Primario("Guardar",         "eh.btn.guardar",       (s, e) => Guardar()),
                UI.Secundario("Nueva",         "eh.btn.nueva",         (s, e) => Limpiar()),
                UI.Secundario("Cambiar estado","eh.btn.cambiarestado", (s, e) => CambiarEstado()),
                _btnCancelarExp);
            barra.BackColor = Color.White;

            panel.Controls.Add(_scrollEdicion);    // Fill
            panel.Controls.Add(barra);             // Bottom
            panel.Controls.Add(_lblFormTitulo);    // Top (título pinneado, siempre visible)

            // ── Barra de filtros sobre la grilla: estado + contador ──────────────
            _cboFiltroEstado = UI.ComboFiltroEstados<BE.EstadoExperiencia>(EstadoLabel, "Todos");
            _cboFiltroEstado.SelectedIndexChanged += (s, e) => AplicarFiltro();
            _lblContador = UI.Contador();
            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Estado", _cboFiltroEstado, 200, "eh.lbl.filtroestado"),
                UI.FiltroCampo(" ", _lblContador, 170));

            // Lectura de un vistazo: color de texto por estado de la experiencia.
            UI.PintarFilasPorEstado<BE.Experiencia>(_grid, ex => ColorEstado(ex.Estado));

            // ── Encabezado + montaje ─────────────────────────────────────────────
            var header = UI.Encabezado("Experiencias",
                "Catálogo de experiencias. Seleccioná una para editarla o creá una nueva.",
                "eh.frm.experiencias", "eh.exp.header.desc");

            Controls.Add(_grid);    // Fill (Estilo lo envuelve en tarjeta)
            Controls.Add(panel);    // Right
            Controls.Add(filtros);  // Top (debajo del header)
            Controls.Add(header);   // Top (ancho completo)
        }

        // Etiqueta legible de cada estado de la experiencia.
        private static string EstadoLabel(BE.EstadoExperiencia e)
        {
            switch (e)
            {
                case BE.EstadoExperiencia.Programada: return "Programada";
                case BE.EstadoExperiencia.Completa:   return "Completa";
                case BE.EstadoExperiencia.EnCurso:    return "En curso";
                case BE.EstadoExperiencia.Finalizada: return "Finalizada";
                case BE.EstadoExperiencia.Cancelada:  return "Cancelada";
                default: return e.ToString();
            }
        }

        // Color por estado: programada verde, completa ámbar, en curso coral, finalizada gris, cancelada roja.
        private static Color ColorEstado(BE.EstadoExperiencia e)
        {
            switch (e)
            {
                case BE.EstadoExperiencia.Programada: return Estilo.Verde;
                case BE.EstadoExperiencia.Completa:   return Estilo.Naranja;
                case BE.EstadoExperiencia.EnCurso:    return Estilo.Coral;
                case BE.EstadoExperiencia.Cancelada:  return Estilo.Rojo;
                default:                              return Estilo.Gris700;  // Finalizada
            }
        }

        private ComboBox NuevoCombo() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

        // Identidad por categoría: emoji + color propio en la columna Categoría.
        private void FormatearCategoria(object s, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "NombreCategoria") return;
            var cat = Estilo.Categoria(e.Value?.ToString());
            e.Value = cat.Emoji + "  " + e.Value;
            e.FormattingApplied = true;
            e.CellStyle.ForeColor = cat.Color;
            e.CellStyle.Font = Estilo.Bold(9.5f);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.experiencias", this.Text); I18n.TraducirControles(this); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            try
            {
                _cboCategoria.DataSource   = _bllCat.ObtenerActivas(); _cboCategoria.DisplayMember = "Nombre"; _cboCategoria.ValueMember = "IdCategoria";
                _cboCiudad.DataSource      = _bllCiu.ObtenerActivas(); _cboCiudad.DisplayMember = "Nombre"; _cboCiudad.ValueMember = "IdCiudad";
                _cboOrganizador.DataSource = _bllOrg.ObtenerActivos(); _cboOrganizador.DisplayMember = "Nombre"; _cboOrganizador.ValueMember = "IdOrganizador";
            }
            catch (Exception ex) { MostrarError(ex); }
            Cargar();
            Limpiar();
            Traducir();
        }

        private void Cargar()
        {
            try
            {
                _todas = _bll.ObtenerTodos();
                AplicarFiltro();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void AplicarFiltro()
        {
            try
            {
                var estado = UI.EstadoSeleccionado<BE.EstadoExperiencia>(_cboFiltroEstado);
                var lista = estado.HasValue ? _todas.FindAll(x => x.Estado == estado.Value) : _todas;

                _grid.DataSource = lista;
                UI.Columnas(_grid,
                    ("Nombre", 22), ("NombreCategoria", 14), ("NombreCiudad", 12),
                    ("Fecha", 15), ("HoraInicio", 11), ("CupoDisponible", 9), ("Estado", 17));
                // Formato compacto: la fecha sin la hora (venía como DateTime completo) y la hora HH:mm.
                if (_grid.Columns.Contains("Fecha"))
                {
                    _grid.Columns["Fecha"].DefaultCellStyle.Format = "dd/MM/yyyy";
                    _grid.Columns["Fecha"].HeaderText = "Fecha";
                }
                if (_grid.Columns.Contains("HoraInicio"))
                {
                    _grid.Columns["HoraInicio"].DefaultCellStyle.Format = @"hh\:mm";
                    _grid.Columns["HoraInicio"].HeaderText = "Hora";
                }
                if (_grid.Columns.Contains("CupoDisponible"))
                    _grid.Columns["CupoDisponible"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                if (_lblContador != null)
                    _lblContador.Text = string.Format(I18n.T("eh.lbl.mostrando", "Mostrando {0} de {1}"), lista.Count, _todas.Count);
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // "Cancelar experiencia" solo si la seleccionada no está ya Finalizada/Cancelada.
        private void ActualizarBotonCancelar(BE.Experiencia ex)
        {
            if (_btnCancelarExp == null) return;
            _btnCancelarExp.Enabled = ex != null
                && ex.Estado != BE.EstadoExperiencia.Finalizada
                && ex.Estado != BE.EstadoExperiencia.Cancelada;
        }

        private void CargarSeleccion()
        {
            if (_grid.CurrentRow?.DataBoundItem is BE.Experiencia ex)
            {
                _idSel = ex.IdExperiencia;
                _txtNombre.Text = ex.Nombre; _txtUbicacion.Text = ex.Ubicacion;
                _cboCategoria.SelectedValue = ex.IdCategoria; _cboCiudad.SelectedValue = ex.IdCiudad; _cboOrganizador.SelectedValue = ex.IdOrganizador;
                _dtpFecha.Value = ex.Fecha == default(DateTime) ? DateTime.Today : ex.Fecha;
                _dtpHora.Value  = DateTime.Today + ex.HoraInicio;
                _numDuracion.Value = Math.Max(1, ex.DuracionMinutos); _numCupo.Value = Math.Max(1, ex.CupoMaximo); _numEdad.Value = ex.EdadMinima;
                _chkPremium.Checked = ex.Premium; _cboEstado.SelectedItem = ex.Estado;
                if (_lblFormTitulo != null) _lblFormTitulo.Text = "Editar: " + ex.Nombre;
                ActualizarBotonCancelar(ex);
            }
        }

        private void Limpiar()
        {
            _idSel = 0; _txtNombre.Clear(); _txtUbicacion.Clear();
            _dtpFecha.Value = DateTime.Today.AddDays(1); _dtpHora.Value = DateTime.Today.AddHours(19);
            _numDuracion.Value = 60; _numCupo.Value = 10; _numEdad.Value = 0; _chkPremium.Checked = false;
            if (_cboEstado.Items.Count > 0) _cboEstado.SelectedIndex = 0;
            if (_lblFormTitulo != null) _lblFormTitulo.Text = "Nueva experiencia";
            if (_scrollEdicion != null) _scrollEdicion.AutoScrollPosition = new Point(0, 0);
            _grid.ClearSelection();
            ActualizarBotonCancelar(null);
        }

        private BE.Experiencia Leer() => new BE.Experiencia
        {
            IdExperiencia = _idSel,
            Nombre = _txtNombre.Text.Trim(),
            Ubicacion = _txtUbicacion.Text.Trim(),
            IdCategoria = _cboCategoria.SelectedValue is int ic ? ic : 0,
            IdCiudad = _cboCiudad.SelectedValue is int iu ? iu : 0,
            IdOrganizador = _cboOrganizador.SelectedValue is int io ? io : 0,
            Fecha = _dtpFecha.Value.Date,
            HoraInicio = _dtpHora.Value.TimeOfDay,
            DuracionMinutos = (int)_numDuracion.Value,
            CupoMaximo = (int)_numCupo.Value,
            EdadMinima = (int)_numEdad.Value,
            Premium = _chkPremium.Checked
        };

        // Validación de interfaz: mensajes claros por campo antes de llamar a la BLL.
        private string Validar(BE.Experiencia ex)
        {
            if (string.IsNullOrWhiteSpace(ex.Nombre)) return "Ingresá el nombre de la experiencia.";
            if (ex.IdCategoria <= 0)                  return "Seleccioná una categoría.";
            if (ex.IdCiudad <= 0)                     return "Seleccioná una ciudad.";
            if (ex.CupoMaximo <= 0)                   return "El cupo debe ser mayor a 0.";
            return null;
        }

        private void Guardar()
        {
            try
            {
                var ex = Leer();
                string err = Validar(ex);
                if (err != null) { MostrarError(err); return; }

                if (_idSel == 0) _bll.Alta(MODULO, ex); else _bll.Modificar(MODULO, ex);
                MostrarOk(_idSel == 0 ? "Experiencia creada correctamente." : "Experiencia actualizada.");
                Cargar(); Limpiar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void CambiarEstado()
        {
            if (_idSel == 0) { MostrarError("Seleccioná una experiencia del listado para cambiar su estado."); return; }
            try
            {
                var ex = _bll.ObtenerPorId(_idSel);
                var nuevo = (BE.EstadoExperiencia)_cboEstado.SelectedItem;

                // Cancelar NO es un simple cambio de estado: dispara la cascada (reservas + lista de
                // espera). Se redirige a «Cancelar experiencia» para no dejar datos inconsistentes.
                if (nuevo == BE.EstadoExperiencia.Cancelada)
                {
                    MostrarError("Para cancelar una experiencia usá «Cancelar experiencia»: cancela sus reservas (con devolución) y cierra la lista de espera.");
                    return;
                }

                _bll.CambiarEstado(MODULO, ex, nuevo);
                MostrarOk($"Estado cambiado a «{nuevo}».");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // CU01-COO — Cancela la experiencia seleccionada en cascada, pidiendo motivo obligatorio y
        // confirmando con el impacto (reservas y personas en lista de espera afectadas).
        private void CancelarExperiencia()
        {
            if (_idSel == 0) { MostrarError("Seleccioná una experiencia del listado para cancelarla."); return; }
            try
            {
                var ex = _bll.ObtenerPorId(_idSel);
                var impacto = _bll.ObtenerImpactoCancelacion(_idSel);

                var confirm = MessageBox.Show(
                    $"Vas a cancelar «{ex.Nombre}».\n\n" +
                    $"• Reservas activas que se cancelarán (con devolución del plan): {impacto.ReservasActivas}\n" +
                    $"• Clientes en lista de espera que se darán de baja: {impacto.EnListaEspera}\n\n" +
                    "Esta acción no se puede deshacer. ¿Confirmás?",
                    "Cancelar experiencia",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                using (var dlg = new InputDialog("Cancelar experiencia", "Motivo de cancelación:", false))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    _bll.CancelarExperiencia(MODULO, ex, dlg.InputText);
                }

                MostrarOk($"Experiencia cancelada. Se cancelaron {impacto.ReservasActivas} reserva(s) con devolución y se cerró la lista de espera.");
                Cargar(); Limpiar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

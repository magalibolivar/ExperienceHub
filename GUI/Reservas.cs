using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Módulo de Reservas (code-only): asignar una experiencia a un cliente según su suscripción.</summary>
    public class Reservas : FormBase, IIdiomaObserver
    {
        private readonly BLL.Reserva     _bll      = new BLL.Reserva();
        private readonly BLL.Cliente     _bllCli   = new BLL.Cliente();
        private readonly BLL.Experiencia _bllExp   = new BLL.Experiencia();
        private readonly BLL.ListaEspera _bllEsp   = new BLL.ListaEspera();
        private readonly BLL.Suscripcion _bllSus   = new BLL.Suscripcion();
        private readonly BLL.Recomendacion _bllRec = new BLL.Recomendacion();
        private const string MODULO = "Reservas";

        private DataGridView _grid;
        private ComboBox _cboCliente, _cboExperiencia;
        private ComboBox _cboFiltroEstado;
        private TextBox _txtBuscarCli;
        private Label _lblContador;
        private NumericUpDown _numInvitados;
        private Button _btnCrear, _btnConfirmar, _btnCancelar;
        private System.Collections.Generic.List<BE.Reserva> _todas = new System.Collections.Generic.List<BE.Reserva>();
        // Lista completa de clientes ya descifrada (ObtenerTodos trae el DNI en claro): el filtro por
        // nombre/DNI se hace EN MEMORIA sobre esta lista, sin consultar la BD por DNI en claro.
        private System.Collections.Generic.List<BE.Cliente> _todosClientes = new System.Collections.Generic.List<BE.Cliente>();
        // Valores del panel de resumen
        private Label _rvCliente, _rvPlan, _rvReservas, _rvExperiencia, _rvFecha, _rvCupo, _rvEstado, _rvFidelidad, _rvRecom;
        // Experiencia sugerida al cliente seleccionado (para el botón "Usar sugerida").
        private int? _idRecomendado;

        public Reservas()
        {
            // Name debe coincidir con [ControlMapeado].Formulario para gatear el botón Cancelar por patente.
            Name = "Reservas";
            Text = "Reservas"; Size = new Size(1120, 640); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(940, 560);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            // ── Listado de reservas (izquierda) ──────────────────────────────────
            _grid = UI.Grilla();
            UI.ConectarEstadoVacio(_grid, "Todavía no hay reservas registradas.", "eh.empty.reservas");

            // ── Tarjeta "Nueva reserva" (derecha) ────────────────────────────────
            var panel = UI.PanelEdicion(400);

            var titulo = new Label { Dock = DockStyle.Top, Height = 30, Text = "Nueva reserva", Tag = "eh.res.form.nueva",
                                     Font = Estilo.SemiBold(12f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.MiddleLeft };

            _cboCliente     = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboExperiencia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _numInvitados   = new NumericUpDown { Minimum = 0, Maximum = 50, Value = 0 };
            _txtBuscarCli   = new TextBox();
            _txtBuscarCli.TextChanged            += (s, e) => FiltrarClientes();
            _cboCliente.SelectedIndexChanged     += (s, e) => ActualizarResumen();
            _cboExperiencia.SelectedIndexChanged += (s, e) => ActualizarResumen();

            var campos = UI.GrillaCampos(110);
            UI.Seccion(campos, "Datos de la reserva", "eh.sec.reserva");
            UI.Campo(campos, "Buscar (nombre o DNI)", _txtBuscarCli, false, "eh.lbl.buscardni");
            UI.Campo(campos, "Cliente",     _cboCliente,     true, "eh.lbl.cliente");
            UI.Campo(campos, "Experiencia", _cboExperiencia, true, "eh.lbl.experiencia");
            UI.Campo(campos, "Invitados",   _numInvitados,   false, "eh.lbl.invitados");
            UI.Seccion(campos, "Resumen", "eh.sec.resumen");

            var resumen = UI.Resumen();
            _rvCliente     = UI.FilaResumenValor(resumen, "Cliente");
            _rvFidelidad   = UI.FilaResumenValor(resumen, "Fidelidad");
            _rvPlan        = UI.FilaResumenValor(resumen, "Plan");
            _rvReservas    = UI.FilaResumenValor(resumen, "Reservas disp.");
            _rvRecom       = UI.FilaResumenValor(resumen, "Sugerida");
            _rvExperiencia = UI.FilaResumenValor(resumen, "Experiencia");
            _rvFecha       = UI.FilaResumenValor(resumen, "Fecha / hora");
            _rvCupo        = UI.FilaResumenValor(resumen, "Cupo disp.");
            _rvEstado      = UI.FilaResumenValor(resumen, "Al confirmar");

            var contenido = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            contenido.Controls.Add(resumen);
            contenido.Controls.Add(campos);
            contenido.Controls.Add(titulo);

            _btnCrear = UI.Primario("Crear reserva", "eh.btn.crearreserva", (s, e) => Crear());
            var barra = UI.BarraAcciones(
                _btnCrear,
                UI.Secundario("Usar sugerida", "eh.btn.usarsugerida", (s, e) => UsarSugerida()),
                UI.Secundario("Lista de espera", "eh.btn.listaespera", (s, e) => IngresarListaEspera()));
            barra.BackColor = Color.White;

            panel.Controls.Add(contenido);
            panel.Controls.Add(barra);

            // ── Acciones sobre la reserva seleccionada (barra inferior) ──────────
            // Nombre estable para que ManejadorSeguridad oculte "Cancelar" a quien no tenga mnuReservasCancelar.
            _btnCancelar = UI.Peligro("Cancelar", "eh.btn.cancelarreserva", (s, e) => Cancelar());
            _btnCancelar.Name = "btnCancelarReserva";
            _btnConfirmar = UI.Exito("Confirmar", "eh.btn.confirmar", (s, e) => Confirmar());
            var accionesReserva = UI.BarraAccionesLR(
                new[] { UI.Secundario("Refrescar", "eh.btn.refrescar", (s, e) => Cargar()) },
                new[] { _btnConfirmar, _btnCancelar });

            // ── Barra de filtros sobre la grilla: estado + contador "Mostrando X de Y" ──
            _cboFiltroEstado = UI.ComboFiltroEstados<BE.EstadoReserva>(EstadoLabel, "Todos");
            _cboFiltroEstado.SelectedIndexChanged += (s, e) => AplicarFiltro();
            _lblContador = UI.Contador();
            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Estado", _cboFiltroEstado, 200, "eh.lbl.filtroestado"),
                UI.FiltroCampo(" ", _lblContador, 170));

            // Lectura de un vistazo: color de texto por estado de la reserva.
            UI.PintarFilasPorEstado<BE.Reserva>(_grid, r => ColorEstado(r.Estado));
            // Botones contextuales: se habilitan según la reserva seleccionada.
            _grid.SelectionChanged += (s, e) => ActualizarBotonesReserva();

            var header = UI.Encabezado("Reservas",
                "Asigná una experiencia a un cliente respetando su suscripción, o gestioná las reservas existentes.",
                "eh.frm.reservas", "eh.res.header.desc");

            Controls.Add(_grid);
            Controls.Add(panel);
            Controls.Add(accionesReserva);
            Controls.Add(filtros);
            Controls.Add(header);
        }

        // Etiqueta legible de cada estado (coincide con lo que muestra la columna Estado).
        private static string EstadoLabel(BE.EstadoReserva e)
        {
            switch (e)
            {
                case BE.EstadoReserva.Pendiente:  return "Pendiente";
                case BE.EstadoReserva.Confirmada: return "Confirmada";
                case BE.EstadoReserva.Cancelada:  return "Cancelada";
                case BE.EstadoReserva.Asistio:    return "Asistió";
                case BE.EstadoReserva.NoAsistio:  return "No asistió";
                default: return e.ToString();
            }
        }

        // Color de texto por estado (mismo criterio visual que WardrobeFlow: verde OK, ámbar pendiente, rojo cancelada).
        private static Color ColorEstado(BE.EstadoReserva e)
        {
            switch (e)
            {
                case BE.EstadoReserva.Confirmada:
                case BE.EstadoReserva.Asistio:    return Estilo.Verde;
                case BE.EstadoReserva.Pendiente:  return Estilo.Naranja;
                case BE.EstadoReserva.Cancelada:  return Estilo.Rojo;
                default:                          return Estilo.Gris700;  // NoAsistio
            }
        }

        private void ActualizarBotonesReserva()
        {
            var r = Sel();
            if (_btnConfirmar != null) _btnConfirmar.Enabled = r != null && r.PuedeConfirmarse();
            if (_btnCancelar  != null) _btnCancelar.Enabled  = r != null && r.PuedeCancelarse();
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.reservas", this.Text); I18n.TraducirControles(this); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            RecargarCombos();
            Cargar();
            ActualizarResumen();
            Traducir();
        }

        private void RecargarCombos()
        {
            try
            {
                _todosClientes = _bllCli.ObtenerTodos();   // lista ya descifrada (DNI en claro en memoria)
                FiltrarClientes();
                _cboExperiencia.DataSource = _bllExp.ObtenerParaReserva(); _cboExperiencia.DisplayMember = "Nombre"; _cboExperiencia.ValueMember = "IdExperiencia";
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Filtra la lista de clientes EN MEMORIA por nombre o DNI (sin consultar la BD por DNI en claro).
        private void FiltrarClientes()
        {
            string q = (_txtBuscarCli?.Text ?? "").Trim();
            var lista = string.IsNullOrEmpty(q)
                ? _todosClientes
                : _todosClientes.FindAll(c =>
                    (c.NombreCompleto ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (c.DNI ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            _cboCliente.DataSource = lista;
            _cboCliente.DisplayMember = "NombreCompleto";
            _cboCliente.ValueMember = "IdCliente";
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

        // Filtra por el estado elegido, colorea y actualiza el contador "Mostrando X de Y".
        private void AplicarFiltro()
        {
            try
            {
                var estado = UI.EstadoSeleccionado<BE.EstadoReserva>(_cboFiltroEstado);
                var lista = estado.HasValue ? _todas.FindAll(r => r.Estado == estado.Value) : _todas;

                _grid.DataSource = lista;
                UI.Columnas(_grid,
                    ("NombreCliente", 23), ("NombreExperiencia", 27), ("FechaReserva", 15),
                    ("CantidadInvitados", 11), ("Estado", 18));
                if (_grid.Columns.Contains("FechaReserva"))
                    _grid.Columns["FechaReserva"].DefaultCellStyle.Format = "dd/MM/yyyy";
                if (_grid.Columns.Contains("CantidadInvitados"))
                {
                    _grid.Columns["CantidadInvitados"].HeaderText = "Invitados";
                    _grid.Columns["CantidadInvitados"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                if (_lblContador != null)
                    _lblContador.Text = string.Format(I18n.T("eh.lbl.mostrando", "Mostrando {0} de {1}"), lista.Count, _todas.Count);
                ActualizarBotonesReserva();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Resumen en vivo: cliente, plan, saldo mensual, experiencia elegida, fecha y cupo.
        private void ActualizarResumen()
        {
            if (_rvCliente == null) return;
            try
            {
                var cli = _cboCliente.SelectedItem as BE.Cliente;
                var exp = _cboExperiencia.SelectedItem as BE.Experiencia;

                _rvCliente.Text     = cli?.NombreCompleto ?? "—";
                _rvExperiencia.Text = exp?.Nombre ?? "—";
                _rvFecha.Text       = exp != null ? $"{exp.Fecha:dd/MM/yyyy} {exp.HoraInicio:hh\\:mm}" : "—";
                _rvCupo.Text        = exp != null ? $"{exp.CupoDisponible} / {exp.CupoMaximo}" : "—";
                _rvEstado.Text      = "Pendiente";

                ActualizarFidelizacion(cli);

                BE.Suscripcion s = cli != null ? _bllSus.ObtenerVigentePorCliente(cli.IdCliente) : null;
                if (s == null)
                {
                    _rvPlan.Text = "Sin suscripción"; _rvPlan.ForeColor = Estilo.Rojo;
                    _rvReservas.Text = "—";           _rvReservas.ForeColor = Estilo.Rojo;
                    if (_btnCrear != null) _btnCrear.Enabled = false;
                    return;
                }

                int restantes = s.ReservasRestantes();
                bool puede    = s.PuedeReservar();
                _rvPlan.Text     = s.NombrePlan + (s.EstaVigente() ? "" : " (vencida)");
                _rvPlan.ForeColor = s.EstaVigente() ? Estilo.AzulOscuro : Estilo.Rojo;
                _rvReservas.Text = $"{restantes} de {s.ReservasDelPlan} este mes";
                _rvReservas.ForeColor = puede ? Estilo.Verde : Estilo.Rojo;

                // CU03-AGR (Consultar situación del cliente) — aviso temprano, ANTES de elegir
                // experiencia: si el cliente tiene una reserva Pendiente sin resolver o agotó sus
                // reservas del mes, se avisa y se bloquea "Crear reserva". Es la consulta de solo
                // lectura BLL.Reserva.ConsultarSituacionCliente (incluida luego en CU01-AGR al registrar).
                string aviso = null;
                try
                {
                    var sit = _bll.ConsultarSituacionCliente(cli.IdCliente);
                    if (sit.TieneReservaActiva)
                        aviso = string.Format(
                            I18n.T("eh.res.aviso.pendiente", "⚠ Reserva pendiente #{0} sin resolver — confirmala o cancelala"),
                            sit.IdReservaActiva);
                    else if (sit.Estado == BE.EstadoSituacionCliente.Ok && sit.ReservasRestantes <= 0)
                        aviso = I18n.T("eh.res.aviso.sincupo", "⚠ Sin reservas disponibles este mes");
                }
                catch { /* un fallo de la consulta no debe romper el resumen */ }

                if (aviso != null)
                {
                    _rvEstado.Text = aviso; _rvEstado.ForeColor = Estilo.Rojo;
                    puede = false;
                }
                else _rvEstado.ForeColor = Estilo.AzulOscuro;

                if (_btnCrear != null) _btnCrear.Enabled = puede;
            }
            catch (Exception ex) { _rvPlan.Text = ex.Message; }
        }

        private void Crear()
        {
            if (!(_cboCliente.SelectedValue is int idCli) || !(_cboExperiencia.SelectedValue is int idExp))
            { MostrarError("Seleccioná un cliente y una experiencia."); return; }
            try
            {
                _bll.CrearReserva(MODULO, idCli, idExp, (int)_numInvitados.Value);
                Estilo.Exito(this,
                    I18n.T("eh.msg.reservaregistrada.titulo", "Reserva registrada (pendiente de confirmación)"),
                    I18n.T("eh.msg.reservaregistrada.cuerpo", "Quedó registrada en estado Pendiente. Confirmala desde la grilla cuando corresponda."));
                RecargarCombos(); Cargar(); ActualizarResumen();
            }
            // Experiencia sin cupo: ofrecer ingresar a la lista de espera (flujo PN02).
            catch (BE.AppException ax) when (ax.Clave == "err.bll.reserva.sin_cupo")
            {
                if (MessageBox.Show(this,
                        "La experiencia está completa. ¿Querés ingresar al cliente a la lista de espera?",
                        "Sin cupo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    OfrecerIngresoListaEspera(idCli, idExp);
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Ingresa al cliente a la lista de espera de la experiencia (reutiliza la regla 8 de BLL).
        private void OfrecerIngresoListaEspera(int idCliente, int idExperiencia)
        {
            try
            {
                int pos = _bllEsp.Ingresar(MODULO, idExperiencia, idCliente);
                Estilo.Info(this, "En lista de espera", $"Te avisamos si se libera un lugar. Posición {pos} en la fila.");
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Fidelización: badge de nivel del cliente + experiencia sugerida según su historial.
        private void ActualizarFidelizacion(BE.Cliente cli)
        {
            _idRecomendado = null;
            if (_rvFidelidad == null) return;
            if (cli == null)
            {
                _rvFidelidad.Text = "—"; _rvFidelidad.ForeColor = Estilo.AzulOscuro;
                _rvRecom.Text = "—";     _rvRecom.ForeColor = Estilo.AzulOscuro;
                return;
            }
            try
            {
                var nivel = _bllRec.NivelFidelidad(cli.IdCliente);
                _rvFidelidad.Text = BE.Fidelidad.Etiqueta(nivel);
                _rvFidelidad.ForeColor = nivel == BE.NivelFidelidad.VIP ? Estilo.Coral
                                       : nivel == BE.NivelFidelidad.Frecuente ? Estilo.Verde
                                       : Estilo.AzulOscuro;

                var recs = _bllRec.RecomendarPara(cli.IdCliente, 1);
                if (recs.Count > 0)
                {
                    _idRecomendado = recs[0].Experiencia.IdExperiencia;
                    _rvRecom.Text  = $"{recs[0].Experiencia.Nombre}  ·  {recs[0].Motivo}";
                }
                else _rvRecom.Text = "Sin sugerencias por ahora";
                _rvRecom.ForeColor = Estilo.AzulOscuro;
            }
            catch (Exception ex) { _rvFidelidad.Text = "—"; _rvRecom.Text = ex.Message; }
        }

        // Carga en el combo la experiencia sugerida para el cliente (si hay).
        private void UsarSugerida()
        {
            if (!_idRecomendado.HasValue) { MostrarError("No hay una experiencia sugerida para este cliente."); return; }
            try { _cboExperiencia.SelectedValue = _idRecomendado.Value; } catch { }
            ActualizarResumen();
        }

        private void IngresarListaEspera()
        {
            try
            {
                if (!(_cboCliente.SelectedValue is int idCli) || !(_cboExperiencia.SelectedValue is int idExp))
                { MostrarError("Seleccioná un cliente y una experiencia."); return; }
                int pos = _bllEsp.Ingresar(MODULO, idExp, idCli);
                Estilo.Info(this, "En lista de espera", $"Te avisamos si se libera un lugar. Estás en la posición {pos} de la fila.");
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private BE.Reserva Sel() => _grid.CurrentRow?.DataBoundItem as BE.Reserva;

        private void Confirmar()
        {
            var r = Sel(); if (r == null) { MostrarError("Seleccioná una reserva del listado."); return; }
            try { _bll.Confirmar(MODULO, r); MostrarOk("Reserva confirmada."); Cargar(); } catch (Exception ex) { MostrarError(ex); }
        }

        private void Cancelar()
        {
            var r = Sel(); if (r == null) { MostrarError("Seleccioná una reserva del listado."); return; }
            using (var dlg = new InputDialog("Cancelar reserva", "Motivo de cancelación:", false))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { _bll.Cancelar(MODULO, r, dlg.InputText); MostrarOk("Reserva cancelada."); RecargarCombos(); Cargar(); ActualizarResumen(); }
                catch (Exception ex) { MostrarError(ex); }
            }
        }
    }
}

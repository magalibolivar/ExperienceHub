using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// PN02 · Reserva de Experiencia. Dos pestañas que NO compiten:
    ///   • «Nueva reserva» — asistente a ancho completo: ① Cliente → ② Experiencia → ③ Confirmar.
    ///       El paso ② ofrece experiencias CONTEXTUALIZADAS al cliente (compatibles primero, las no
    ///       disponibles con su motivo), usando el evaluador de solo lectura BLL.Reserva.EvaluarCompatibilidades.
    ///   • «Reservas existentes» — gestión (confirmar pendientes de lista de espera, cancelar, PDF).
    /// La validación DEFINITIVA y el cupo atómico siguen en la BLL al confirmar; la GUI no duplica reglas.
    /// </summary>
    public class Reservas : FormBase, IIdiomaObserver
    {
        private readonly BLL.Reserva       _bll    = new BLL.Reserva();
        private readonly BLL.Cliente       _bllCli = new BLL.Cliente();
        private readonly BLL.Experiencia   _bllExp = new BLL.Experiencia();
        private readonly BLL.ListaEspera   _bllEsp = new BLL.ListaEspera();
        private readonly BLL.Suscripcion   _bllSus = new BLL.Suscripcion();
        private readonly BLL.Recomendacion _bllRec = new BLL.Recomendacion();
        private readonly BLL.PlanSuscripcion _bllPlan = new BLL.PlanSuscripcion();
        private readonly BLL.Calificacion  _bllCal = new BLL.Calificacion();
        private const string MODULO = "Reservas";
        // Bitácora de asistencia/calificación: se conserva el módulo original de "Reservas realizadas"
        // para que la auditoría quede idéntica tras unificar en una pestaña.
        private const string MODULO_ASIS = "ReservasRealizadas";

        // Permisos resueltos al abrir (para gatear pestañas y acciones sin ampliar privilegios).
        private bool _puedeReservas, _puedeRealizadas, _puedeRealizadasEditar;

        // Situación del cliente para el paso ① (orden = ítems del combo "Mostrar" no, es coloreo).
        private enum SitCat { Activa, PorVencer, Vencida, Suspendida, SinSus }

        private TabControl _tabs;

        // ── Asistente ────────────────────────────────────────────────────────────
        private int _paso = 1;
        private Panel _pasoCliente, _pasoExperiencia, _pasoConfirmar;
        private PasoBar _pasoBar;
        private Button _btnAtras, _btnSiguiente, _btnReservar, _btnOtra;

        // Paso ① Cliente
        private TextBox _txtBuscarCli;
        private ComboBox _cboMostrar;
        private DataGridView _gridCli;
        private List<BE.Cliente> _todosClientes = new List<BE.Cliente>();
        private Label _cliNombre, _cliDni, _cliCiudad, _cliEmail, _cliEstadoSus, _cliVence, _cliFidelidad, _cliHabilitado;
        private Panel _cliAlerta; private Label _cliAlertaLbl;
        private Button _btnContratar;
        private bool _clienteOk;
        private BE.Cliente _clienteSel;

        /// <summary>Pide abrir PN01 «Nueva contratación» con este cliente (continuidad comercial cuando no puede reservar por falta de suscripción). Lo cablea el Menú a AbrirNuevaContratacionPara.</summary>
        public event Action<int> SolicitaContratar;

        // Paso ② Experiencia
        private DataGridView _gridExp;
        private NumericUpDown _numInvitados;
        private int _maxInvitados;          // tope de invitados del plan del cliente (CantidadInvitados)
        private bool _ajustandoInvitados;   // evita recalcular al capar el NumericUpDown por el plan
        private List<BE.Experiencia> _experiencias = new List<BE.Experiencia>();
        private Dictionary<int, BE.CompatibilidadReserva> _compat = new Dictionary<int, BE.CompatibilidadReserva>();
        private int? _idRecomendado; private string _motivoRecom;
        private Label _expNombre, _expFecha, _expCiudad, _expCupo, _expEstado, _expRecom;
        private Button _btnListaEspera;
        private BE.Experiencia _expSel;

        // Paso ③ Confirmar
        private Label _cfCliente, _cfExperiencia, _cfFecha, _cfLugares;

        // ── Pestaña «Reservas existentes» ─────────────────────────────────────────
        private DataGridView _grid;
        private ComboBox _cboFiltroEstado, _cboFiltroCliente, _cboFiltroExperiencia;
        private bool _cargandoFiltros;
        private Label _lblContador;
        private Button _btnConfirmar, _btnCancelar, _btnConfirmacionPdf;
        private List<BE.Reserva> _todas = new List<BE.Reserva>();

        // ── Pestaña «Asistencia y valoración» (ex ReservasRealizadas) ──────────────
        private DataGridView _gridAsis;
        private ComboBox _cboVistaAsis;
        private TextBox _txtBuscarAsis;
        private Label _lblIndAsis, _lblIndCal, _asisDetalle;
        private Button _btnAsistio, _btnNoAsistio, _btnCalificar;
        private List<BE.Reserva> _realizadas = new List<BE.Reserva>();

        public Reservas()
        {
            Name = "Reservas";
            Text = "Reservas"; Size = new Size(1160, 680); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(1000, 600);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            // Permisos resueltos una vez (con bypass admin). Gatean qué pestañas se muestran y qué acciones
            // se habilitan: la unificación NO amplía privilegios — cada patente gobierna lo mismo que antes.
            _puedeReservas         = PermisosGui.Permite(BE.Patentes.Reservas);
            _puedeRealizadas       = PermisosGui.Permite(BE.Patentes.ReservasRealizadas);
            _puedeRealizadasEditar = PermisosGui.Permite(BE.Patentes.ReservasRealizadasEditar);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            // ①② Nueva reserva + Gestionar reservas → requieren mnuReservas.
            if (_puedeReservas)
            {
                var tabNueva = new TabPage(I18n.T("eh.res.tab.nueva", "Nueva reserva")) { Tag = "eh.res.tab.nueva", BackColor = Estilo.Lienzo };
                var tabGest  = new TabPage(I18n.T("eh.res.tab.gestionar", "Gestionar reservas")) { Tag = "eh.res.tab.gestionar", BackColor = Estilo.Lienzo };
                tabNueva.Controls.Add(ConstruirTabNueva());
                tabGest.Controls.Add(ConstruirTabExistentes());
                _tabs.TabPages.Add(tabNueva);
                _tabs.TabPages.Add(tabGest);
            }
            // ③ Asistencia y valoración → requiere mnuReservasRealizadas.
            if (_puedeRealizadas)
            {
                var tabAsis = new TabPage(I18n.T("eh.res.tab.asistencia", "Asistencia y valoración")) { Tag = "eh.res.tab.asistencia", BackColor = Estilo.Lienzo };
                tabAsis.Controls.Add(ConstruirTabAsistencia());
                _tabs.TabPages.Add(tabAsis);
            }

            var header = UI.Encabezado("Reservas",
                "Asesorá al cliente y concretá la reserva, gestioná las existentes o registrá asistencia y valoración.",
                "eh.frm.reservas", "eh.res.header.desc");

            Controls.Add(_tabs);    // Fill
            Controls.Add(header);   // Top
        }

        // ══════════════════════ PESTAÑA «NUEVA RESERVA» (asistente) ════════════════
        private Panel ConstruirTabNueva()
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            _pasoCliente     = ConstruirPasoCliente();
            _pasoExperiencia = ConstruirPasoExperiencia();
            _pasoConfirmar   = ConstruirPasoConfirmar();
            var centro = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            centro.Controls.Add(_pasoCliente);
            centro.Controls.Add(_pasoExperiencia);
            centro.Controls.Add(_pasoConfirmar);

            _btnAtras     = UI.Secundario("Atrás", "eh.btn.atras", (s, e) => Ir(_paso - 1));
            _btnSiguiente = UI.Primario("Siguiente", "eh.btn.siguiente", (s, e) => Ir(_paso + 1));
            _btnReservar  = UI.Primario("Reservar y confirmar", "eh.res.btn.reservar", (s, e) => ReservarYConfirmar());
            _btnOtra      = UI.Primario("Iniciar otra reserva", "eh.res.btn.otra", (s, e) => ReiniciarAsistente());
            var footer = UI.BarraAccionesLR(new[] { _btnAtras }, new[] { _btnSiguiente, _btnReservar, _btnOtra });

            _pasoBar = new PasoBar
            {
                Dock = DockStyle.Top,
                Pasos = new[]
                {
                    I18n.T("eh.res.paso.cliente",      "Cliente"),
                    I18n.T("eh.res.paso.experiencia",  "Experiencia"),
                    I18n.T("eh.res.paso.confirmacion", "Confirmación")
                }
            };

            host.Controls.Add(centro);   // Fill
            host.Controls.Add(footer);   // Bottom
            host.Controls.Add(_pasoBar); // Top
            return host;
        }

        // ── Paso ① Cliente ─────────────────────────────────────────────────────────
        private Panel ConstruirPasoCliente()
        {
            var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Estilo.Lienzo,
                                              Padding = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL) };
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            var cardIzq = Tarjeta(new Padding(0, 0, UI.Gap, 0));
            _gridCli = UI.Grilla();
            _gridCli.SelectionChanged += (s, e) => MostrarCliente();
            UI.ConectarEstadoVacio(_gridCli, "Buscá un cliente por nombre o DNI.", "eh.res.cli.empty");

            _txtBuscarCli = new TextBox();
            _txtBuscarCli.TextChanged += (s, e) => FiltrarClientes();
            _cboMostrar = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboMostrar.Items.AddRange(new object[]
            {
                I18n.T("eh.res.filtro.todoscli", "Todos los clientes"),
                I18n.T("eh.res.cli.pueden",      "Solo los que pueden reservar"),
            });
            _cboMostrar.SelectedIndex = 0;
            _cboMostrar.SelectedIndexChanged += (s, e) => FiltrarClientes();

            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White, WrapContents = false, AutoSize = false };
            filtros.Controls.Add(UI.FiltroCampo("Buscar (nombre o DNI)", _txtBuscarCli, 240, "eh.lbl.buscardni"));
            filtros.Controls.Add(UI.FiltroCampo("Mostrar", _cboMostrar, 210, "eh.res.mostrar"));

            cardIzq.Controls.Add(_gridCli);
            cardIzq.Controls.Add(filtros);
            cardIzq.Controls.Add(UI.TituloSeccion("Clientes", "eh.nva.cli.listado"));

            var cardDer = Tarjeta(new Padding(UI.Gap, 0, 0, 0));
            var cuerpo = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            // Jerarquía: ① nombre (título) → ② datos personales → ③ suscripción → ④ fidelidad → ⑤ veredicto + alerta.
            _cliNombre    = new Label { Dock = DockStyle.Top, Height = 30, Font = Estilo.SemiBold(13f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true, Text = "—" };
            _cliDni       = FilaDato("DNI —"); _cliCiudad = FilaDato("Ciudad —"); _cliEmail = FilaDato("Email —");
            var sep1      = new Panel { Dock = DockStyle.Top, Height = 1, Margin = new Padding(0, 6, 0, 6), BackColor = Estilo.Gris200 };
            var tSus      = UI.TituloSeccion("Suscripción", "eh.cli.sec.suscripcion");
            _cliEstadoSus = new Label { Dock = DockStyle.Top, Height = 24, Font = Estilo.Bold(10f), ForeColor = Estilo.Gris700, Text = "—" };
            _cliVence     = FilaDato("Vence —");
            _cliFidelidad = FilaDato("");
            var sep2      = new Panel { Dock = DockStyle.Top, Height = 1, Margin = new Padding(0, 6, 0, 6), BackColor = Estilo.Gris200 };
            _cliHabilitado = new Label { Dock = DockStyle.Top, Height = 26, Font = Estilo.Bold(10.5f), ForeColor = Estilo.Gris700, Text = "", TextAlign = ContentAlignment.MiddleLeft };
            _cliAlertaLbl = new Label { Dock = DockStyle.Fill, Font = Estilo.Normal(9f), ForeColor = Estilo.AzulOscuro, Padding = new Padding(8, 6, 8, 6), TextAlign = ContentAlignment.MiddleLeft };
            _cliAlerta    = new Panel { Dock = DockStyle.Top, Height = 50, Visible = false, Margin = new Padding(0, 4, 0, 0), BackColor = Estilo.Amarillo };
            _cliAlerta.Controls.Add(_cliAlertaLbl);
            // Orden inverso (Dock=Top apila de abajo hacia arriba): alerta → verdicto → sep → fidelidad → vence → estado → título → sep → datos → nombre.
            cuerpo.Controls.Add(_cliAlerta); cuerpo.Controls.Add(_cliHabilitado); cuerpo.Controls.Add(sep2);
            cuerpo.Controls.Add(_cliFidelidad); cuerpo.Controls.Add(_cliVence); cuerpo.Controls.Add(_cliEstadoSus); cuerpo.Controls.Add(tSus);
            cuerpo.Controls.Add(sep1);
            cuerpo.Controls.Add(_cliEmail); cuerpo.Controls.Add(_cliCiudad); cuerpo.Controls.Add(_cliDni); cuerpo.Controls.Add(_cliNombre);

            // Continuidad comercial: «Ir a contratación» (PN01) cuando no puede reservar por falta de suscripción.
            _btnContratar = UI.Primario("Ir a contratación", "eh.res.btn.contratar", (s, e) => IrAContratacion());
            _btnContratar.Visible = false;
            var barraContratar = UI.BarraAcciones(_btnContratar); barraContratar.BackColor = Color.White;

            cardDer.Controls.Add(cuerpo);
            cardDer.Controls.Add(barraContratar);
            cardDer.Controls.Add(UI.TituloSeccion("Cliente seleccionado", "eh.nva.cli.sec"));

            cols.Controls.Add(cardIzq, 0, 0);
            cols.Controls.Add(cardDer, 1, 0);
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            p.Controls.Add(cols);
            return p;
        }

        // ── Paso ② Experiencia ──────────────────────────────────────────────────────
        private Panel ConstruirPasoExperiencia()
        {
            var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Estilo.Lienzo,
                                              Padding = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL) };
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            var cardIzq = Tarjeta(new Padding(0, 0, UI.Gap, 0));
            _gridExp = UI.Grilla();
            _gridExp.SelectionChanged += (s, e) => MostrarExperiencia();
            UI.ConectarEstadoVacio(_gridExp, "No hay experiencias para ofrecer.", "eh.res.exp.empty");

            _numInvitados = new NumericUpDown { Minimum = 0, Maximum = 50, Value = 0 };
            _numInvitados.ValueChanged += (s, e) => { if (!_ajustandoInvitados) RefrescarExperiencias(); };
            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White, WrapContents = false, AutoSize = false };
            filtros.Controls.Add(UI.FiltroCampo("Invitados", _numInvitados, 120, "eh.lbl.invitados"));

            cardIzq.Controls.Add(_gridExp);
            cardIzq.Controls.Add(filtros);
            cardIzq.Controls.Add(UI.TituloSeccion("Experiencias para el cliente", "eh.res.exp.listado"));

            var cardDer = Tarjeta(new Padding(UI.Gap, 0, 0, 0));
            var cuerpo = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            _expNombre = new Label { Dock = DockStyle.Top, Height = 30, Font = Estilo.SemiBold(13f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true, Text = "—" };
            _expFecha  = FilaDato("📅 —"); _expCiudad = FilaDato("📍 —"); _expCupo = FilaDato("🎟 —");
            var sep    = new Panel { Dock = DockStyle.Top, Height = 1, Margin = new Padding(0, 6, 0, 6), BackColor = Estilo.Gris200 };
            _expEstado = new Label { Dock = DockStyle.Top, Height = 44, Font = Estilo.Bold(10f), ForeColor = Estilo.Gris700, AutoEllipsis = true, Text = "—" };
            _expRecom  = new Label { Dock = DockStyle.Top, Height = 40, Font = Estilo.Normal(9.5f), ForeColor = Estilo.Coral, AutoEllipsis = true, Text = "" };
            cuerpo.Controls.Add(_expRecom); cuerpo.Controls.Add(_expEstado); cuerpo.Controls.Add(sep);
            cuerpo.Controls.Add(_expCupo); cuerpo.Controls.Add(_expCiudad); cuerpo.Controls.Add(_expFecha); cuerpo.Controls.Add(_expNombre);

            _btnListaEspera = UI.Secundario("Ingresar a lista de espera", "eh.res.btn.listaespera", (s, e) => IngresarListaEspera());
            var barra = UI.BarraAcciones(_btnListaEspera); barra.BackColor = Color.White;

            cardDer.Controls.Add(cuerpo);
            cardDer.Controls.Add(barra);
            cardDer.Controls.Add(UI.TituloSeccion("Detalle de la experiencia", "eh.res.exp.sec"));

            cols.Controls.Add(cardIzq, 0, 0);
            cols.Controls.Add(cardDer, 1, 0);
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Visible = false };
            p.Controls.Add(cols);
            return p;
        }

        // ── Paso ③ Confirmar ─────────────────────────────────────────────────────────
        private Panel ConstruirPasoConfirmar()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Visible = false,
                                Padding = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL) };
            var centro = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Estilo.Lienzo };
            centro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            centro.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 540));
            centro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var card = new TarjetaPanel { Dock = DockStyle.Top, Radio = 10, BackColor = Color.White, AutoSize = true,
                                          Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
            var resumen = UI.Resumen(); resumen.Dock = DockStyle.Top;
            _cfCliente     = UI.FilaResumenValor(resumen, "Cliente");
            _cfExperiencia = UI.FilaResumenValor(resumen, "Experiencia");
            _cfFecha       = UI.FilaResumenValor(resumen, "Fecha / hora");
            _cfLugares     = UI.FilaResumenValor(resumen, "Lugares");
            var aviso = new Label { Dock = DockStyle.Top, Height = 44, Tag = "eh.res.conf.aviso",
                Text = "Al confirmar, la reserva queda registrada y confirmada. Se podrá imprimir su comprobante.",
                ForeColor = Estilo.Gris700, Font = Estilo.Normal(9.5f), Padding = new Padding(2, 10, 0, 0) };
            card.Controls.Add(aviso);
            card.Controls.Add(resumen);
            card.Controls.Add(UI.TituloSeccion("Confirmación", "eh.res.sec.confirmacion"));
            centro.Controls.Add(card, 1, 0);
            p.Controls.Add(centro);
            return p;
        }

        private static TarjetaPanel Tarjeta(Padding margen)
            => new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White, Margin = margen,
                                  Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
        private static Label FilaDato(string texto)
            => new Label { Dock = DockStyle.Top, Height = 22, Font = Estilo.Normal(9.5f), ForeColor = Estilo.Gris700, AutoEllipsis = true, Text = texto };

        // ══════════════════════ PESTAÑA «RESERVAS EXISTENTES» ══════════════════════
        private Panel ConstruirTabExistentes()
        {
            var card = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                          Margin = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
            _grid = UI.Grilla();
            UI.ConectarEstadoVacio(_grid, "Todavía no hay reservas registradas.", "eh.empty.reservas");
            UI.PintarFilasPorEstado<BE.Reserva>(_grid, r => ColorEstado(r.Estado));
            _grid.SelectionChanged += (s, e) => ActualizarBotonesReserva();

            _btnCancelar = UI.Peligro("Cancelar", "eh.btn.cancelarreserva", (s, e) => Cancelar());
            _btnCancelar.Name = "btnCancelarReserva";
            _btnConfirmar = UI.Exito("Confirmar", "eh.btn.confirmar", (s, e) => ConfirmarConDialogo(Sel()));
            _btnConfirmacionPdf = UI.Secundario("Confirmación (PDF)", "eh.btn.confpdf", (s, e) => GenerarPdfSeleccionada());
            var acciones = UI.BarraAccionesLR(
                new[] { UI.Secundario("Refrescar", "eh.btn.refrescar", (s, e) => CargarExistentes()) },
                new[] { _btnConfirmacionPdf, _btnConfirmar, _btnCancelar });
            acciones.BackColor = Color.White;

            _cboFiltroCliente = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboFiltroCliente.SelectedIndexChanged += (s, e) => AplicarFiltro();
            _cboFiltroExperiencia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboFiltroExperiencia.SelectedIndexChanged += (s, e) => AplicarFiltro();
            _cboFiltroEstado = UI.ComboFiltroEstados<BE.EstadoReserva>(EstadoLabel, "Todos");
            _cboFiltroEstado.SelectedIndexChanged += (s, e) => AplicarFiltro();
            _lblContador = UI.Contador();
            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, WrapContents = false, AutoSize = false,
                                                BackColor = Color.White, Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, 0) };
            filtros.Controls.Add(UI.FiltroCampo("Cliente", _cboFiltroCliente, 210, "eh.lbl.cliente"));
            filtros.Controls.Add(UI.FiltroCampo("Experiencia", _cboFiltroExperiencia, 210, "eh.lbl.experiencia"));
            filtros.Controls.Add(UI.FiltroCampo("Estado", _cboFiltroEstado, 170, "eh.lbl.filtroestado"));
            filtros.Controls.Add(UI.FiltroCampo(" ", _lblContador, 160));

            card.Controls.Add(_grid);
            card.Controls.Add(acciones);
            card.Controls.Add(filtros);
            card.Controls.Add(UI.TituloSeccion("Reservas existentes", "eh.res.sec.existentes"));
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            p.Controls.Add(card);
            return p;
        }

        // ══════════════════════ PESTAÑA «ASISTENCIA Y VALORACIÓN» ══════════════════
        // Clasificación derivada de estados reales + fin de la experiencia (sin estados persistidos nuevos).
        private enum VistaAsis { Pendientes = 0, PendAsistencia, PendCalificar, Asistio, NoAsistio, Calificadas, Todas }

        private Panel ConstruirTabAsistencia()
        {
            var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Estilo.Lienzo,
                                              Padding = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL) };
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

            // ── Izquierda: indicadores + filtros + grilla ──
            var cardIzq = Tarjeta(new Padding(0, 0, UI.Gap, 0));
            _gridAsis = UI.Grilla();
            _gridAsis.CellFormatting += FormatearPuntajeAsis;
            UI.PintarFilasPorEstado<BE.Reserva>(_gridAsis, r => ColorEstado(r.Estado));
            _gridAsis.SelectionChanged += (s, e) => MostrarDetalleAsis();
            UI.ConectarEstadoVacio(_gridAsis, "No hay reservas para gestionar acá.", "eh.asis.empty");

            _txtBuscarAsis = new TextBox();
            _txtBuscarAsis.TextChanged += (s, e) => FiltrarAsistencia();
            _cboVistaAsis = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboVistaAsis.Items.AddRange(new object[]
            {
                I18n.T("eh.asis.vista.pendientes",     "Pendientes"),
                I18n.T("eh.asis.vista.pendasistencia", "Pendientes de asistencia"),
                I18n.T("eh.asis.vista.pendcalificar",  "Pendientes de calificar"),
                I18n.T("eh.res.estado.asistio",        "Asistió"),
                I18n.T("eh.res.estado.noasistio",      "No asistió"),
                I18n.T("eh.asis.vista.calificadas",    "Calificadas"),
                I18n.T("eh.le.filtro.todos",           "Todos"),
            });
            _cboVistaAsis.SelectedIndex = 0;
            _cboVistaAsis.SelectedIndexChanged += (s, e) => FiltrarAsistencia();

            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White, WrapContents = false, AutoSize = false };
            filtros.Controls.Add(UI.FiltroCampo("Buscar (cliente o experiencia)", _txtBuscarAsis, 240, "eh.res.buscar"));
            filtros.Controls.Add(UI.FiltroCampo("Ver", _cboVistaAsis, 210, "eh.asis.ver"));

            // Indicadores de actividad (chips).
            _lblIndAsis = new Label { Dock = DockStyle.Left, AutoSize = false, Width = 230, Font = Estilo.SemiBold(9.5f), ForeColor = Estilo.Naranja, TextAlign = ContentAlignment.MiddleLeft };
            _lblIndCal  = new Label { Dock = DockStyle.Left, AutoSize = false, Width = 230, Font = Estilo.SemiBold(9.5f), ForeColor = Estilo.AzulDeSeleccion, TextAlign = ContentAlignment.MiddleLeft };
            var inds = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.White, Padding = new Padding(2, 2, 0, 2) };
            inds.Controls.Add(_lblIndCal); inds.Controls.Add(_lblIndAsis);

            cardIzq.Controls.Add(_gridAsis);   // Fill
            cardIzq.Controls.Add(filtros);     // Top
            cardIzq.Controls.Add(inds);        // Top
            cardIzq.Controls.Add(UI.TituloSeccion("Asistencia y valoración", "eh.res.tab.asistencia")); // Top

            // ── Derecha: detalle del registro seleccionado + acciones contextuales ──
            var cardDer = Tarjeta(new Padding(UI.Gap, 0, 0, 0));
            _asisDetalle = new Label { Dock = DockStyle.Fill, Font = Estilo.Normal(9.5f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.TopLeft, Text = "" };
            var cuerpoDet = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(2, 4, 2, 4) };
            cuerpoDet.Controls.Add(_asisDetalle);

            _btnAsistio   = UI.Exito   ("Marcó asistencia", "eh.btn.asistio",   (s, e) => Asistencia(true));
            _btnNoAsistio = UI.Peligro ("No asistió",       "eh.btn.noasistio", (s, e) => Asistencia(false));
            _btnCalificar = UI.Primario("Calificar",        "eh.btn.calificar", (s, e) => Calificar());
            foreach (var b in new[] { _btnAsistio, _btnNoAsistio, _btnCalificar })
            { b.Dock = DockStyle.Top; b.Height = 34; b.Margin = new Padding(0, 0, 0, 6); }
            var barraAcc = new Panel { Dock = DockStyle.Bottom, Height = 124, BackColor = Color.White, Padding = new Padding(0, UI.Gap, 0, 0) };
            // Apilado inverso (Dock=Top): Calificar abajo, Asistió arriba.
            barraAcc.Controls.Add(_btnCalificar); barraAcc.Controls.Add(_btnNoAsistio); barraAcc.Controls.Add(_btnAsistio);

            cardDer.Controls.Add(cuerpoDet);   // Fill
            cardDer.Controls.Add(barraAcc);    // Bottom
            cardDer.Controls.Add(UI.TituloSeccion("Registro seleccionado", "eh.asis.detalle.sec")); // Top

            cols.Controls.Add(cardIzq, 0, 0);
            cols.Controls.Add(cardDer, 1, 0);
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            p.Controls.Add(cols);
            return p;
        }

        // Fin real de la experiencia de una reserva: inicio (JOIN) + duración (JOIN). Null si no hay inicio.
        private static DateTime? FinExperiencia(BE.Reserva r)
            => r.FechaHoraExperiencia.HasValue
                ? (DateTime?)r.FechaHoraExperiencia.Value.AddMinutes(r.DuracionExperienciaMinutos)
                : null;

        // Confirmada cuya experiencia YA finalizó → candidata real a registrar asistencia.
        private static bool FinalizadaParaAsistencia(BE.Reserva r)
        {
            if (r.Estado != BE.EstadoReserva.Confirmada) return false;
            var fin = FinExperiencia(r);
            return fin.HasValue && fin.Value <= DateTime.Now;
        }

        private void CargarAsistencia()
        {
            if (_gridAsis == null) return;
            try
            {
                _realizadas = _bll.ObtenerRealizadas();   // Confirmada / Asistió / NoAsistió
                var puntajes = _bllCal.ObtenerPuntajesPorReserva();
                foreach (var r in _realizadas)
                    if (puntajes.TryGetValue(r.IdReserva, out int pt)) r.Puntaje = pt;
                FiltrarAsistencia();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void FiltrarAsistencia()
        {
            if (_gridAsis == null) return;
            try
            {
                string q = (_txtBuscarAsis?.Text ?? "").Trim();
                var vista = (VistaAsis)(_cboVistaAsis?.SelectedIndex ?? 0);

                bool PendAsis(BE.Reserva r)  => FinalizadaParaAsistencia(r);
                bool PendCalif(BE.Reserva r) => r.Estado == BE.EstadoReserva.Asistio && r.Puntaje == null;

                var lista = _realizadas.FindAll(r =>
                {
                    if (!string.IsNullOrEmpty(q)
                        && (r.NombreCliente ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                        && (r.NombreExperiencia ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) return false;
                    switch (vista)
                    {
                        case VistaAsis.Pendientes:      return PendAsis(r) || PendCalif(r);
                        case VistaAsis.PendAsistencia:  return PendAsis(r);
                        case VistaAsis.PendCalificar:   return PendCalif(r);
                        case VistaAsis.Asistio:         return r.Estado == BE.EstadoReserva.Asistio;
                        case VistaAsis.NoAsistio:       return r.Estado == BE.EstadoReserva.NoAsistio;
                        case VistaAsis.Calificadas:     return r.Estado == BE.EstadoReserva.Asistio && r.Puntaje != null;
                        default:                        return true;   // Todas
                    }
                });
                // Prioridad visual: lo accionable primero (pend. asistencia, luego pend. calificar).
                lista.Sort((a, b) => Rango(a).CompareTo(Rango(b)));

                _gridAsis.DataSource = lista;
                UI.Columnas(_gridAsis, ("NombreCliente", 22), ("NombreExperiencia", 26),
                                       ("FechaHoraExperiencia", 20), ("Estado", 16), ("Puntaje", 12));
                if (_gridAsis.Columns.Contains("FechaHoraExperiencia"))
                    _gridAsis.Columns["FechaHoraExperiencia"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
                HeaderAsis();

                int nAsis = _realizadas.FindAll(PendAsis).Count;
                int nCal  = _realizadas.FindAll(PendCalif).Count;
                if (_lblIndAsis != null) _lblIndAsis.Text = "● " + string.Format(I18n.T("eh.asis.ind.asistencia", "Pendientes de asistencia: {0}"), nAsis);
                if (_lblIndCal  != null) _lblIndCal.Text  = "● " + string.Format(I18n.T("eh.asis.ind.calificar",   "Pendientes de calificar: {0}"), nCal);

                MostrarDetalleAsis();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private static int Rango(BE.Reserva r)
        {
            if (FinalizadaParaAsistencia(r)) return 0;                                   // pend. asistencia
            if (r.Estado == BE.EstadoReserva.Asistio && r.Puntaje == null) return 1;      // pend. calificar
            if (r.Estado == BE.EstadoReserva.Confirmada) return 2;                        // confirmada futura
            return 3;                                                                     // cerradas
        }

        private void HeaderAsis()
        {
            void H(string col, string clave, string fb) { if (_gridAsis.Columns.Contains(col)) _gridAsis.Columns[col].HeaderText = I18n.T(clave, fb); }
            H("NombreCliente",        "eh.le.col.cliente",   "Cliente");
            H("NombreExperiencia",    "eh.lbl.experiencia",  "Experiencia");
            H("FechaHoraExperiencia", "eh.asis.col.fechaexp","Fecha de la experiencia");
            H("Estado",               "eh.le.col.estado",    "Estado");
            H("Puntaje",              "eh.asis.col.punt",    "Calificación");
        }

        private void FormatearPuntajeAsis(object s, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = _gridAsis.Columns[e.ColumnIndex].Name;
            if (col == "Puntaje")
            {
                e.Value = (e.Value == null || e.Value == DBNull.Value) ? "—" : "★ " + e.Value + "/5";
                e.FormattingApplied = true;
                e.CellStyle.ForeColor = (e.Value as string) == "—" ? Estilo.Gris400 : Estilo.Naranja;
            }
            else if (col == "Estado" && e.Value is BE.EstadoReserva st)
            { e.Value = EstadoLabel(st); e.FormattingApplied = true; }
        }

        private BE.Reserva SelAsis() => _gridAsis?.CurrentRow?.DataBoundItem as BE.Reserva;

        private void MostrarDetalleAsis()
        {
            var r = SelAsis();
            // Elegibilidad por regla (BLL) + madurez temporal (fin ≤ ahora) + permiso de edición.
            bool puedeAsis  = _puedeRealizadasEditar && r != null && r.PuedeRegistrarAsistencia() && FinalizadaParaAsistencia(r);
            bool puedeCalif = _puedeRealizadasEditar && r != null && r.PuedeCalificarse() && r.Puntaje == null;
            if (_btnAsistio   != null) _btnAsistio.Enabled   = puedeAsis;
            if (_btnNoAsistio != null) _btnNoAsistio.Enabled = puedeAsis;
            if (_btnCalificar != null) _btnCalificar.Enabled = puedeCalif;

            if (_asisDetalle == null) return;
            if (r == null) { _asisDetalle.Text = I18n.T("eh.asis.detalle.vacio", "Elegí un registro para ver su detalle."); return; }

            var fin = FinExperiencia(r);
            string estadoTmp = r.Estado == BE.EstadoReserva.Confirmada
                ? (FinalizadaParaAsistencia(r) ? I18n.T("eh.asis.clasif.pendasis", "Pendiente de registrar asistencia")
                                               : I18n.T("eh.asis.clasif.proxima", "Experiencia aún no finalizada"))
                : r.Estado == BE.EstadoReserva.Asistio
                    ? (r.Puntaje == null ? I18n.T("eh.asis.clasif.pendcal", "Asistió · pendiente de calificar")
                                         : I18n.T("eh.asis.clasif.calificada", "Asistió · calificada"))
                    : I18n.T("eh.asis.clasif.noasis", "No asistió");

            _asisDetalle.Text =
                $"{I18n.T("eh.le.col.cliente", "Cliente")}: {r.NombreCliente ?? "—"}\r\n" +
                $"{I18n.T("eh.lbl.experiencia", "Experiencia")}: {r.NombreExperiencia ?? "—"}\r\n" +
                $"{I18n.T("eh.asis.col.fechaexp", "Fecha de la experiencia")}: {(r.FechaHoraExperiencia?.ToString("dd/MM/yyyy HH:mm") ?? "—")}\r\n" +
                $"{I18n.T("eh.asis.finaliza", "Finaliza")}: {(fin?.ToString("dd/MM/yyyy HH:mm") ?? "—")}\r\n" +
                $"{I18n.T("eh.le.col.estado", "Estado")}: {EstadoLabel(r.Estado)}\r\n" +
                $"{I18n.T("eh.asis.col.punt", "Calificación")}: {(r.Puntaje == null ? "—" : "★ " + r.Puntaje + "/5")}\r\n\r\n" +
                $"» {estadoTmp}";
        }

        private void Asistencia(bool asistio)
        {
            var r = SelAsis();
            if (r == null) { MostrarError(I18n.T("eh.res.err.selreserva", "Seleccioná una reserva del listado.")); return; }
            if (!r.PuedeRegistrarAsistencia())
            { MostrarError(I18n.T("eh.asis.err.estado", "Solo se registra la asistencia de reservas confirmadas.")); return; }
            if (!FinalizadaParaAsistencia(r))
            { MostrarError(I18n.T("eh.asis.err.futura", "La experiencia todavía no finalizó.")); return; }
            try
            {
                _bll.RegistrarAsistencia(MODULO_ASIS, r, asistio);
                MostrarOk(asistio ? I18n.T("eh.asis.ok.asistio", "Asistencia registrada.")
                                  : I18n.T("eh.asis.ok.noasistio", "Inasistencia registrada."));
                CargarAsistencia();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Calificar()
        {
            var r = SelAsis();
            if (r == null) { MostrarError(I18n.T("eh.res.err.selreserva", "Seleccioná una reserva del listado.")); return; }
            if (!r.PuedeCalificarse())
            { MostrarError(I18n.T("eh.asis.err.calif", "Solo se califica una reserva con asistencia registrada.")); return; }
            if (!PedirCalificacion(out int puntaje, out string comentario)) return;
            try
            {
                _bllCal.Calificar(MODULO_ASIS, r, puntaje, comentario);
                Estilo.Exito(this, I18n.T("eh.asis.grac.titulo", "¡Gracias por calificar!"),
                    I18n.T("eh.asis.grac.cuerpo", "Tu opinión ayuda a mejorar las recomendaciones."));
                CargarAsistencia();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Diálogo único: nota (1–5) + comentario opcional (idéntico al de ReservasRealizadas).
        private bool PedirCalificacion(out int puntaje, out string comentario)
        {
            puntaje = 5; comentario = "";
            using (var dlg = new Form
            {
                Text = I18n.T("eh.asis.calif.titulo", "Calificar experiencia"), FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, 210),
                MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Color.White
            })
            {
                var lblP = new Label { Text = I18n.T("eh.asis.calif.punt", "Puntaje (1 a 5) *"), Location = new Point(18, 18), AutoSize = true, ForeColor = Estilo.AzulOscuro, Font = Estilo.Bold(9.5f) };
                var num  = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 5, Location = new Point(18, 42), Width = 90 };
                var lblC = new Label { Text = I18n.T("eh.asis.calif.coment", "Comentario (opcional)"), Location = new Point(18, 82), AutoSize = true, ForeColor = Estilo.AzulOscuro, Font = Estilo.Bold(9.5f) };
                var txt  = new TextBox { Location = new Point(18, 106), Width = 344, Height = 44, Multiline = true };
                var btnOk     = UI.Primario("Guardar",  "eh.btn.guardar",  null);
                var btnCancel = UI.Secundario("Cancelar","eh.btn.cancelar", null);
                btnOk.DialogResult = DialogResult.OK;      btnOk.Location = new Point(178, 162);
                btnCancel.DialogResult = DialogResult.Cancel; btnCancel.Location = new Point(288, 162);
                dlg.Controls.AddRange(new Control[] { lblP, num, lblC, txt, btnOk, btnCancel });
                dlg.AcceptButton = btnOk; dlg.CancelButton = btnCancel;
                Estilo.Aplicar(dlg);
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                puntaje = (int)num.Value; comentario = txt.Text.Trim();
                return true;
            }
        }

        // ══════════════════════ CICLO DE VIDA ══════════════════════════════════════
        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir()
        {
            this.Text = I18n.T("eh.frm.reservas", this.Text);
            I18n.TraducirControles(this);
            if (_pasoBar != null)
                _pasoBar.Pasos = new[]
                {
                    I18n.T("eh.res.paso.cliente",      "Cliente"),
                    I18n.T("eh.res.paso.experiencia",  "Experiencia"),
                    I18n.T("eh.res.paso.confirmacion", "Confirmación")
                };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            if (_puedeReservas)
            {
                RecargarClientes();
                CargarExistentes();
                Ir(1);
            }
            if (_puedeRealizadas) CargarAsistencia();
            Traducir();
        }

        // ══════════════════════ NAVEGACIÓN DEL ASISTENTE ═══════════════════════════
        private void Ir(int paso)
        {
            if (paso < 1 || paso > 3) return;
            if (paso >= 2 && !_clienteOk)
            { MostrarError(I18n.T("eh.res.err.clienteno", "Este cliente no puede reservar. Revisá su suscripción o su reserva pendiente.")); return; }
            if (paso == 2) { AplicarLimiteInvitados(); RefrescarExperiencias(); }
            if (paso == 3)
            {
                if (_expSel == null || !(_compat.TryGetValue(_expSel.IdExperiencia, out var c) && c.EsReservable))
                { MostrarError(I18n.T("eh.res.err.expno", "Elegí una experiencia disponible para continuar.")); return; }
                PrepararConfirmacion();
            }
            _paso = paso;
            _pasoCliente.Visible     = paso == 1;
            _pasoExperiencia.Visible = paso == 2;
            _pasoConfirmar.Visible   = paso == 3;
            ActualizarNav();
        }

        private void ActualizarNav()
        {
            if (_pasoBar != null) _pasoBar.Current = _paso - 1;
            _btnAtras.Visible     = _paso > 1;
            _btnSiguiente.Visible = _paso < 3;
            _btnReservar.Visible  = _paso == 3;
            _btnOtra.Visible      = false;
            _btnReservar.Enabled  = _paso == 3;
            _btnSiguiente.Enabled =
                (_paso == 1 && _clienteOk) ||
                (_paso == 2 && _expSel != null && _compat.TryGetValue(_expSel.IdExperiencia, out var c) && c.EsReservable);
        }

        // ══════════════════════ PASO ① · CLIENTE ═══════════════════════════════════
        private void RecargarClientes()
        {
            try { _todosClientes = _bllCli.ObtenerTodos(); FiltrarClientes(); }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void FiltrarClientes()
        {
            if (_gridCli == null) return;
            int? sel = _clienteSel?.IdCliente;
            string q = (_txtBuscarCli?.Text ?? "").Trim();
            bool soloPueden = _cboMostrar != null && _cboMostrar.SelectedIndex == 1;

            var lista = _todosClientes.FindAll(c =>
                (string.IsNullOrEmpty(q)
                    || (c.NombreCompleto ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (c.DNI ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                && (!soloPueden || (c.Suscripcion != null && c.Suscripcion.PuedeReservar())));

            var t = new DataTable();
            t.Columns.Add("Nombre", typeof(string));
            t.Columns.Add("DNI", typeof(string));
            t.Columns.Add("Ciudad", typeof(string));
            t.Columns.Add("Situacion", typeof(string));
            t.Columns.Add("_Id", typeof(int));
            t.Columns.Add("_Cat", typeof(int));
            foreach (var c in lista)
            {
                var cat = CategoriaCliente(c);
                t.Rows.Add(c.NombreCompleto, c.DNI, c.NombreCiudad ?? "—", EtiquetaSituacion(cat), c.IdCliente, (int)cat);
            }
            _gridCli.DataSource = t;
            UI.Columnas(_gridCli, ("Nombre", 32), ("DNI", 18), ("Ciudad", 20), ("Situacion", 30));
            if (_gridCli.Columns.Contains("_Id"))  _gridCli.Columns["_Id"].Visible = false;
            if (_gridCli.Columns.Contains("_Cat")) _gridCli.Columns["_Cat"].Visible = false;
            if (_gridCli.Columns.Contains("Situacion")) _gridCli.Columns["Situacion"].HeaderText = I18n.T("eh.nva.cli.situacion", "Situación");
            ColorearGridCli();
            if (sel.HasValue)
                foreach (DataGridViewRow r in _gridCli.Rows)
                    if (r.Cells["_Id"].Value is int id && id == sel.Value) { r.Selected = true; break; }
            MostrarCliente();
        }

        private SitCat CategoriaCliente(BE.Cliente c)
        {
            var s = c.Suscripcion;
            if (s == null) return SitCat.SinSus;
            if (s.Estado == BE.EstadoSuscripcion.Suspendida) return SitCat.Suspendida;
            if (s.EstadoVigenciaCalculado(DateTime.Today) == BE.EstadoSuscripcion.Vencida) return SitCat.Vencida;
            if (s.EstaVigente() && s.ProximaAVencer()) return SitCat.PorVencer;
            return SitCat.Activa;
        }

        private static string EtiquetaSituacion(SitCat cat)
        {
            switch (cat)
            {
                case SitCat.Activa:     return I18n.T("eh.nva.sit.activa",     "Suscripción activa");
                case SitCat.PorVencer:  return "⏰ " + I18n.T("eh.nva.sit.porvencer", "Por vencer");
                case SitCat.Vencida:    return "⚠ " + I18n.T("eh.nva.sit.vencida",   "Vencida");
                case SitCat.Suspendida: return I18n.T("eh.nva.sit.suspendida", "Suspendida");
                default:                return I18n.T("eh.nva.sit.sinsus",     "Sin suscripción");
            }
        }

        private void ColorearGridCli()
        {
            if (!_gridCli.Columns.Contains("_Cat")) return;
            foreach (DataGridViewRow row in _gridCli.Rows)
            {
                if (!(row.Cells["_Cat"].Value is int ci)) continue;
                Color col;
                switch ((SitCat)ci)
                {
                    case SitCat.Activa:     col = Estilo.Verde;   break;
                    case SitCat.PorVencer:  col = Estilo.Naranja; break;
                    case SitCat.Vencida:    col = Estilo.Rojo;    break;
                    case SitCat.Suspendida: col = Estilo.Rojo;    break;
                    default:                col = Estilo.Gris700; break;
                }
                if (_gridCli.Columns.Contains("Situacion")) row.Cells["Situacion"].Style.ForeColor = col;
            }
        }

        private BE.Cliente ClienteDeGrilla()
        {
            if (_gridCli.CurrentRow?.DataBoundItem is DataRowView drv && drv.Row.Table.Columns.Contains("_Id"))
            {
                int id = Convert.ToInt32(drv["_Id"]);
                return _todosClientes.Find(c => c.IdCliente == id);
            }
            return null;
        }

        private void MostrarCliente()
        {
            var nuevo = ClienteDeGrilla();
            if (nuevo?.IdCliente != _clienteSel?.IdCliente) _expSel = null;   // cambió el cliente → invalida la experiencia
            _clienteSel = nuevo;
            _clienteOk = false;
            if (_btnContratar != null) _btnContratar.Visible = false;

            if (_clienteSel == null)
            {
                _cliNombre.Text = I18n.T("eh.nva.cli.ninguno", "Ningún cliente seleccionado"); _cliNombre.ForeColor = Estilo.Gris400;
                _cliDni.Text = "DNI —"; _cliCiudad.Text = $"{I18n.T("eh.lbl.ciudad", "Ciudad")} —"; _cliEmail.Text = "Email —";
                _cliEstadoSus.Text = "—"; _cliEstadoSus.ForeColor = Estilo.Gris700;
                _cliVence.Text = $"{I18n.T("eh.cli.sus.vence", "Vence")} —"; _cliFidelidad.Text = "";
                _cliHabilitado.Text = ""; _cliAlerta.Visible = false;
                ActualizarNav(); return;
            }

            // ① Datos personales.
            _cliNombre.Text = _clienteSel.NombreCompleto; _cliNombre.ForeColor = Estilo.AzulOscuro;
            _cliDni.Text    = $"DNI {_clienteSel.DNI}";
            _cliCiudad.Text = $"{I18n.T("eh.lbl.ciudad", "Ciudad")}: {_clienteSel.NombreCiudad ?? "—"}";
            _cliEmail.Text  = $"Email: {_clienteSel.Email ?? "—"}";

            try
            {
                // Estado REAL desde la suscripción ya hidratada (distingue Vigente/Por vencer/Vencida/
                // Suspendida/Sin suscripción). No se usa la "vigente" sola, que confundía Vencida/Suspendida
                // con "Sin suscripción".
                var sus = _clienteSel.Suscripcion;
                var cat = CategoriaCliente(_clienteSel);
                bool vigente = sus != null && sus.EstaVigente();

                // Tope de invitados según el PLAN del cliente (regla existente: plan.CantidadInvitados).
                // Plan básico (0) → no se podrán elegir invitados en el paso ②.
                _maxInvitados = 0;
                try { if (sus != null) { var plan = _bllPlan.ObtenerPorId(sus.IdPlan); _maxInvitados = plan?.CantidadInvitados ?? 0; } }
                catch { _maxInvitados = 0; }

                // ② Suscripción (dato factual: plan · estado · saldo).
                if (sus == null)
                {
                    _cliEstadoSus.Text = I18n.T("eh.nva.sit.sinsus", "Sin suscripción"); _cliEstadoSus.ForeColor = Estilo.Gris700;
                    _cliVence.Text = $"{I18n.T("eh.cli.sus.vence", "Vence")} —";
                }
                else
                {
                    _cliEstadoSus.Text = $"● {sus.NombrePlan} · {EtiquetaSituacion(cat)} · " +
                        string.Format(I18n.T("eh.res.saldomes", "{0} de {1} este mes"), sus.ReservasRestantes(), sus.ReservasDelPlan);
                    _cliEstadoSus.ForeColor = vigente ? (cat == SitCat.PorVencer ? Estilo.Naranja : Estilo.Verde) : Estilo.Rojo;
                    _cliVence.Text = sus.FechaVencimiento.HasValue
                        ? $"{I18n.T("eh.cli.sus.vence", "Vence")}: {sus.FechaVencimiento.Value:dd/MM/yyyy}"
                        : $"{I18n.T("eh.cli.sus.vence", "Vence")}: {I18n.T("eh.cli.sus.sinlimite", "sin límite")}";
                }

                // ③ Fidelidad.
                try { _cliFidelidad.Text = "★ " + BE.Fidelidad.Etiqueta(_bllRec.NivelFidelidad(_clienteSel.IdCliente)); } catch { _cliFidelidad.Text = ""; }

                // ④ Reserva pendiente bloqueante (regla vigente, sin cambios).
                bool tienePendiente = false; int? idPend = null;
                try { var sit = _bll.ConsultarSituacionCliente(_clienteSel.IdCliente); tienePendiente = sit.TieneReservaActiva; idPend = sit.IdReservaActiva; } catch { }

                bool conCupo = vigente && sus != null && sus.ReservasRestantes() > 0;
                _clienteOk = vigente && conCupo && !tienePendiente;

                // ⑤ Veredicto claro + alerta ACCIONABLE (no repite el estado) + continuidad comercial.
                if (_clienteOk)
                {
                    _cliHabilitado.Text = I18n.T("eh.res.puede", "✓ Puede reservar"); _cliHabilitado.ForeColor = Estilo.Verde;
                    _cliAlerta.Visible = false;
                }
                else
                {
                    _cliHabilitado.Text = I18n.T("eh.res.nopuede", "✗ No puede reservar"); _cliHabilitado.ForeColor = Estilo.Rojo;
                    if (tienePendiente)
                        MostrarAlerta(Estilo.Amarillo, Estilo.AzulOscuro,
                            string.Format(I18n.T("eh.res.aviso.pendiente", "⚠ Reserva pendiente #{0} sin resolver — confirmala o cancelala"), idPend));
                    else if (sus == null)
                    { MostrarAlerta(Estilo.Amarillo, Estilo.AzulOscuro, I18n.T("eh.res.alerta.sinsus", "Sin suscripción. Contratá un plan para poder reservar.")); _btnContratar.Visible = true; }
                    else if (cat == SitCat.Suspendida)
                        MostrarAlerta(Estilo.Amarillo, Estilo.AzulOscuro, I18n.T("eh.res.alerta.suspendida", "Suscripción suspendida. Reactivala desde Clientes."));
                    else if (!vigente)   // Vencida
                    { MostrarAlerta(Estilo.Amarillo, Estilo.AzulOscuro, I18n.T("eh.res.alerta.vencida", "Suscripción vencida. Renovala para poder reservar.")); _btnContratar.Visible = true; }
                    else                 // vigente pero sin cupo mensual
                        MostrarAlerta(Estilo.Amarillo, Estilo.AzulOscuro, I18n.T("eh.res.aviso.sincupo", "⚠ Sin reservas disponibles este mes"));
                }
            }
            catch (Exception ex) { _cliEstadoSus.Text = ex.Message; _cliAlerta.Visible = false; }
            ActualizarNav();
        }

        // Continuidad comercial: delega en el Menú abrir PN01 «Nueva contratación» con este cliente.
        private void IrAContratacion()
        {
            if (_clienteSel == null) return;
            SolicitaContratar?.Invoke(_clienteSel.IdCliente);
        }

        private void MostrarAlerta(Color fondo, Color texto, string msg)
        {
            _cliAlerta.BackColor = fondo; _cliAlertaLbl.ForeColor = texto; _cliAlertaLbl.Text = msg; _cliAlerta.Visible = true;
        }

        // ══════════════════════ PASO ② · EXPERIENCIA ═══════════════════════════════
        private void RefrescarExperiencias()
        {
            if (_clienteSel == null || _gridExp == null) return;
            try
            {
                int? sel = _expSel?.IdExperiencia;
                _experiencias = _bllExp.ObtenerParaReserva();
                _compat = _bll.EvaluarCompatibilidades(_clienteSel.IdCliente, _experiencias, (int)_numInvitados.Value);

                // Recomendación (solo si es compatible con el cliente).
                _idRecomendado = null; _motivoRecom = null;
                try
                {
                    var recs = _bllRec.RecomendarPara(_clienteSel.IdCliente, 3);
                    foreach (var rec in recs)
                        if (_compat.TryGetValue(rec.Experiencia.IdExperiencia, out var cc) && cc.EsReservable)
                        { _idRecomendado = rec.Experiencia.IdExperiencia; _motivoRecom = rec.Motivo; break; }
                }
                catch { }

                var ordenadas = new List<BE.Experiencia>(_experiencias);
                ordenadas.Sort((a, b) =>
                {
                    int oa = Orden(a), ob = Orden(b);
                    if (oa != ob) return oa.CompareTo(ob);
                    // Dentro del mismo grupo, la sugerida va primero (sin seleccionarla automáticamente).
                    bool ra = _idRecomendado.HasValue && a.IdExperiencia == _idRecomendado.Value;
                    bool rb = _idRecomendado.HasValue && b.IdExperiencia == _idRecomendado.Value;
                    if (ra != rb) return ra ? -1 : 1;
                    return a.FechaHoraInicio.CompareTo(b.FechaHoraInicio);
                });

                var t = new DataTable();
                t.Columns.Add("Experiencia", typeof(string));
                t.Columns.Add("Fecha", typeof(string));
                t.Columns.Add("Cupo", typeof(string));
                t.Columns.Add("Estado", typeof(string));
                t.Columns.Add("_Id", typeof(int));
                t.Columns.Add("_Cat", typeof(int));
                foreach (var e in ordenadas)
                {
                    var c = _compat.ContainsKey(e.IdExperiencia) ? _compat[e.IdExperiencia] : null;
                    bool sug = _idRecomendado.HasValue && e.IdExperiencia == _idRecomendado.Value;
                    t.Rows.Add((sug ? "★ " : "") + e.Nombre,
                        e.FechaHoraInicio.ToString("dd/MM HH:mm"),
                        $"{e.CupoDisponible}/{e.CupoMaximo}",
                        EstadoCompatTexto(c),
                        e.IdExperiencia,
                        c != null ? (int)c.Estado : (int)BE.EstadoCompatibilidad.Incompatible);
                }
                _gridExp.DataSource = t;
                UI.Columnas(_gridExp, ("Experiencia", 34), ("Fecha", 16), ("Cupo", 12), ("Estado", 38));
                if (_gridExp.Columns.Contains("_Id"))  _gridExp.Columns["_Id"].Visible = false;
                if (_gridExp.Columns.Contains("_Cat")) _gridExp.Columns["_Cat"].Visible = false;
                ColorearGridExp();

                if (sel.HasValue)
                    foreach (DataGridViewRow r in _gridExp.Rows)
                        if (r.Cells["_Id"].Value is int id && id == sel.Value) { r.Selected = true; break; }
                MostrarExperiencia();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Orden de presentación: disponibles primero; incompatibles al final.
        private int Orden(BE.Experiencia e)
        {
            if (!_compat.TryGetValue(e.IdExperiencia, out var c)) return 9;
            switch (c.Estado)
            {
                case BE.EstadoCompatibilidad.Disponible:           return 0;
                case BE.EstadoCompatibilidad.SinLugar:             return 1;
                case BE.EstadoCompatibilidad.ListaEspera:          return 2;
                case BE.EstadoCompatibilidad.ListaEsperaSinOferta: return 3;
                case BE.EstadoCompatibilidad.YaEnCola:             return 3;
                default:                                           return 4;  // Incompatible
            }
        }

        private static string EstadoCompatTexto(BE.CompatibilidadReserva c)
        {
            if (c == null) return "—";
            switch (c.Estado)
            {
                case BE.EstadoCompatibilidad.Disponible:           return I18n.T("eh.res.compat.disponible", "Disponible");
                case BE.EstadoCompatibilidad.SinLugar:             return string.Format(I18n.T("eh.res.compat.sinlugar", "Quedan {0} lugar(es)"), c.LugaresDisponibles);
                case BE.EstadoCompatibilidad.ListaEspera:          return I18n.T("eh.res.compat.completa", "Completa — lista de espera");
                case BE.EstadoCompatibilidad.ListaEsperaSinOferta: return I18n.T("eh.res.compat.sinoferta", "Completa (sin tiempo para oferta)");
                case BE.EstadoCompatibilidad.YaEnCola:             return I18n.T("eh.res.compat.yaencola", "Ya está en lista de espera");
                default:                                           return string.Format(I18n.T(c.MotivoClave, c.MotivoFallback ?? "No disponible"), c.MotivoArgs ?? new object[0]);
            }
        }

        private static Color ColorCompat(BE.EstadoCompatibilidad e)
        {
            switch (e)
            {
                case BE.EstadoCompatibilidad.Disponible:  return Estilo.Verde;
                case BE.EstadoCompatibilidad.SinLugar:    return Estilo.Naranja;
                case BE.EstadoCompatibilidad.ListaEspera: return Estilo.Naranja;
                case BE.EstadoCompatibilidad.Incompatible:return Estilo.Rojo;
                default:                                  return Estilo.Gris700;
            }
        }

        private void ColorearGridExp()
        {
            if (!_gridExp.Columns.Contains("_Cat")) return;
            foreach (DataGridViewRow row in _gridExp.Rows)
                if (row.Cells["_Cat"].Value is int ci && _gridExp.Columns.Contains("Estado"))
                    row.Cells["Estado"].Style.ForeColor = ColorCompat((BE.EstadoCompatibilidad)ci);
        }

        private BE.Experiencia ExpDeGrilla()
        {
            if (_gridExp.CurrentRow?.DataBoundItem is DataRowView drv && drv.Row.Table.Columns.Contains("_Id"))
            {
                int id = Convert.ToInt32(drv["_Id"]);
                return _experiencias.Find(e => e.IdExperiencia == id);
            }
            return null;
        }

        private void MostrarExperiencia()
        {
            _expSel = ExpDeGrilla();
            BE.CompatibilidadReserva c = (_expSel != null && _compat.ContainsKey(_expSel.IdExperiencia)) ? _compat[_expSel.IdExperiencia] : null;

            if (_expSel == null)
            {
                _expNombre.Text = I18n.T("eh.res.exp.ninguna", "Elegí una experiencia"); _expNombre.ForeColor = Estilo.Gris400;
                _expFecha.Text = "📅 —"; _expCiudad.Text = "📍 —"; _expCupo.Text = "🎟 —";
                _expEstado.Text = "—"; _expEstado.ForeColor = Estilo.Gris700; _expRecom.Text = "";
                if (_btnListaEspera != null) _btnListaEspera.Visible = false;
                ActualizarNav(); return;
            }

            _expNombre.Text = _expSel.Nombre; _expNombre.ForeColor = Estilo.AzulOscuro;
            _expFecha.Text  = "📅 " + _expSel.FechaHoraInicio.ToString("dd/MM/yyyy HH:mm");
            _expCiudad.Text = "📍 " + (_expSel.NombreCiudad ?? "—");
            _expCupo.Text   = "🎟 " + $"{_expSel.CupoDisponible}/{_expSel.CupoMaximo}";
            _expEstado.Text = EstadoCompatTexto(c);
            _expEstado.ForeColor = c != null ? ColorCompat(c.Estado) : Estilo.Gris700;
            _expRecom.Text  = (_idRecomendado.HasValue && _expSel.IdExperiencia == _idRecomendado.Value && !string.IsNullOrEmpty(_motivoRecom))
                ? "★ " + string.Format(I18n.T("eh.res.sugeridapara", "Sugerida para {0}: {1}"), _clienteSel?.Nombre ?? "", _motivoRecom)
                : "";

            // «Lista de espera» SOLO si la experiencia es compatible salvo cupo y encolable (punto 1 y 6).
            if (_btnListaEspera != null)
                _btnListaEspera.Visible = c != null && c.PuedeListaEspera;

            ActualizarNav();
        }

        // Capa el selector de invitados al máximo del plan del cliente (CantidadInvitados). Si el plan
        // no admite invitados (básico → 0), lo deshabilita. Refleja una regla existente; no la cambia.
        private void AplicarLimiteInvitados()
        {
            if (_numInvitados == null) return;
            _ajustandoInvitados = true;
            int max = Math.Max(0, _maxInvitados);
            if (_numInvitados.Value > max) _numInvitados.Value = max;
            _numInvitados.Maximum = max;
            _numInvitados.Enabled = max > 0;
            _ajustandoInvitados = false;
        }

        private void IngresarListaEspera()
        {
            if (_clienteSel == null || _expSel == null) { MostrarError(I18n.T("eh.res.err.selcliexp", "Seleccioná un cliente y una experiencia.")); return; }
            try
            {
                int pos = _bllEsp.Ingresar(MODULO, _expSel.IdExperiencia, _clienteSel.IdCliente);
                Estilo.Info(this, I18n.T("eh.res.espera.titulo", "En lista de espera"),
                    string.Format(I18n.T("eh.res.espera.cuerpo", "Te avisamos si se libera un lugar. Posición {0} en la fila."), pos));
                RefrescarExperiencias();
                CargarExistentes();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // ══════════════════════ PASO ③ · CONFIRMAR ═════════════════════════════════
        private void PrepararConfirmacion()
        {
            _cfCliente.Text     = _clienteSel?.NombreCompleto ?? "—";
            _cfExperiencia.Text = _expSel?.Nombre ?? "—";
            _cfFecha.Text       = _expSel != null ? _expSel.FechaHoraInicio.ToString("dd/MM/yyyy HH:mm") : "—";
            _cfLugares.Text     = (1 + (int)_numInvitados.Value).ToString();
            _btnReservar.Enabled = true;
        }

        private void ReservarYConfirmar()
        {
            if (_clienteSel == null || _expSel == null)
            { MostrarError(I18n.T("eh.res.err.selcliexp", "Seleccioná un cliente y una experiencia.")); return; }

            int idCli = _clienteSel.IdCliente, idExp = _expSel.IdExperiencia;
            string cuerpo = string.Format(
                I18n.T("eh.res.reservar.cuerpo",
                    "Cliente: {0}\nExperiencia: {1}\nFecha y horario: {2}\nLugares: {3}\n\nSe creará y confirmará la reserva.\n\n¿Confirmás la operación?"),
                _clienteSel.NombreCompleto, _expSel.Nombre,
                _expSel.FechaHoraInicio.ToString("dd/MM/yyyy HH:mm"), 1 + (int)_numInvitados.Value);
            if (!Estilo.Confirmar(this, I18n.T("eh.res.reservar.titulo", "Reservar y confirmar"), cuerpo)) return;

            int idNuevo;
            try { idNuevo = _bll.CrearReserva(MODULO, idCli, idExp, (int)_numInvitados.Value); }
            catch (BE.AppException ax) when (ax.Clave == "err.bll.reserva.sin_cupo")
            {
                if (Estilo.Confirmar(this, I18n.T("eh.res.sincupo.titulo", "Sin cupo"),
                        I18n.T("eh.res.sincupo.cuerpo", "La experiencia está completa. ¿Querés ingresar al cliente a la lista de espera?")))
                { try { int pos = _bllEsp.Ingresar(MODULO, idExp, idCli); Estilo.Info(this, I18n.T("eh.res.espera.titulo", "En lista de espera"), string.Format(I18n.T("eh.res.espera.cuerpo", "Te avisamos si se libera un lugar. Posición {0} en la fila."), pos)); } catch (Exception ex2) { MostrarError(ex2); } }
                return;
            }
            catch (Exception ex) { MostrarError(ex); return; }

            // Confirmación inmediata (dos operaciones BLL separadas; sin fusionar la transición).
            bool confirmada = false;
            try
            {
                var creada = _bll.ObtenerPorId(idNuevo);
                if (creada != null && creada.PuedeConfirmarse()) { _bll.Confirmar(MODULO, creada); confirmada = true; }
            }
            catch (Exception ex) { MostrarError(ex); }

            Estilo.Exito(this,
                confirmada ? I18n.T("eh.res.result.titulo", "Reserva confirmada")
                           : I18n.T("eh.res.result.pendtitulo", "Reserva creada"),
                string.Format(confirmada ? I18n.T("eh.res.ok.reservada",  "Reserva #{0} creada y confirmada.")
                                         : I18n.T("eh.res.ok.creadapend", "Reserva #{0} creada (Pendiente)."), idNuevo));

            CargarExistentes();

            // PDF solo si quedó Confirmada.
            if (confirmada)
            {
                try { var fresca = _bll.ObtenerPorId(idNuevo); if (fresca != null && fresca.Estado == BE.EstadoReserva.Confirmada) OfrecerPdf(fresca); }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError("[Reservas] post-alta: " + ex.Message); }
            }

            // Evitar doble reserva accidental: se bloquea «Reservar» y se ofrece iniciar otra.
            _btnReservar.Enabled = false;
            _btnReservar.Visible = false;
            _btnOtra.Visible = true;
        }

        private void ReiniciarAsistente()
        {
            _clienteSel = null; _expSel = null; _clienteOk = false;
            if (_numInvitados != null) _numInvitados.Value = 0;
            if (_txtBuscarCli != null) _txtBuscarCli.Text = "";
            if (_cboMostrar != null) _cboMostrar.SelectedIndex = 0;
            RecargarClientes();
            Ir(1);
        }

        // ══════════════════════ PESTAÑA EXISTENTES · LÓGICA ════════════════════════
        private static string EstadoLabel(BE.EstadoReserva e)
        {
            switch (e)
            {
                case BE.EstadoReserva.Pendiente:  return I18n.T("eh.res.estado.pendiente",  "Pendiente");
                case BE.EstadoReserva.Confirmada: return I18n.T("eh.res.estado.confirmada", "Confirmada");
                case BE.EstadoReserva.Cancelada:  return I18n.T("eh.res.estado.cancelada",  "Cancelada");
                case BE.EstadoReserva.Asistio:    return I18n.T("eh.res.estado.asistio",    "Asistió");
                case BE.EstadoReserva.NoAsistio:  return I18n.T("eh.res.estado.noasistio",  "No asistió");
                default: return e.ToString();
            }
        }

        private static Color ColorEstado(BE.EstadoReserva e)
        {
            switch (e)
            {
                case BE.EstadoReserva.Confirmada:
                case BE.EstadoReserva.Asistio:    return Estilo.Verde;
                case BE.EstadoReserva.Pendiente:  return Estilo.Naranja;
                case BE.EstadoReserva.Cancelada:  return Estilo.Rojo;
                default:                          return Estilo.Gris700;
            }
        }

        private void CargarExistentes()
        {
            try { _todas = _bll.ObtenerTodos(); PoblarFiltrosListado(); AplicarFiltro(); }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void PoblarFiltrosListado()
        {
            if (_cboFiltroCliente == null) return;
            _cargandoFiltros = true;
            string cliSel = _cboFiltroCliente.SelectedItem as string;
            string expSel = _cboFiltroExperiencia.SelectedItem as string;
            var clientes = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            var exps     = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var r in _todas)
            {
                if (!string.IsNullOrEmpty(r.NombreCliente))     clientes.Add(r.NombreCliente);
                if (!string.IsNullOrEmpty(r.NombreExperiencia)) exps.Add(r.NombreExperiencia);
            }
            _cboFiltroCliente.Items.Clear();
            _cboFiltroCliente.Items.Add(I18n.T("eh.res.filtro.todoscli", "Todos los clientes"));
            foreach (var c in clientes) _cboFiltroCliente.Items.Add(c);
            _cboFiltroCliente.SelectedIndex = (cliSel != null && _cboFiltroCliente.Items.IndexOf(cliSel) >= 0) ? _cboFiltroCliente.Items.IndexOf(cliSel) : 0;
            _cboFiltroExperiencia.Items.Clear();
            _cboFiltroExperiencia.Items.Add(I18n.T("eh.res.filtro.todasexp", "Todas las experiencias"));
            foreach (var e in exps) _cboFiltroExperiencia.Items.Add(e);
            _cboFiltroExperiencia.SelectedIndex = (expSel != null && _cboFiltroExperiencia.Items.IndexOf(expSel) >= 0) ? _cboFiltroExperiencia.Items.IndexOf(expSel) : 0;
            _cargandoFiltros = false;
        }

        private void AplicarFiltro()
        {
            try
            {
                if (_cargandoFiltros) return;
                var estado = UI.EstadoSeleccionado<BE.EstadoReserva>(_cboFiltroEstado);
                string cliF = (_cboFiltroCliente     != null && _cboFiltroCliente.SelectedIndex     > 0) ? _cboFiltroCliente.SelectedItem     as string : null;
                string expF = (_cboFiltroExperiencia != null && _cboFiltroExperiencia.SelectedIndex > 0) ? _cboFiltroExperiencia.SelectedItem as string : null;
                var lista = _todas.FindAll(r =>
                    (!estado.HasValue || r.Estado == estado.Value)
                    && (cliF == null || string.Equals(r.NombreCliente,     cliF, StringComparison.Ordinal))
                    && (expF == null || string.Equals(r.NombreExperiencia, expF, StringComparison.Ordinal)));

                _grid.DataSource = lista;
                UI.Columnas(_grid, ("NombreCliente", 23), ("NombreExperiencia", 27), ("FechaReserva", 15), ("CantidadInvitados", 11), ("Estado", 18));
                if (_grid.Columns.Contains("FechaReserva")) _grid.Columns["FechaReserva"].DefaultCellStyle.Format = "dd/MM/yyyy";
                if (_grid.Columns.Contains("CantidadInvitados"))
                {
                    _grid.Columns["CantidadInvitados"].HeaderText = I18n.T("eh.lbl.invitados", "Invitados");
                    _grid.Columns["CantidadInvitados"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                if (_lblContador != null) _lblContador.Text = string.Format(I18n.T("eh.lbl.mostrando", "Mostrando {0} de {1}"), lista.Count, _todas.Count);
                ActualizarBotonesReserva();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void ActualizarBotonesReserva()
        {
            var r = Sel();
            if (_btnConfirmar != null)       _btnConfirmar.Enabled       = r != null && r.PuedeConfirmarse();
            if (_btnCancelar  != null)       _btnCancelar.Enabled        = r != null && r.PuedeCancelarse();
            if (_btnConfirmacionPdf != null) _btnConfirmacionPdf.Enabled = r != null && r.Estado == BE.EstadoReserva.Confirmada;
        }

        private BE.Reserva Sel() => _grid.CurrentRow?.DataBoundItem as BE.Reserva;

        private void SeleccionarEnGrilla(int idReserva)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!(row.DataBoundItem is BE.Reserva r) || r.IdReserva != idReserva) continue;
                row.Selected = true;
                foreach (DataGridViewColumn col in _grid.Columns)
                    if (col.Visible) { _grid.CurrentCell = row.Cells[col.Index]; break; }
                return;
            }
        }

        private void ConfirmarConDialogo(BE.Reserva r)
        {
            if (r == null) { MostrarError(I18n.T("eh.res.err.selreserva", "Seleccioná una reserva del listado.")); return; }
            if (!r.PuedeConfirmarse()) { MostrarError(I18n.T("eh.res.err.confestado", "Solo se pueden confirmar reservas pendientes.")); return; }

            string cuerpo = string.Format(
                I18n.T("eh.res.confirmar.cuerpo",
                    "Cliente: {0}\nExperiencia: {1}\nFecha y horario: {2}\nLugares: {3}\nReserva N° {4}\n\nEstado actual: {5} → se confirmará la reserva.\n\n¿Confirmás la operación?"),
                r.NombreCliente ?? "—", r.NombreExperiencia ?? "—",
                r.FechaHoraExperiencia?.ToString("dd/MM/yyyy HH:mm") ?? "—",
                r.LugaresOcupados, r.IdReserva, EstadoLabel(r.Estado));
            if (!Estilo.Confirmar(this, I18n.T("eh.res.confirmar.titulo", "Confirmar reserva"), cuerpo)) return;

            try { _bll.Confirmar(MODULO, r); MostrarOk(I18n.T("eh.res.ok.confirmada", "Reserva confirmada.")); CargarExistentes(); }
            catch (Exception ex) { MostrarError(ex); return; }

            try { var fresca = _bll.ObtenerPorId(r.IdReserva); if (fresca != null) { SeleccionarEnGrilla(fresca.IdReserva); OfrecerPdf(fresca); } }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("[Reservas] post-confirmación: " + ex.Message); }
        }

        private void OfrecerPdf(BE.Reserva r)
        {
            if (r == null || r.Estado != BE.EstadoReserva.Confirmada) return;
            try
            {
                if (Estilo.Confirmar(this, I18n.T("eh.res.verpdf.titulo", "Confirmación de reserva"),
                        I18n.T("eh.res.verpdf.cuerpo", "¿Ver la confirmación de la reserva en PDF?")))
                {
                    var exp = _bllExp.ObtenerPorId(r.IdExperiencia);   // ubicación/ciudad reales para el PDF
                    Exportacion.DocumentosReserva.Mostrar(Exportacion.DocumentosReserva.ConfirmacionReserva(r, exp), this);
                }
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void GenerarPdfSeleccionada()
        {
            var r = Sel();
            if (r == null) { MostrarError(I18n.T("eh.res.err.selreserva", "Seleccioná una reserva del listado.")); return; }
            if (r.Estado != BE.EstadoReserva.Confirmada) { MostrarError(I18n.T("eh.res.pdf.soloconfirmada", "Solo las reservas confirmadas tienen confirmación en PDF.")); return; }
            try
            {
                var exp = _bllExp.ObtenerPorId(r.IdExperiencia);   // ubicación/ciudad reales para el PDF
                Exportacion.DocumentosReserva.Mostrar(Exportacion.DocumentosReserva.ConfirmacionReserva(r, exp), this);
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Cancelar()
        {
            var r = Sel(); if (r == null) { MostrarError(I18n.T("eh.res.err.selreserva", "Seleccioná una reserva del listado.")); return; }
            using (var dlg = new InputDialog(I18n.T("eh.res.cancelar.titulo", "Cancelar reserva"), I18n.T("eh.res.cancelar.motivo", "Motivo de cancelación:"), false))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { _bll.Cancelar(MODULO, r, dlg.InputText); MostrarOk(I18n.T("eh.res.ok.cancelada", "Reserva cancelada.")); CargarExistentes(); }
                catch (Exception ex) { MostrarError(ex); }
            }
        }
    }
}

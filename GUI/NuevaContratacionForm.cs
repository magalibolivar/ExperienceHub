using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// PN01 · Venta — "Nueva contratación" (CU03-VTA + CU01-VTA, armado). Asistente de 3 pasos en un
    /// mismo formulario: (1) Cliente, (2) Plan, (3) Confirmar. Solo al confirmar se crea la
    /// contratación (BLL.Contratacion.CrearContratacion), que queda Pendiente de pago y se deriva a
    /// Caja. Name = "NuevaContratacionForm" para el gateo por ControlMapeado.
    /// </summary>
    public class NuevaContratacionForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.Cliente         _bllCli  = new BLL.Cliente();
        private readonly BLL.Suscripcion     _bllSus  = new BLL.Suscripcion();
        private readonly BLL.PlanSuscripcion _bllPlan = new BLL.PlanSuscripcion();
        private readonly BLL.Contratacion    _bllCon  = new BLL.Contratacion();
        private const string MODULO = "Nueva contratación";

        private int _paso = 1;
        private Panel _pasoCliente, _pasoPlan, _pasoConfirmar;
        private PasoBar _pasoBar;

        // Situación del cliente para el filtro/columna del paso 1 (orden = ítems del combo).
        private enum SitCat { Todos = 0, Activa, PorVencer, Vencida, Suspendida, SinSus, Pendiente }

        // Paso 1
        private TextBox _txtBuscar;
        private ComboBox _cboSituacion;
        private readonly HashSet<int> _idPendientes = new HashSet<int>();  // clientes con contratación Pendiente de pago
        private DataGridView _gridCli;
        // Tarjeta del cliente seleccionado (columna derecha).
        private Label _cardNombre, _cardDni, _cardCiudad, _cardEmail, _cardEstadoSus, _cardVence;
        private Panel _cardAlerta;
        private Label _cardAlertaLbl;
        private string _susResumen = "—";       // resumen textual de la suscripción (para el paso 3)
        private List<BE.Cliente> _todosCli = new List<BE.Cliente>();
        private BE.Contratacion _conEnProceso;   // contratación activa del cliente elegido (si hay)
        // Modo del asistente según la suscripción del cliente: Activa/Vencida → Renovación; si no → Alta.
        private bool _esRenovacion;

        // Paso 2 — tarjeta de detalle del plan (columna derecha).
        private DataGridView _gridPlan;
        private Label _planNombre, _planPrecio, _planReservas, _planPremium, _planInvitados, _planAntic;
        private Button _btnCondiciones;
        private List<BE.PlanSuscripcion> _planes = new List<BE.PlanSuscripcion>();

        // Paso 3
        private Label _cfCliente, _cfSusActual, _cfPlan, _cfImporte;
        private Button _btnIrCobrar;

        // Navegación
        private Button _btnCancelar, _btnAtras, _btnSiguiente, _btnRegistrar;

        /// <summary>Se dispara tras registrar una contratación: el Menú refresca sus contadores.</summary>
        public event Action OperacionRealizada;
        /// <summary>Atajo admin: pide abrir "Cobros pendientes" con la contratación recién creada.</summary>
        public event Action<int> SolicitaIrACobrar;

        public NuevaContratacionForm()
        {
            Name = "NuevaContratacionForm";
            Text = "Nueva contratación"; Size = new Size(1020, 640); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(900, 560);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            _pasoCliente   = ConstruirPasoCliente();
            _pasoPlan      = ConstruirPasoPlan();
            _pasoConfirmar = ConstruirPasoConfirmar();
            host.Controls.Add(_pasoCliente);
            host.Controls.Add(_pasoPlan);
            host.Controls.Add(_pasoConfirmar);

            // Footer fijo: Cancelar + Atrás (izq) · Siguiente / Registrar (der).
            _btnCancelar  = UI.Secundario("Cancelar", "eh.btn.cancelar", (s, e) => Close());
            _btnAtras     = UI.Secundario("Atrás", "eh.btn.atras", (s, e) => Ir(_paso - 1));
            _btnSiguiente = UI.Primario("Siguiente", "eh.btn.siguiente", (s, e) => Ir(_paso + 1));
            _btnRegistrar = UI.Primario("Registrar y derivar a Caja", "eh.nva.btn.registrar", (s, e) => Registrar());
            var footer = UI.BarraAccionesLR(new[] { _btnCancelar, _btnAtras }, new[] { _btnSiguiente, _btnRegistrar });

            // Barra de pasos (reemplaza el texto suelto "Paso X de 3").
            _pasoBar = new PasoBar
            {
                Dock = DockStyle.Top,
                Pasos = new[]
                {
                    I18n.T("eh.nva.paso.cliente",   "Cliente"),
                    I18n.T("eh.nva.paso.plan",      "Plan"),
                    I18n.T("eh.nva.paso.confirmar", "Confirmar")
                }
            };

            var header = UI.Encabezado("Nueva contratación",
                "Asistente en 3 pasos: elegí el cliente, el plan y confirmá para derivar a Caja.",
                "eh.nva.titulo", "eh.nva.header.desc");

            Controls.Add(host);       // Fill
            Controls.Add(footer);     // Bottom
            Controls.Add(_pasoBar);   // Top (debajo del header)
            Controls.Add(header);     // Top (más arriba)
        }

        // ── Paso 1 · Cliente (dos columnas: listado | tarjeta del cliente) ───────
        private Panel ConstruirPasoCliente()
        {
            // Contenedor de dos columnas (izq más ancha para el listado, der para la tarjeta).
            var cols = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Estilo.Lienzo,
                Padding = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL)
            };
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            // ── Columna izquierda: buscador + grilla, dentro de una tarjeta ──
            var cardIzq = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                             Margin = new Padding(0, 0, UI.Gap, 0), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
            _gridCli = UI.Grilla();
            _gridCli.SelectionChanged += (s, e) => MostrarSituacionCliente();
            UI.ConectarEstadoVacio(_gridCli, "Buscá un cliente por nombre, apellido o DNI.", "eh.nva.cli.empty");

            _txtBuscar = new TextBox();
            _txtBuscar.TextChanged += (s, e) => FiltrarClientes();
            _cboSituacion = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboSituacion.Items.AddRange(new object[]
            {
                I18n.T("eh.nva.sit.todos",      "Todas las situaciones"),
                I18n.T("eh.nva.sit.activa",     "Suscripción activa"),
                I18n.T("eh.nva.sit.porvencer",  "Por vencer"),
                I18n.T("eh.nva.sit.vencida",    "Vencida"),
                I18n.T("eh.nva.sit.suspendida", "Suspendida"),
                I18n.T("eh.nva.sit.sinsus",     "Sin suscripción"),
                I18n.T("eh.nva.sit.pendiente",  "Con contratación pendiente"),
            });
            _cboSituacion.SelectedIndex = 0;
            _cboSituacion.SelectedIndexChanged += (s, e) => FiltrarClientes();

            // Barra de filtros dentro de la tarjeta (búsqueda + situación), fondo blanco para no cortar la card.
            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White, WrapContents = false, AutoSize = false };
            filtros.Controls.Add(UI.FiltroCampo("Buscar (nombre o DNI)", _txtBuscar, 240, "eh.lbl.buscardni"));
            filtros.Controls.Add(UI.FiltroCampo("Situación", _cboSituacion, 210, "eh.nva.cli.situacion"));

            cardIzq.Controls.Add(_gridCli);                                  // Fill
            cardIzq.Controls.Add(filtros);                                   // Top
            cardIzq.Controls.Add(UI.TituloSeccion("Clientes", "eh.nva.cli.listado")); // Top

            // ── Columna derecha: tarjeta del cliente seleccionado ──
            var cardDer = ConstruirTarjetaCliente();

            cols.Controls.Add(cardIzq, 0, 0);
            cols.Controls.Add(cardDer, 1, 0);

            var p = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            p.Controls.Add(cols);
            return p;
        }

        // Tarjeta con los datos del cliente seleccionado, su suscripción, alertas y "Nuevo cliente".
        private TarjetaPanel ConstruirTarjetaCliente()
        {
            var card = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                          Margin = new Padding(UI.Gap, 0, 0, 0), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };

            // Botón "Nuevo cliente" integrado al pie de la tarjeta.
            var barraNuevo = UI.BarraAcciones(UI.Secundario("Nuevo cliente", "eh.btn.nuevocliente", (s, e) => NuevoCliente()));
            barraNuevo.BackColor = Color.White;

            // Cuerpo: stack vertical de datos (Panel con Dock=Top; se apila en orden inverso de alta).
            var cuerpo = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            _cardNombre    = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 30, Font = Estilo.SemiBold(13f),
                                         ForeColor = Estilo.AzulOscuro, AutoEllipsis = true, Text = "—" };
            _cardDni       = FilaDato("DNI —");
            _cardCiudad    = FilaDato("Ciudad —");
            _cardEmail     = FilaDato("Email —");
            var sep        = new Panel { Dock = DockStyle.Top, Height = 1, Margin = new Padding(0, 6, 0, 6), BackColor = Estilo.Gris200 };
            var tSus       = UI.TituloSeccion("Suscripción", "eh.cli.sec.suscripcion");
            _cardEstadoSus = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 24, Font = Estilo.Bold(10f),
                                         ForeColor = Estilo.Gris700, Text = "—" };
            _cardVence     = FilaDato("Vence —");
            _cardAlertaLbl = new Label { Dock = DockStyle.Fill, AutoSize = false, Font = Estilo.Normal(9f),
                                         ForeColor = Estilo.Rojo, Padding = new Padding(8, 6, 8, 6), TextAlign = ContentAlignment.MiddleLeft };
            _cardAlerta    = new Panel { Dock = DockStyle.Top, Height = 54, Visible = false, Margin = new Padding(0, 6, 0, 0),
                                         BackColor = Estilo.Amarillo };
            _cardAlerta.Controls.Add(_cardAlertaLbl);

            // Orden de arriba hacia abajo (Dock=Top se agrega en orden inverso → agrego de último a primero).
            cuerpo.Controls.Add(_cardAlerta);
            cuerpo.Controls.Add(_cardVence);
            cuerpo.Controls.Add(_cardEstadoSus);
            cuerpo.Controls.Add(tSus);
            cuerpo.Controls.Add(sep);
            cuerpo.Controls.Add(_cardEmail);
            cuerpo.Controls.Add(_cardCiudad);
            cuerpo.Controls.Add(_cardDni);
            cuerpo.Controls.Add(_cardNombre);

            card.Controls.Add(cuerpo);       // Fill
            card.Controls.Add(barraNuevo);   // Bottom
            card.Controls.Add(UI.TituloSeccion("Cliente seleccionado", "eh.nva.cli.sec"));  // Top
            return card;
        }

        private static Label FilaDato(string texto)
            => new Label { Dock = DockStyle.Top, AutoSize = false, Height = 22, Font = Estilo.Normal(9.5f),
                           ForeColor = Estilo.Gris700, AutoEllipsis = true, Text = texto };

        // ── Paso 2 · Plan (dos columnas: listado de planes | tarjeta de detalle) ──
        private Panel ConstruirPasoPlan()
        {
            var cols = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Estilo.Lienzo,
                Padding = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL)
            };
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            // Izquierda: grilla de planes activos, en tarjeta.
            var cardIzq = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                             Margin = new Padding(0, 0, UI.Gap, 0), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
            _gridPlan = UI.Grilla();
            _gridPlan.SelectionChanged += (s, e) => MostrarDetallePlan();
            UI.ConectarEstadoVacio(_gridPlan, "No hay planes activos.", "eh.nva.plan.empty");
            cardIzq.Controls.Add(_gridPlan);
            cardIzq.Controls.Add(UI.TituloSeccion("Planes disponibles", "eh.nva.plan.listado"));

            // Derecha: tarjeta de detalle del plan (nombre + precio destacado + beneficios).
            var cardDer = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                             Margin = new Padding(UI.Gap, 0, 0, 0), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
            var cuerpo = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            _planNombre    = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 30, Font = Estilo.SemiBold(13f),
                                         ForeColor = Estilo.AzulOscuro, AutoEllipsis = true, Text = "—" };
            _planPrecio    = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 40, Font = Estilo.SemiBold(20f),
                                         ForeColor = Estilo.Coral, Text = "" };
            var sep        = new Panel { Dock = DockStyle.Top, Height = 1, Margin = new Padding(0, 6, 0, 6), BackColor = Estilo.Gris200 };
            _planReservas  = FilaDato("");
            _planPremium   = FilaDato("");
            _planInvitados = FilaDato("");
            _planAntic     = FilaDato("");

            cuerpo.Controls.Add(_planAntic);
            cuerpo.Controls.Add(_planInvitados);
            cuerpo.Controls.Add(_planPremium);
            cuerpo.Controls.Add(_planReservas);
            cuerpo.Controls.Add(sep);
            cuerpo.Controls.Add(_planPrecio);
            cuerpo.Controls.Add(_planNombre);

            // Documento 2 del diagrama: condiciones del plan (PDF), antes de aceptar la contratación.
            _btnCondiciones = UI.Secundario("Ver condiciones (PDF)", "eh.nva.plan.btncond", (s, e) => VerCondiciones());
            var barraCond = UI.BarraAcciones(_btnCondiciones);
            barraCond.BackColor = Color.White;

            cardDer.Controls.Add(cuerpo);
            cardDer.Controls.Add(barraCond);
            cardDer.Controls.Add(UI.TituloSeccion("Detalle del plan", "eh.nva.plan.sec"));

            cols.Controls.Add(cardIzq, 0, 0);
            cols.Controls.Add(cardDer, 1, 0);

            var p = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Visible = false };
            p.Controls.Add(cols);
            return p;
        }

        // ── Paso 3 · Confirmar (tarjeta de resumen centrada + importe destacado) ──
        private Panel ConstruirPasoConfirmar()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Visible = false,
                                Padding = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL) };

            // Tarjeta centrada de ancho acotado: el resumen no debe estirarse a toda la pantalla.
            var centro = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Estilo.Lienzo };
            centro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            centro.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
            centro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var card = new TarjetaPanel { Dock = DockStyle.Top, Radio = 10, BackColor = Color.White,
                                          Margin = new Padding(0, 0, 0, 0), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL), AutoSize = true };

            // Banda inferior con el importe grande (jerarquía: lo que se va a cobrar).
            var bandaImporte = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Estilo.Gris100, Margin = new Padding(0, UI.Gap, 0, 0) };
            var lblImporteTit = new Label { Dock = DockStyle.Left, Width = 140, Text = I18n.T("eh.nva.conf.importe", "Importe"),
                                            Tag = "eh.nva.conf.importe", TextAlign = ContentAlignment.MiddleLeft,
                                            ForeColor = Estilo.Gris700, Font = Estilo.Normal(10f), Padding = new Padding(10, 0, 0, 0) };
            _cfImporte = new Label { Dock = DockStyle.Fill, Text = "—", TextAlign = ContentAlignment.MiddleRight,
                                     ForeColor = Estilo.Coral, Font = Estilo.SemiBold(22f), Padding = new Padding(0, 0, 14, 0) };
            bandaImporte.Controls.Add(_cfImporte);
            bandaImporte.Controls.Add(lblImporteTit);

            var resumen = UI.Resumen(); resumen.Dock = DockStyle.Top;
            _cfCliente   = UI.FilaResumenValor(resumen, "Cliente");
            _cfSusActual = UI.FilaResumenValor(resumen, "Suscripción actual");
            _cfPlan      = UI.FilaResumenValor(resumen, "Plan a contratar");

            var aviso = new Label { Dock = DockStyle.Top, Height = 48, Tag = "eh.nva.conf.derivar",
                Text = "Al registrar, la contratación queda Pendiente de pago y se deriva a Caja para su cobro.",
                ForeColor = Estilo.Gris700, Font = Estilo.Normal(9.5f), Padding = new Padding(2, 10, 0, 0) };

            // Atajo admin: aparece recién tras registrar, si el usuario puede cobrar (patente Caja).
            _btnIrCobrar = UI.Secundario("Ir a cobrar ahora", "eh.nva.btn.ircobrar", (s, e) => IrACobrar());
            _btnIrCobrar.Visible = false;
            var barraAtajo = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, UI.Gap, 0, 0) };
            barraAtajo.Controls.Add(_btnIrCobrar);

            // Orden visual (Dock=Top, apilado inverso): título → resumen → importe → aviso → atajo.
            card.Controls.Add(barraAtajo);
            card.Controls.Add(aviso);
            card.Controls.Add(bandaImporte);
            card.Controls.Add(resumen);
            card.Controls.Add(UI.TituloSeccion("Confirmación", "eh.nva.conf.sec"));

            centro.Controls.Add(card, 1, 0);
            p.Controls.Add(centro);
            return p;
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.nva.titulo", this.Text); I18n.TraducirControles(this); ActualizarNav(); }

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
                _todosCli = _bllCli.ObtenerTodos();
                RecargarPendientes();
                FiltrarClientes();
                _planes = _bllPlan.ObtenerActivos();
                _gridPlan.DataSource = ArmarTablaPlanes(_planes);
                UI.Columnas(_gridPlan, ("Nombre", 34), ("ReservasMes", 22), ("Premium", 16), ("Precio", 28));
                if (_gridPlan.Columns.Contains("_Id")) _gridPlan.Columns["_Id"].Visible = false;
            }
            catch (Exception ex) { MostrarError(ex); }
            Ir(1);
            Traducir();
        }

        // ── Navegación entre pasos ────────────────────────────────────────────────
        private void Ir(int paso)
        {
            if (paso < 1 || paso > 3) return;
            if (paso == 2 && !PuedeAvanzarDeCliente()) return;
            if (paso == 3) PrepararConfirmacion();
            _paso = paso;
            _pasoCliente.Visible   = paso == 1;
            _pasoPlan.Visible      = paso == 2;
            _pasoConfirmar.Visible = paso == 3;
            ActualizarNav();
        }

        private void ActualizarNav()
        {
            if (_pasoBar != null) _pasoBar.Current = _paso - 1;   // la barra de pasos refleja el paso actual
            _btnAtras.Visible     = _paso > 1;
            _btnSiguiente.Visible = _paso < 3;
            _btnRegistrar.Visible = _paso == 3;
            _btnSiguiente.Enabled =
                (_paso == 1 && ClienteSeleccionado() != null && _conEnProceso == null) ||
                (_paso == 2 && PlanSeleccionado() != null);
        }

        private bool PuedeAvanzarDeCliente()
        {
            if (ClienteSeleccionado() == null)
            { MostrarError(I18n.T("eh.nva.err.sincliente", "Seleccioná un cliente para continuar.")); return false; }
            if (_conEnProceso != null)
            { MostrarError(I18n.T("eh.nva.err.enproceso", "El cliente ya tiene una contratación en proceso. Resolvela antes de crear otra.")); return false; }
            return true;
        }

        // ── Paso 1 · lógica ───────────────────────────────────────────────────────
        // Lee una sola vez qué clientes tienen una contratación Pendiente de pago (para el filtro/columna).
        private void RecargarPendientes()
        {
            _idPendientes.Clear();
            try { foreach (var c in _bllCon.ObtenerPendientes()) _idPendientes.Add(c.IdCliente); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("[NuevaContratacion] pendientes: " + ex.Message); }
        }

        private void FiltrarClientes()
        {
            int? sel = ClienteSeleccionado()?.IdCliente;
            string q = (_txtBuscar?.Text ?? "").Trim();
            var filtroSit = (SitCat)(_cboSituacion?.SelectedIndex ?? 0);

            var lista = _todosCli.FindAll(c =>
                (string.IsNullOrEmpty(q) ||
                 (c.NombreCompleto ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (c.DNI ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                && (filtroSit == SitCat.Todos || CategoriaDe(c) == filtroSit));

            var t = new System.Data.DataTable();
            t.Columns.Add("Nombre", typeof(string));
            t.Columns.Add("DNI", typeof(string));
            t.Columns.Add("Ciudad", typeof(string));
            t.Columns.Add("Situacion", typeof(string));
            t.Columns.Add("_Id", typeof(int));
            t.Columns.Add("_Cat", typeof(int));
            foreach (var c in lista)
            {
                var cat = CategoriaDe(c);
                t.Rows.Add(c.NombreCompleto, c.DNI, c.NombreCiudad ?? "—", EtiquetaSituacion(cat), c.IdCliente, (int)cat);
            }
            _gridCli.DataSource = t;
            UI.Columnas(_gridCli, ("Nombre", 32), ("DNI", 18), ("Ciudad", 20), ("Situacion", 30));
            if (_gridCli.Columns.Contains("_Id"))  _gridCli.Columns["_Id"].Visible = false;
            if (_gridCli.Columns.Contains("_Cat")) _gridCli.Columns["_Cat"].Visible = false;
            if (_gridCli.Columns.Contains("Situacion"))
                _gridCli.Columns["Situacion"].HeaderText = I18n.T("eh.nva.cli.situacion", "Situación");
            ColorearSituacion();

            if (sel.HasValue)
                foreach (DataGridViewRow r in _gridCli.Rows)
                    if (r.Cells["_Id"].Value is int id && id == sel.Value) { r.Selected = true; break; }
            MostrarSituacionCliente();
        }

        // Clasifica a un cliente en una situación. Prioridad: contratación pendiente (estado accionable)
        // por sobre el estado de la suscripción. Usa la suscripción ya hidratada por ObtenerTodos().
        private SitCat CategoriaDe(BE.Cliente c)
        {
            if (_idPendientes.Contains(c.IdCliente)) return SitCat.Pendiente;
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
                case SitCat.PorVencer:  return "⏰ " + I18n.T("eh.nva.sit.porvencer",  "Por vencer");
                case SitCat.Vencida:    return "⚠ " + I18n.T("eh.nva.sit.vencida",    "Vencida");
                case SitCat.Suspendida: return I18n.T("eh.nva.sit.suspendida", "Suspendida");
                case SitCat.SinSus:     return I18n.T("eh.nva.sit.sinsus",     "Sin suscripción");
                case SitCat.Pendiente:  return "⏳ " + I18n.T("eh.nva.sit.pendiente",  "Contratación pendiente");
                default:                return "—";
            }
        }

        private void ColorearSituacion()
        {
            if (!_gridCli.Columns.Contains("_Cat")) return;
            foreach (DataGridViewRow row in _gridCli.Rows)
            {
                if (!(row.Cells["_Cat"].Value is int ci)) continue;
                Color col;
                switch ((SitCat)ci)
                {
                    case SitCat.Activa:     col = Estilo.Verde;    break;
                    case SitCat.PorVencer:  col = Estilo.Naranja;  break;
                    case SitCat.Vencida:    col = Estilo.Rojo;     break;
                    case SitCat.Suspendida: col = Estilo.Rojo;     break;
                    case SitCat.Pendiente:  col = Estilo.Coral;    break;
                    default:                col = Estilo.Gris700;  break;  // SinSus
                }
                if (_gridCli.Columns.Contains("Situacion"))
                    row.Cells["Situacion"].Style.ForeColor = col;
            }
        }

        /// <summary>Preselecciona un cliente (para abrir el asistente ya en renovación desde Clientes).</summary>
        public void SeleccionarCliente(int idCliente)
        {
            Ir(1);
            _txtBuscar.Text = "";
            if (_cboSituacion != null) _cboSituacion.SelectedIndex = 0;
            FiltrarClientes();
            foreach (DataGridViewRow r in _gridCli.Rows)
                if (r.Cells["_Id"].Value is int id && id == idCliente)
                { r.Selected = true; _gridCli.CurrentCell = r.Cells[0]; break; }
            MostrarSituacionCliente();
        }

        private BE.Cliente ClienteSeleccionado()
        {
            if (_gridCli.CurrentRow?.DataBoundItem is System.Data.DataRowView drv && drv.Row.Table.Columns.Contains("_Id"))
            {
                int id = Convert.ToInt32(drv["_Id"]);
                return _todosCli.Find(c => c.IdCliente == id);
            }
            return null;
        }

        private void MostrarSituacionCliente()
        {
            _conEnProceso = null;
            _esRenovacion = false;
            var cli = ClienteSeleccionado();

            if (cli == null)
            {
                _cardNombre.Text    = I18n.T("eh.nva.cli.ninguno", "Ningún cliente seleccionado");
                _cardNombre.ForeColor = Estilo.Gris400;
                _cardDni.Text = "DNI —"; _cardCiudad.Text = "Ciudad —"; _cardEmail.Text = "Email —";
                _cardEstadoSus.Text = "—"; _cardEstadoSus.ForeColor = Estilo.Gris700;
                _cardVence.Text = "Vence —";
                _cardAlerta.Visible = false;
                _susResumen = "—";
                ActualizarNav();
                return;
            }

            // Datos básicos
            _cardNombre.Text    = cli.NombreCompleto;
            _cardNombre.ForeColor = Estilo.AzulOscuro;
            _cardDni.Text    = $"DNI {cli.DNI}";
            _cardCiudad.Text = $"Ciudad: {cli.NombreCiudad ?? "—"}";
            _cardEmail.Text  = $"Email: {cli.Email ?? "—"}";

            try
            {
                var s = _bllSus.ObtenerVigentePorCliente(cli.IdCliente);
                if (s == null)
                {
                    _cardEstadoSus.Text = I18n.T("eh.nva.sinsus", "Sin suscripción");
                    _cardEstadoSus.ForeColor = Estilo.Gris700;
                    _cardVence.Text = $"{I18n.T("eh.cli.sus.vence", "Vence")} —";
                    _susResumen = I18n.T("eh.nva.sinsus", "Sin suscripción");
                }
                else
                {
                    var est = s.EstadoVigenciaCalculado(DateTime.Today);
                    _cardEstadoSus.Text = $"● {s.NombrePlan} · {est}";
                    _cardEstadoSus.ForeColor = s.EstaVigente() ? Estilo.Verde : Estilo.Rojo;
                    _cardVence.Text = s.FechaVencimiento.HasValue
                        ? $"{I18n.T("eh.cli.sus.vence", "Vence")}: {s.FechaVencimiento.Value:dd/MM/yyyy}"
                        : $"{I18n.T("eh.cli.sus.vence", "Vence")}: {I18n.T("eh.cli.sus.sinlimite", "sin límite")}";
                    _susResumen = $"{s.NombrePlan} · {est}" +
                                  (s.FechaVencimiento.HasValue ? $" · vence {s.FechaVencimiento.Value:dd/MM/yyyy}" : "");

                    // Activa o Vencida → el asistente opera en modo RENOVACIÓN (extiende la suscripción).
                    // Suspendida → no se renueva (se reactiva manualmente desde Clientes).
                    if (est == BE.EstadoSuscripcion.Suspendida)
                        MostrarAlerta(Estilo.Amarillo, Estilo.AzulOscuro,
                            I18n.T("eh.nva.alerta.suspendida", "⚠ La suscripción está suspendida. Reactivala desde Clientes (no se renueva)."));
                    else
                    {
                        _esRenovacion = true;
                        if (!s.EstaVigente())
                            MostrarAlerta(Estilo.Amarillo, Estilo.AzulOscuro,
                                string.Format(I18n.T("eh.nva.alerta.vencida", "⚠ La suscripción está {0}. Al renovar, se extiende desde hoy."), est));
                        else
                            _cardAlerta.Visible = false;
                    }
                }

                // Alerta ROJA (bloqueante) si hay una contratación en proceso: pisa a la anterior.
                _conEnProceso = _bllCon.ObtenerActivas().FirstOrDefault(c => c.IdCliente == cli.IdCliente);
                if (_conEnProceso != null)
                    MostrarAlerta(Estilo.Rojo, Color.White,
                        string.Format(I18n.T("eh.nva.aviso.enproceso", "⚠ Ya tiene la contratación #{0} ({1}) en proceso. No se puede iniciar otra."),
                            _conEnProceso.IdContratacion, _conEnProceso.Estado));
            }
            catch (Exception ex) { _cardEstadoSus.Text = ex.Message; _cardAlerta.Visible = false; }
            ActualizarNav();
        }

        private void MostrarAlerta(Color fondo, Color texto, string mensaje)
        {
            _cardAlerta.BackColor = fondo;
            _cardAlertaLbl.ForeColor = texto;
            _cardAlertaLbl.Text = mensaje;
            _cardAlerta.Visible = true;
        }

        private void NuevoCliente()
        {
            BE.Cliente prefill = null;
            while (true)
                using (var form = prefill == null ? new ClienteForm() : new ClienteForm(prefill, false))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;
                    try { _bllCli.Alta(MODULO, form.ClienteEditado); }
                    catch (Exception ex) { MostrarError(ex); prefill = form.ClienteEditado; continue; }
                    string dni = form.ClienteEditado.DNI;
                    _todosCli = _bllCli.ObtenerTodos();
                    _txtBuscar.Text = dni;   // deja visible y filtrado al nuevo
                    FiltrarClientes();
                    var nuevo = _todosCli.Find(c => c.DNI == dni);
                    if (nuevo != null)
                        foreach (DataGridViewRow r in _gridCli.Rows)
                            if (r.Cells["_Id"].Value is int id && id == nuevo.IdCliente) { r.Selected = true; break; }
                    MostrarSituacionCliente();
                    return;
                }
        }

        // ── Paso 2 · lógica ───────────────────────────────────────────────────────
        private System.Data.DataTable ArmarTablaPlanes(List<BE.PlanSuscripcion> planes)
        {
            var t = new System.Data.DataTable();
            t.Columns.Add("Nombre", typeof(string));
            t.Columns.Add("ReservasMes", typeof(int));
            t.Columns.Add("Premium", typeof(string));
            t.Columns.Add("Precio", typeof(string));
            t.Columns.Add("_Id", typeof(int));
            foreach (var p in planes)
                t.Rows.Add(p.Nombre, p.ReservasMensuales, p.AccesoPremium ? "✓" : "—", $"${p.Precio:N2}", p.IdPlan);
            return t;
        }

        private BE.PlanSuscripcion PlanSeleccionado()
        {
            if (_gridPlan.CurrentRow?.DataBoundItem is System.Data.DataRowView drv && drv.Row.Table.Columns.Contains("_Id"))
            {
                int id = Convert.ToInt32(drv["_Id"]);
                return _planes.Find(p => p.IdPlan == id);
            }
            return null;
        }

        private void MostrarDetallePlan()
        {
            var p = PlanSeleccionado();
            if (p == null)
            {
                _planNombre.Text = I18n.T("eh.nva.plan.ninguno", "Elegí un plan");
                _planNombre.ForeColor = Estilo.Gris400;
                _planPrecio.Text = _planReservas.Text = _planPremium.Text = _planInvitados.Text = _planAntic.Text = "";
            }
            else
            {
                _planNombre.Text = p.Nombre; _planNombre.ForeColor = Estilo.AzulOscuro;
                _planPrecio.Text = $"${p.Precio:N2}";
                _planReservas.Text  = $"🎫 {string.Format(I18n.T("eh.nva.plan.reservasmes", "{0} reservas por mes"), p.ReservasMensuales)}";
                _planPremium.Text   = $"⭐ {I18n.T("eh.nva.plan.premium", "Acceso premium")}: {(p.AccesoPremium ? I18n.T("eh.si", "Sí") : "—")}";
                _planInvitados.Text = $"👥 {string.Format(I18n.T("eh.nva.plan.invitados", "Hasta {0} invitado(s) por reserva"), p.CantidadInvitados)}";
                _planAntic.Text     = $"📅 {string.Format(I18n.T("eh.nva.plan.anticip", "Anticipación máxima: {0} días"), p.AnticipacionMaximaDias)}";
            }
            if (_btnCondiciones != null) _btnCondiciones.Enabled = p != null;
            ActualizarNav();
        }

        // Documento "Condiciones del plan" (PDF) del plan seleccionado.
        private void VerCondiciones()
        {
            var p = PlanSeleccionado();
            if (p == null) { MostrarError(I18n.T("eh.nva.plan.ninguno", "Elegí un plan")); return; }
            Exportacion.DocumentosContratacion.Mostrar(Exportacion.DocumentosContratacion.Condiciones(p), this);
        }

        // ── Paso 3 · lógica ───────────────────────────────────────────────────────
        private void PrepararConfirmacion()
        {
            var cli = ClienteSeleccionado();
            var plan = PlanSeleccionado();
            _cfCliente.Text   = cli?.NombreCompleto ?? "—";
            _cfSusActual.Text = _susResumen;
            _cfPlan.Text      = plan?.Nombre ?? "—";
            _cfImporte.Text   = plan != null ? $"${plan.Precio:N2}" : "—";
            // El botón refleja si es alta o renovación.
            _btnRegistrar.Text = _esRenovacion
                ? I18n.T("eh.nva.btn.registrarrenov", "Registrar renovación y derivar a Caja")
                : I18n.T("eh.nva.btn.registrar", "Registrar y derivar a Caja");
            _btnIrCobrar.Visible = false;   // se habilita recién tras registrar
            _btnRegistrar.Enabled = true;
        }

        private void Registrar()
        {
            var cli = ClienteSeleccionado();
            var plan = PlanSeleccionado();
            if (cli == null || plan == null)
            { MostrarError(I18n.T("eh.nva.err.incompleto", "Falta elegir cliente y plan.")); return; }
            try
            {
                int id = _esRenovacion
                    ? _bllCon.CrearRenovacion(MODULO, cli.IdCliente, plan.IdPlan)
                    : _bllCon.CrearContratacion(MODULO, cli.IdCliente, plan.IdPlan);
                _ultimaCreada = id;
                Estilo.Exito(this,
                    _esRenovacion ? I18n.T("eh.nva.okrenov.titulo", "Renovación registrada")
                                  : I18n.T("eh.nva.ok.titulo", "Contratación registrada"),
                    string.Format(_esRenovacion
                        ? I18n.T("eh.nva.okrenov.cuerpo", "Renovación #{0} creada (Pendiente de pago). La cobra Caja.")
                        : I18n.T("eh.nva.ok.cuerpo", "Contratación #{0} creada (Pendiente de pago). La cobra Caja."), id));
                OperacionRealizada?.Invoke();
                _btnRegistrar.Enabled = false;
                // Atajo admin: visible solo si el usuario también puede cobrar.
                _btnIrCobrar.Visible = PermisosGui.Permite(BE.Patentes.ContratacionCaja);
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private int _ultimaCreada;
        private void IrACobrar() => SolicitaIrACobrar?.Invoke(_ultimaCreada);
    }
}

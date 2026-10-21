using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// Capa de Presentación — Módulo de Gestión de Clientes.
    ///
    /// Permite al Vendedor administrar los clientes suscriptores del servicio:
    ///   ✓ Ver listado completo con plan y stock en uso
    ///   ✓ Registrar nuevo cliente
    ///   ✓ Editar datos de cliente existente
    ///   ✓ Dar de baja (solo si no tiene reservas activas)
    ///   ✓ Filtrar por nombre/apellido/DNI
    ///
    /// Accesible desde Menú → Ventas → Clientes (permiso mnuClientes).
    /// </summary>
    /// <summary>
    /// Hereda de <see cref="FormBase"/>:
    ///   - MostrarOk() y MostrarError() → heredados, no se redeclaran
    ///   - MensajeLabel → sobreescrito para devolver el lblMensaje de este formulario
    /// </summary>
    public partial class Clientes : FormBase, IIdiomaObserver
    {
        protected override Label MensajeLabel => lblMensaje;

        private readonly BLL.Interfaces.IClienteService clienteBLL = new BLL.Cliente();
        private readonly BLL.Suscripcion               _bllSus    = new BLL.Suscripcion();
        private readonly BLL.PlanSuscripcion           _bllPlan   = new BLL.PlanSuscripcion();

        // ── Sección "Suscripción" del cliente seleccionado (gestión del ciclo de vida) ──
        private Panel _panelSus;
        private Label _susEstado, _susPlan, _susVence;
        private Button _susBtnRenovar, _susBtnSuspender, _susBtnReactivar;
        private BE.Suscripcion _susActual;

        /// <summary>Pide abrir Nueva contratación (modo renovación) con este cliente cargado.</summary>
        public event Action<int> SolicitaRenovar;

        // Cache de la lista actual
        private List<BE.Cliente> _clientes = new List<BE.Cliente>();

        // ── Filtros avanzados (plan / situación / ciudad) + exportación del listado ──
        private ComboBox _cboPlan, _cboSituacion, _cboCiudad;
        private Button   _btnExportar;
        private Panel    _header;       // encabezado (ref. para fijar el z-order)
        private bool     _cargando;     // evita filtrar mientras se repueblan los combos
        private enum SitCli { Todas = 0, Activa, PorVencer, Vencida, Suspendida, SinSus }

        // Idioma activo — sincronizado en Traducir() para usar en AplicarFiltro y CargarClientes
        private Idioma _idioma = GestorIdioma.IdiomaActual;

        public Clientes()
        {
            InitializeComponent();
            this.Load += new EventHandler(Clientes_Load);

            // Encabezado de pantalla (Nivel 1) + estado vacío, sin tocar el Designer.
            _header = UI.Encabezado("Clientes",
                "Clientes suscriptores del servicio. Registrá, editá o filtrá por nombre, DNI o email.",
                "eh.frm.clientes", "eh.cli.header.desc");
            this.Controls.Add(_header);
            UI.ConectarEstadoVacio(dgvClientes, "No se encontraron clientes con el filtro aplicado.", "eh.empty.clientes");

            // Los botones del Designer tenían ancho fijo chico y truncaban su texto: los ampliamos.
            btnNuevo.AutoSize = false;  btnNuevo.Width  = 120;
            btnEditar.AutoSize = false; btnEditar.Width = 120;
            btnBaja.AutoSize = false;   btnBaja.Width   = 150;

            ConstruirBarraFiltros();
            ConstruirSeccionSuscripcion();
            OrdenarZ();
        }

        // WinForms resuelve el docking de mayor a menor índice (mayor índice = borde más exterior).
        // Al mezclar controles del Designer con los agregados por código el orden no era determinista
        // y la sección "Suscripción" podía quedar tapada. Lo fijamos explícitamente:
        //   header (arriba) · panelTop (filtros) · grilla · Suscripción · panelBottom (estado).
        private void OrdenarZ()
        {
            this.Controls.SetChildIndex(dgvClientes, 0);
            this.Controls.SetChildIndex(panelTop,    1);
            this.Controls.SetChildIndex(_panelSus,   2);
            this.Controls.SetChildIndex(_header,     3);
            this.Controls.SetChildIndex(panelBottom, 4);
        }

        // Segunda fila de filtros dentro de panelTop: Plan / Situación / Ciudad + "Descargar PDF".
        // Plan y Ciudad se pueblan en CargarClientes con los valores reales presentes en la lista.
        private void ConstruirBarraFiltros()
        {
            panelTop.Height = 104;

            _cboPlan = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboPlan.SelectedIndexChanged += (s, e) => AplicarFiltro();

            _cboSituacion = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboSituacion.Items.AddRange(new object[]
            {
                I18n.T("eh.nva.sit.todos",      "Todas las situaciones"),
                I18n.T("eh.nva.sit.activa",     "Suscripción activa"),
                I18n.T("eh.nva.sit.porvencer",  "Por vencer"),
                I18n.T("eh.nva.sit.vencida",    "Vencida"),
                I18n.T("eh.nva.sit.suspendida", "Suspendida"),
                I18n.T("eh.nva.sit.sinsus",     "Sin suscripción"),
            });
            _cboSituacion.SelectedIndex = 0;
            _cboSituacion.SelectedIndexChanged += (s, e) => AplicarFiltro();

            _cboCiudad = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboCiudad.SelectedIndexChanged += (s, e) => AplicarFiltro();

            _btnExportar = UI.Secundario("Descargar PDF", "eh.cli.btn.exportar", (s, e) => ExportarListado());
            _btnExportar.Width = 150; _btnExportar.Height = 28; _btnExportar.Margin = new Padding(0, 20, 0, 0);

            var fila = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 52, WrapContents = false, AutoSize = false,
                BackColor = panelTop.BackColor, Padding = new Padding(UI.Gap, 2, UI.Gap, 2)
            };
            fila.Controls.Add(UI.FiltroCampo(I18n.T("eh.cli.filtro.plan", "Plan"),        _cboPlan,      180, "eh.cli.filtro.plan"));
            fila.Controls.Add(UI.FiltroCampo(I18n.T("eh.nva.cli.situacion", "Situación"), _cboSituacion, 190, "eh.nva.cli.situacion"));
            fila.Controls.Add(UI.FiltroCampo(I18n.T("eh.cli.filtro.ciudad", "Ciudad"),    _cboCiudad,    170, "eh.cli.filtro.ciudad"));
            fila.Controls.Add(_btnExportar);

            panelTop.Controls.Add(fila);
        }

        // Puebla Plan y Ciudad con los valores reales de la lista (en memoria, sobre el DNI ya descifrado).
        private void PoblarCombosFiltro()
        {
            if (_cboPlan == null) return;
            var t = Traductor.ObtenerTraducciones(_idioma);
            string Tx(string k, string fb) => t.ContainsKey(k) ? t[k].Texto : fb;

            string planSel   = _cboPlan.SelectedItem   as string;
            string ciudadSel = _cboCiudad.SelectedItem as string;

            var planes   = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            var ciudades = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var c in _clientes)
            {
                if (!string.IsNullOrEmpty(c.Suscripcion?.NombrePlan)) planes.Add(c.Suscripcion.NombrePlan);
                if (!string.IsNullOrEmpty(c.NombreCiudad))            ciudades.Add(c.NombreCiudad);
            }

            _cboPlan.Items.Clear();
            _cboPlan.Items.Add(Tx("eh.cli.filtro.todosplanes", "Todos los planes"));
            foreach (var p in planes) _cboPlan.Items.Add(p);
            _cboPlan.SelectedIndex = (planSel != null && _cboPlan.Items.IndexOf(planSel) >= 0)
                ? _cboPlan.Items.IndexOf(planSel) : 0;

            _cboCiudad.Items.Clear();
            _cboCiudad.Items.Add(Tx("eh.cli.filtro.todasciudades", "Todas las ciudades"));
            foreach (var c in ciudades) _cboCiudad.Items.Add(c);
            _cboCiudad.SelectedIndex = (ciudadSel != null && _cboCiudad.Items.IndexOf(ciudadSel) >= 0)
                ? _cboCiudad.Items.IndexOf(ciudadSel) : 0;
        }

        // Clasifica la situación de la suscripción del cliente (misma lógica que Nueva contratación,
        // sin la categoría "pendiente" que acá no aplica). Usa la suscripción ya hidratada.
        private SitCli CategoriaCli(BE.Cliente c)
        {
            var s = c.Suscripcion;
            if (s == null) return SitCli.SinSus;
            if (s.Estado == BE.EstadoSuscripcion.Suspendida) return SitCli.Suspendida;
            if (s.EstadoVigenciaCalculado(DateTime.Today) == BE.EstadoSuscripcion.Vencida) return SitCli.Vencida;
            if (s.EstaVigente() && s.ProximaAVencer()) return SitCli.PorVencer;
            return SitCli.Activa;
        }

        // Exporta a PDF el listado tal como está filtrado (Factory → ExportadorPdf, modo tabular).
        private void ExportarListado()
        {
            var dt = dgvClientes.DataSource as DataTable;
            var t = Traductor.ObtenerTraducciones(_idioma);
            string Tx(string k, string fb) => t.ContainsKey(k) ? t[k].Texto : fb;
            if (dt == null || dt.Rows.Count == 0)
            { MostrarError(Tx("eh.cli.export.vacio", "No hay clientes para exportar.")); return; }

            var cols = new (string Col, string Hdr)[]
            {
                ("Nombre",      Tx("col.cli.nombre",       "Nombre")),
                ("Apellido",    Tx("col.cli.apellido",     "Apellido")),
                ("DNI",         Tx("col.cli.dni",          "DNI")),
                ("Email",       Tx("col.cli.email",        "Email")),
                ("Plan",        Tx("col.cli.plan",         "Plan")),
                ("Ciudad",      Tx("eh.cli.filtro.ciudad", "Ciudad")),
                ("Alta",        Tx("col.cli.alta",         "Alta")),
                ("Vencimiento", Tx("col.cli.vencimiento",  "Vencimiento")),
            };

            var export = new DataTable();
            foreach (var c in cols) export.Columns.Add(c.Hdr, typeof(string));
            foreach (DataRow r in dt.Rows)
            {
                var vals = new object[cols.Length];
                for (int i = 0; i < cols.Length; i++)
                    vals[i] = dt.Columns.Contains(cols[i].Col) ? (r[cols[i].Col]?.ToString() ?? "") : "";
                export.Rows.Add(vals);
            }

            var headers = new string[cols.Length];
            for (int i = 0; i < cols.Length; i++) headers[i] = cols[i].Hdr;

            string titulo = Tx("eh.cli.export.titulo", "Listado de clientes");
            var reporte = new Exportacion.ReporteExportable
            {
                Titulo        = titulo,
                NombreArchivo = titulo,
                Encabezados   = headers,
                Datos         = export
            };
            new Exportacion.ExportadorPdf(Tx("eh.frm.clientes", "Clientes")).Exportar(reporte, this);
        }

        // Panel "Suscripción" anclado abajo: estado + plan + vencimiento del cliente seleccionado y
        // las acciones del ciclo de vida. Suspender / Reactivar son manuales (según estado); "Renovar"
        // abre Nueva contratación con el cliente cargado (renovación COBRADA; sin fecha libre acá).
        private void ConstruirSeccionSuscripcion()
        {
            _panelSus = new Panel { Dock = DockStyle.Bottom, Height = 120, BackColor = Color.White,
                                    Padding = new Padding(UI.GapXL, UI.Gap, UI.GapXL, UI.Gap) };

            var fila = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 28, WrapContents = false, AutoSize = false, BackColor = Color.White };
            _susEstado = new Label { AutoSize = true, Font = Estilo.Bold(10f), ForeColor = Estilo.AzulOscuro, Margin = new Padding(0, 4, 24, 0) };
            _susPlan   = new Label { AutoSize = true, Font = Estilo.Normal(10f), ForeColor = Estilo.AzulOscuro, Margin = new Padding(0, 4, 24, 0) };
            _susVence  = new Label { AutoSize = true, Font = Estilo.Normal(10f), ForeColor = Estilo.AzulOscuro, Margin = new Padding(0, 4, 0, 0) };
            fila.Controls.Add(_susEstado); fila.Controls.Add(_susPlan); fila.Controls.Add(_susVence);

            var barra = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, WrapContents = false, AutoSize = false, BackColor = Color.White };
            _susBtnRenovar   = UI.Primario("Renovar",   "eh.btn.renovar",   (s, e) => AbrirRenovacion());
            _susBtnSuspender = UI.Peligro("Suspender",  "eh.btn.suspender", (s, e) => AccionSuscripcion(true));
            _susBtnReactivar = UI.Exito("Reactivar",    "eh.btn.reactivar", (s, e) => AccionSuscripcion(false));
            foreach (var b in new[] { _susBtnRenovar, _susBtnSuspender, _susBtnReactivar })
            { b.Height = 32; b.Margin = new Padding(0, 0, 8, 0); barra.Controls.Add(b); }

            _panelSus.Controls.Add(barra);
            _panelSus.Controls.Add(fila);
            _panelSus.Controls.Add(UI.TituloSeccion("Suscripción", "eh.cli.sec.suscripcion"));
            this.Controls.Add(_panelSus);
        }

        // ── Observer de idioma ────────────────────────────────────────────────

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            Traducir(GestorIdioma.IdiomaActual);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        public void UpdateLanguage(Idioma idioma)
        {
            Traducir(idioma);
            // Refrescar grilla para que los headers y "Sin plan" reflejen el nuevo idioma
            if (_clientes.Count > 0 || dgvClientes.Rows.Count > 0)
                AplicarFiltro();
        }

        private void Traducir(Idioma idioma)
        {
            _idioma = idioma;
            var t = Traductor.ObtenerTraducciones(idioma);
            if (this.Tag != null && t.ContainsKey(this.Tag.ToString()))
                this.Text = t[this.Tag.ToString()].Texto;
            Aplicar(lblFiltro,  t);
            Aplicar(btnNuevo,   t);
            Aplicar(btnEditar,  t);
            Aplicar(btnBaja,    t);
            // Fila de filtros agregada por código (etiquetas Plan/Situación/Ciudad + botón Descargar).
            I18n.TraducirControles(panelTop);
            TraducirHeadersGrilla();
        }

        /// <summary>Traduce los HeaderText de la grilla de clientes según el idioma activo.</summary>
        private void TraducirHeadersGrilla()
        {
            var t = Traductor.ObtenerTraducciones(_idioma);
            void RH(string col, string clave, string fallback)
            {
                if (dgvClientes.Columns.Contains(col) && t.ContainsKey(clave))
                    dgvClientes.Columns[col].HeaderText = t[clave].Texto;
                else if (dgvClientes.Columns.Contains(col))
                    dgvClientes.Columns[col].HeaderText = fallback;
            }
            RH("Nombre",     "col.cli.nombre",     "Nombre");
            RH("Apellido",   "col.cli.apellido",   "Apellido");
            RH("DNI",        "col.cli.dni",        "DNI");
            RH("Email",      "col.cli.email",      "Email");
            RH("Plan",       "col.cli.plan",       "Plan");
            RH("Reservas",   "col.cli.reservas",   "Reservas");
            RH("MetodoPago", "col.cli.metodopago", "Método de Pago");
            RH("Alta",        "col.cli.alta",        "Alta");
            RH("Vencimiento", "col.cli.vencimiento", "Vencimiento");
        }

        private static void Aplicar(Control c, IDictionary<string, Traduccion> t)
        {
            if (c?.Tag != null && t.ContainsKey(c.Tag.ToString()))
                c.Text = t[c.Tag.ToString()].Texto;
        }

        // ── Eventos de carga ──────────────────────────────────────────────────

        private void Clientes_Load(object sender, EventArgs e)
        {
            CargarClientes();
        }

        // ── Eventos del Designer ──────────────────────────────────────────────

        private void TxtFiltro_TextChanged(object sender, EventArgs e)
        {
            AplicarFiltro();
        }

        private void BtnRefrescar_Click(object sender, EventArgs e)
        {
            CargarClientes();
        }

        private void DgvClientes_SelectionChanged(object sender, EventArgs e)
        {
            bool hay = dgvClientes.SelectedRows.Count > 0;
            btnEditar.Enabled = hay;
            btnBaja.Enabled   = hay;
            MostrarSuscripcion();
        }

        // ── Sección Suscripción ───────────────────────────────────────────────
        private void MostrarSuscripcion()
        {
            if (_panelSus == null) return;
            var cli = ObtenerClienteSeleccionado();
            _susActual = null;
            var t = Traductor.ObtenerTraducciones(_idioma);
            string Tx(string k, string fb) => t.ContainsKey(k) ? t[k].Texto : fb;

            if (cli == null)
            {
                _susEstado.Text = Tx("eh.cli.sus.sincliente", "Seleccioná un cliente para ver su suscripción.");
                _susEstado.ForeColor = Estilo.Gris700;
                _susPlan.Text = _susVence.Text = "";
                _susBtnRenovar.Enabled = _susBtnSuspender.Enabled = _susBtnReactivar.Enabled = false;
                return;
            }
            try { _susActual = _bllSus.ObtenerVigentePorCliente(cli.IdCliente); } catch { _susActual = null; }

            if (_susActual == null)
            {
                _susEstado.Text = Tx("eh.cli.sus.sin", "Sin suscripción"); _susEstado.ForeColor = Estilo.Gris700;
                _susPlan.Text = _susVence.Text = "";
                _susBtnRenovar.Enabled = true;   // renovar = contratar/asignar un plan nuevo
                _susBtnSuspender.Enabled = _susBtnReactivar.Enabled = false;
            }
            else
            {
                var est = _susActual.EstadoVigenciaCalculado(DateTime.Today);
                _susEstado.Text = $"{Tx("eh.cli.sus.estado", "Estado")}: {est}";
                _susEstado.ForeColor = _susActual.EstaVigente() ? Estilo.Verde : Estilo.Rojo;
                _susPlan.Text  = $"{Tx("eh.cli.sus.plan", "Plan")}: {_susActual.NombrePlan}";
                _susVence.Text = $"{Tx("eh.cli.sus.vence", "Vence")}: " +
                                 (_susActual.FechaVencimiento?.ToString("dd/MM/yyyy") ?? Tx("eh.cli.sus.sinlimite", "sin límite"));
                _susBtnRenovar.Enabled   = true;
                _susBtnSuspender.Enabled = _susActual.PuedeSuspender();
                _susBtnReactivar.Enabled = _susActual.PuedeReactivar();
            }
        }

        // "Renovar" abre el asistente de Nueva contratación (modo renovación) con el cliente cargado:
        // la renovación es COBRADA (pasa por Caja), no un cambio de fecha directo. Lo pide el evento
        // SolicitaRenovar, que el Menú traduce en abrir/activar NuevaContratacionForm con ese cliente.
        private void AbrirRenovacion()
        {
            var cli = ObtenerClienteSeleccionado();
            if (cli == null)
            {
                var t0 = Traductor.ObtenerTraducciones(_idioma);
                MostrarError(t0.ContainsKey("eh.cli.sus.sincliente") ? t0["eh.cli.sus.sincliente"].Texto : "Seleccioná un cliente.");
                return;
            }
            SolicitaRenovar?.Invoke(cli.IdCliente);
        }

        private void AccionSuscripcion(bool suspender)
        {
            var cli = ObtenerClienteSeleccionado();
            if (cli == null) return;
            var t = Traductor.ObtenerTraducciones(_idioma);
            string Tx(string k, string fb) => t.ContainsKey(k) ? t[k].Texto : fb;
            try
            {
                if (suspender)
                {
                    if (!Estilo.Confirmar(this, Tx("eh.cli.sus.susp.titulo", "Suspender suscripción"),
                            Tx("eh.cli.sus.susp.cuerpo", "¿Suspender la suscripción? El cliente no podrá reservar hasta reactivarla.")))
                        return;
                    _bllSus.Suspender(this.Text, cli.IdCliente);
                    MostrarOk(Tx("eh.cli.sus.suspendida", "Suscripción suspendida."));
                }
                else
                {
                    _bllSus.Reactivar(this.Text, cli.IdCliente);
                    MostrarOk(Tx("eh.cli.sus.reactivada", "Suscripción reactivada."));
                }
                CargarClientes();
                MostrarSuscripcion();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void DgvClientes_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            BtnEditar_Click(sender, e);
        }

        // ── Carga y filtrado ──────────────────────────────────────────────────

        private void CargarClientes()
        {
            try
            {
                _cargando = true;
                _clientes = clienteBLL.ObtenerTodos();
                PoblarCombosFiltro();
                _cargando = false;
                AplicarFiltro();

                var t = Traductor.ObtenerTraducciones(_idioma);
                string fmt = t.ContainsKey("msg.cli.cargados")
                    ? t["msg.cli.cargados"].Texto
                    : "{0} cliente(s) registrado(s).";
                MostrarOk(string.Format(fmt, _clientes.Count));
            }
            catch (Exception ex)
            {
                _cargando = false;
                var te = Traductor.ObtenerTraducciones(_idioma);
                MostrarError(string.Format(te.ContainsKey("err.generico.cargar") ? te["err.generico.cargar"].Texto : "Error al cargar: {0}", ex.Message));
            }
        }

        private void AplicarFiltro()
        {
            if (_cargando) return;
            string filtro  = txtFiltro.Text.Trim().ToLower();
            string planF   = (_cboPlan   != null && _cboPlan.SelectedIndex   > 0) ? _cboPlan.SelectedItem   as string : null;
            string ciudadF = (_cboCiudad != null && _cboCiudad.SelectedIndex > 0) ? _cboCiudad.SelectedItem as string : null;
            var sitF       = (SitCli)(_cboSituacion?.SelectedIndex ?? 0);

            var lista = _clientes.FindAll(c =>
                (string.IsNullOrEmpty(filtro) ||
                    c.NombreCompleto.ToLower().Contains(filtro) ||
                    c.DNI.Contains(filtro) ||
                    (c.Email ?? "").ToLower().Contains(filtro))
                && (planF   == null || string.Equals(c.Suscripcion?.NombrePlan, planF,   StringComparison.Ordinal))
                && (ciudadF == null || string.Equals(c.NombreCiudad,            ciudadF, StringComparison.Ordinal))
                && (sitF == SitCli.Todas || CategoriaCli(c) == sitF));

            var t = Traductor.ObtenerTraducciones(_idioma);
            string sinPlan = t.ContainsKey("lbl.sinplan") ? t["lbl.sinplan"].Texto : "Sin plan";

            string sinVenc = t.ContainsKey("msg.cli.sinvenc") ? t["msg.cli.sinvenc"].Texto : "—";
            string vencido = t.ContainsKey("msg.cli.vencido") ? t["msg.cli.vencido"].Texto : "Vencido";

            string proxVencer = t.ContainsKey("msg.cli.proxvencer") ? t["msg.cli.proxvencer"].Texto : "Vence pronto";

            var tabla = new DataTable();
            tabla.Columns.Add("ID",          typeof(int));
            tabla.Columns.Add("Nombre",      typeof(string));
            tabla.Columns.Add("Apellido",    typeof(string));
            tabla.Columns.Add("DNI",         typeof(string));
            tabla.Columns.Add("Email",       typeof(string));
            tabla.Columns.Add("Plan",        typeof(string));
            tabla.Columns.Add("Reservas",    typeof(string));
            tabla.Columns.Add("Ciudad",      typeof(string));
            tabla.Columns.Add("Alta",        typeof(string));
            tabla.Columns.Add("Vencimiento", typeof(string));
            tabla.Columns.Add("_Vencido",    typeof(bool));
            tabla.Columns.Add("_ProxVencer", typeof(bool));

            foreach (var c in lista)
            {
                var sus = c.Suscripcion;
                System.DateTime? venc = sus?.FechaVencimiento;
                bool expirado    = venc.HasValue && venc.Value.Date < System.DateTime.Today;
                bool proxAVencer = !expirado && sus != null && sus.ProximaAVencer();

                string vencStr = venc.HasValue
                    ? (expirado
                        ? $"⚠ {venc.Value:dd/MM/yyyy} ({vencido})"
                        : proxAVencer
                            ? $"⏰ {venc.Value:dd/MM/yyyy} ({proxVencer})"
                            : venc.Value.ToString("dd/MM/yyyy"))
                    : sinVenc;

                // Reservas consumidas / mensuales del plan
                string reservas = sus != null
                    ? $"{sus.ReservasConsumidasMes} / {sus.ReservasDelPlan}"
                    : "—";

                tabla.Rows.Add(
                    c.IdCliente,
                    c.Nombre,
                    c.Apellido,
                    c.DNI,
                    c.Email ?? "—",
                    sus?.NombrePlan ?? sinPlan,
                    reservas,
                    c.NombreCiudad ?? "—",
                    c.FechaAlta.ToString("dd/MM/yyyy"),
                    vencStr,
                    expirado,
                    proxAVencer);
            }

            dgvClientes.DataSource = tabla;

            // Ocultar columnas auxiliares y colorear filas
            if (dgvClientes.Columns.Contains("_Vencido"))
                dgvClientes.Columns["_Vencido"].Visible = false;
            if (dgvClientes.Columns.Contains("_ProxVencer"))
                dgvClientes.Columns["_ProxVencer"].Visible = false;

            foreach (DataGridViewRow row in dgvClientes.Rows)
            {
                if (row.Cells["_Vencido"].Value is bool exp && exp)
                    row.DefaultCellStyle.ForeColor = Color.DarkRed;
                else if (row.Cells["_ProxVencer"].Value is bool prox && prox)
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(160, 100, 0);
            }

            // Ocultar el ID técnico (no aporta al usuario final)
            if (dgvClientes.Columns.Contains("ID"))
                dgvClientes.Columns["ID"].Visible = false;

            TraducirHeadersGrilla();

            string fmtConteo = t.ContainsKey("msg.cli.conteo")
                ? t["msg.cli.conteo"].Texto
                : "Mostrando {0} de {1}";
            lblConteo.Text = string.Format(fmtConteo, lista.Count, _clientes.Count);
        }

        // ── Eventos de botones ────────────────────────────────────────────────

        private void BtnNuevo_Click(object sender, EventArgs e)
        {
            using (var form = new ClienteForm())
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    clienteBLL.Alta(this.Text, form.ClienteEditado);
                    var t = Traductor.ObtenerTraducciones(_idioma);
                    string fmt = t.ContainsKey("msg.cli.registrado") ? t["msg.cli.registrado"].Texto : "Cliente '{0}' registrado correctamente.";
                    MostrarOk(string.Format(fmt, form.ClienteEditado.NombreCompleto));
                    CargarClientes();
                }
                catch (Exception ex)
                {
                    MostrarError(ex);
                }
            }
        }

        private void BtnEditar_Click(object sender, EventArgs e)
        {
            var cliente = ObtenerClienteSeleccionado();
            if (cliente == null) return;

            using (var form = new ClienteForm(cliente))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    clienteBLL.Modificar(this.Text, form.ClienteEditado);
                    var t = Traductor.ObtenerTraducciones(_idioma);
                    string fmt = t.ContainsKey("msg.cli.actualizado") ? t["msg.cli.actualizado"].Texto : "Cliente '{0}' actualizado.";
                    MostrarOk(string.Format(fmt, form.ClienteEditado.NombreCompleto));
                    CargarClientes();
                }
                catch (Exception ex)
                {
                    MostrarError(ex);
                }
            }
        }

        private void BtnBaja_Click(object sender, EventArgs e)
        {
            var cliente = ObtenerClienteSeleccionado();
            if (cliente == null) return;

            var t = Traductor.ObtenerTraducciones(_idioma);
            string T(string k, string fb) => t.ContainsKey(k) ? t[k].Texto : fb;

            var confirmacion = MessageBox.Show(
                string.Format(T("conf.baja.cli.msg", "¿Dar de baja a {0} (DNI {1})?\n\nEsta acción no se puede deshacer."),
                    cliente.NombreCompleto, cliente.DNI),
                T("conf.baja.cli.titulo", "Confirmar Baja"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmacion != DialogResult.Yes) return;

            try
            {
                clienteBLL.Baja(this.Text, cliente);
                string fmt = T("msg.cli.eliminado", "Cliente '{0}' eliminado.");
                MostrarOk(string.Format(fmt, cliente.NombreCompleto));
                CargarClientes();
            }
            catch (Exception ex)
            {
                MostrarError(ex);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private BE.Cliente ObtenerClienteSeleccionado()
        {
            if (dgvClientes.SelectedRows.Count == 0) return null;
            int id = Convert.ToInt32(dgvClientes.SelectedRows[0].Cells["ID"].Value);
            return _clientes.Find(c => c.IdCliente == id);
        }

    }
}

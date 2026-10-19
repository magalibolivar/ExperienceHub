using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// Membresía del cliente (PN01 unificado). Una sola pantalla centrada en el cliente que reúne la
    /// COMERCIALIZACIÓN (contratar → cobrar → formalizar) y la GESTIÓN de la suscripción (estado,
    /// renovar, suspender, reactivar). La separación de roles NO la hace la navegación sino los
    /// permisos: las secciones Venta y Caja se mapean a sus patentes vía [ControlMapeado], de modo
    /// que el Agente ve solo lo suyo, Caja solo el cobro y el Administrador (bypass) ve TODO — vista
    /// total para el seguimiento. Así se respeta el PN sin tener dos pantallas para un mismo concepto.
    /// </summary>
    public class MembresiaForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.Contratacion    _bllCon  = new BLL.Contratacion();
        private readonly BLL.Suscripcion     _bllSus  = new BLL.Suscripcion();
        private readonly BLL.Cliente         _bllCli  = new BLL.Cliente();
        private readonly BLL.PlanSuscripcion _bllPlan = new BLL.PlanSuscripcion();
        private const string MODULO = "Membresia";

        private ComboBox _cboCliente, _cboPlan, _cboMedio, _cboCuotas;
        private TextBox _txtBuscarCli;
        private CheckBox _chkVenc;
        private DateTimePicker _dtpVenc;
        private Label _rvPlan, _rvEstado, _rvVence, _rvReservas, _rvContratacion, _lblPlanDetalle;
        private Panel _panelVenta, _panelCaja;
        private Button _btnRegistrar, _btnFormalizar, _btnRenovar, _btnSuspender, _btnReactivar, _btnCobrarOk, _btnCobrarFail, _btnNuevoCli;
        private BE.Contratacion _conActual;   // contratación en proceso del cliente seleccionado (si hay)
        // Lista completa de clientes ya descifrada (DNI en claro en memoria): el filtro por nombre/DNI
        // se hace EN MEMORIA, sin consultar la BD por DNI en claro.
        private System.Collections.Generic.List<BE.Cliente> _todosClientes = new System.Collections.Generic.List<BE.Cliente>();

        public MembresiaForm()
        {
            Name = "MembresiaForm";   // debe coincidir con [ControlMapeado].Formulario para gatear por patente
            Text = "Membresía del cliente"; Size = new Size(960, 740); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(840, 620);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            // Cliente: barra superior, siempre visible (todos eligen sobre quién operar).
            _cboCliente = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboCliente.SelectedIndexChanged += (s, e) => MostrarEstado();
            _txtBuscarCli = new TextBox();
            _txtBuscarCli.TextChanged += (s, e) => FiltrarClientes();
            _btnNuevoCli = UI.Secundario("Nuevo cliente", "eh.btn.nuevocliente", (s, e) => NuevoCliente());
            _btnNuevoCli.Height = 26; _btnNuevoCli.Margin = new Padding(0, 20, 0, 0);
            var clienteBar = UI.BarraFiltros(
                UI.FiltroCampo("Buscar (nombre o DNI)", _txtBuscarCli, 220, "eh.lbl.buscardni"),
                UI.FiltroCampo("Cliente", _cboCliente, 300, "eh.lbl.cliente"),
                _btnNuevoCli);

            // Zona de tarjetas (resúmenes + Venta + Caja), con scroll si no entra.
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Estilo.Lienzo,
                                   Padding = new Padding(UI.GapXL, 0, UI.GapXL, UI.GapL) };

            var bloqueResumen = ConstruirResumen();   // dos tarjetas lado a lado
            _panelVenta = ConstruirVenta();           // tarjeta Venta  (gateable: panelVenta)
            _panelCaja  = ConstruirCaja();            // tarjeta Caja   (gateable: panelCaja)

            // Dock=Top apila el ÚLTIMO agregado más arriba: agrego en orden inverso para que el
            // resultado visual sea resúmenes → Venta → Caja de arriba hacia abajo.
            host.Controls.Add(_panelCaja);
            host.Controls.Add(_panelVenta);
            host.Controls.Add(bloqueResumen);

            var barra  = UI.BarraAcciones(UI.Secundario("Actualizar", "eh.btn.refrescar", (s, e) => Recargar()));
            var header = UI.Encabezado("Membresía del cliente",
                "Comercialización y gestión de la suscripción en una pantalla. Venta y Caja ven solo su parte; el administrador, todo.",
                "eh.frm.membresia", "eh.mem.header.desc");

            Controls.Add(host);
            Controls.Add(barra);
            Controls.Add(clienteBar);
            Controls.Add(header);
        }

        // Tarjeta blanca redondeada, con el mismo estilo que el resto de la app.
        private static TarjetaPanel Tarjeta() => new TarjetaPanel
        {
            Radio = 10, BackColor = Color.White, Margin = new Padding(0),
            Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL)
        };

        // Envoltorio de alto fijo con gap superior. Su Name se mapea en [ControlMapeado]: si el
        // usuario no tiene la patente, ManejadorSeguridad lo oculta entero (tarjeta incluida).
        private static Panel Envoltorio(string name, Control inner, int alto)
        {
            inner.Dock = DockStyle.Fill;
            var w = new Panel { Name = name, Dock = DockStyle.Top, Height = alto, BackColor = Estilo.Lienzo,
                                Padding = new Padding(0, UI.GapL, 0, 0) };
            w.Controls.Add(inner);
            return w;
        }

        // Barra de botones horizontal (igual que las barras de acciones del resto de la app).
        private static FlowLayoutPanel BarraBotones(params Button[] botones)
        {
            var flp = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, BackColor = Color.White,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, UI.Gap, 0, 0) };
            foreach (var b in botones) { b.Margin = new Padding(0, 0, UI.Gap, 0); flp.Controls.Add(b); }
            return flp;
        }

        // Fila superior: dos tarjetas lado a lado (suscripción actual | contratación en proceso).
        private Panel ConstruirResumen()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Estilo.Lienzo };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var cardSus = Tarjeta(); cardSus.Margin = new Padding(0, 0, UI.Gap, 0);
            var resumen = UI.Resumen(); resumen.Dock = DockStyle.Fill;
            _rvPlan     = UI.FilaResumenValor(resumen, "Plan");
            _rvEstado   = UI.FilaResumenValor(resumen, "Estado");
            _rvVence    = UI.FilaResumenValor(resumen, "Vence");
            _rvReservas = UI.FilaResumenValor(resumen, "Reservas");
            cardSus.Controls.Add(resumen);
            cardSus.Controls.Add(UI.TituloSeccion("Suscripción actual", "eh.sec.suscactual"));

            var cardCon = Tarjeta(); cardCon.Margin = new Padding(UI.Gap, 0, 0, 0);
            var rc = UI.Resumen(); rc.Dock = DockStyle.Fill;
            _rvContratacion = UI.FilaResumenValor(rc, "Estado");
            cardCon.Controls.Add(rc);
            cardCon.Controls.Add(UI.TituloSeccion("Contratación en proceso", "eh.mem.sec.contratacion"));

            grid.Controls.Add(cardSus, 0, 0);
            grid.Controls.Add(cardCon, 1, 0);

            var wrap = new Panel { Dock = DockStyle.Top, Height = 168, BackColor = Estilo.Lienzo, Padding = new Padding(0, UI.GapL, 0, 0) };
            wrap.Controls.Add(grid);
            return wrap;
        }

        // Tarjeta VENTA: campos (plan, vencimiento) + barra horizontal de acciones.
        private Panel ConstruirVenta()
        {
            var card = Tarjeta();

            _cboPlan = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _chkVenc = new CheckBox { Text = "Con vencimiento", Tag = "eh.lbl.venceel", AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
            _dtpVenc = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 130, Enabled = false, Value = DateTime.Today.AddMonths(1) };
            _chkVenc.CheckedChanged += (s, e) => _dtpVenc.Enabled = _chkVenc.Checked;
            var venc = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            venc.Controls.Add(_chkVenc); venc.Controls.Add(_dtpVenc);

            // Detalle del plan seleccionado (precio, reservas/mes, premium, invitados, anticipación).
            _cboPlan.SelectedIndexChanged += (s, e) => MostrarDetallePlan();
            _lblPlanDetalle = new Label { AutoSize = false, Width = 470, Height = 20, Margin = new Padding(0, 6, 0, 0),
                                          ForeColor = Estilo.Gris700, Font = Estilo.Normal(9f), Text = "" };

            var campos = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            campos.Controls.Add(UI.FiltroCampo("Plan", _cboPlan, 220, "eh.lbl.plan"));
            campos.Controls.Add(UI.FiltroCampo("Vencimiento", venc, 230, "eh.lbl.vencimiento"));
            campos.Controls.Add(_lblPlanDetalle);

            _btnRegistrar  = UI.Primario("Registrar contratación", "eh.btn.nuevacontratacion", (s, e) => Registrar());
            _btnFormalizar = UI.Primario("Formalizar suscripción", "eh.btn.formalizar",        (s, e) => Formalizar());
            _btnRenovar    = UI.Primario("Renovar",                 "eh.btn.renovar",           (s, e) => Renovar());
            _btnSuspender  = UI.Peligro ("Suspender",               "eh.btn.suspender",         (s, e) => Accion(true));
            _btnReactivar  = UI.Exito   ("Reactivar",               "eh.btn.reactivar",         (s, e) => Accion(false));

            card.Controls.Add(campos);
            card.Controls.Add(BarraBotones(_btnRegistrar, _btnFormalizar, _btnRenovar, _btnSuspender, _btnReactivar));
            card.Controls.Add(UI.TituloSeccion("Venta", "eh.mem.sec.venta"));
            return Envoltorio("panelVenta", card, 236);
        }

        // Tarjeta CAJA: campos (medio, cuotas) + barra horizontal de cobro.
        private Panel ConstruirCaja()
        {
            var card = Tarjeta();

            _cboMedio = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboMedio.DataSource = Enum.GetValues(typeof(BE.MedioPago));
            _cboMedio.SelectedIndexChanged += (s, e) => ActualizarCuotas();
            _cboCuotas = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

            var campos = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            campos.Controls.Add(UI.FiltroCampo("Medio de pago", _cboMedio, 180, "eh.lbl.mediopago"));
            campos.Controls.Add(UI.FiltroCampo("Cuotas", _cboCuotas, 120, "eh.lbl.cuotas"));

            _btnCobrarOk   = UI.Exito  ("Registrar pago aprobado",  "eh.btn.cobrar",  (s, e) => Cobrar(true));
            _btnCobrarFail = UI.Peligro("Registrar pago rechazado", "eh.btn.intento", (s, e) => Cobrar(false));

            card.Controls.Add(campos);
            card.Controls.Add(BarraBotones(_btnCobrarOk, _btnCobrarFail));
            card.Controls.Add(UI.TituloSeccion("Caja", "eh.mem.sec.caja"));
            return Envoltorio("panelCaja", card, 158);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir()
        {
            this.Text = I18n.T("eh.frm.membresia", this.Text);
            I18n.TraducirControles(this);
            ActualizarAcciones();
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
            // Mantenimiento perezoso: vence las suscripciones pasadas de plazo y reinicia el cupo
            // mensual al abrir, para que el estado mostrado esté al día.
            try { _bllSus.ActualizarEstadosPorFecha(); } catch (Exception ex) { System.Diagnostics.Trace.TraceError("[Membresia] " + ex.Message); }
            Recargar();
            Traducir();
        }

        // Refresca clientes y planes (preservando la selección) y re-muestra el estado del cliente.
        private void Recargar()
        {
            try
            {
                int? cli  = _cboCliente.SelectedValue as int?;
                int? plan = _cboPlan.SelectedValue as int?;
                _todosClientes = _bllCli.ObtenerTodos();            // lista ya descifrada (DNI en claro)
                FiltrarClientes();
                _cboPlan.DataSource    = _bllPlan.ObtenerActivos(); _cboPlan.DisplayMember    = "Nombre";          _cboPlan.ValueMember    = "IdPlan";
                if (cli.HasValue)  _cboCliente.SelectedValue = cli.Value;
                if (plan.HasValue) _cboPlan.SelectedValue    = plan.Value;
                MostrarDetallePlan();
                MostrarEstado();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Filtra la lista de clientes EN MEMORIA por nombre o DNI (sin SQL por DNI en claro).
        private void FiltrarClientes()
        {
            int? sel = _cboCliente.SelectedValue as int?;
            string q = (_txtBuscarCli?.Text ?? "").Trim();
            var lista = string.IsNullOrEmpty(q)
                ? _todosClientes
                : _todosClientes.FindAll(c =>
                    (c.NombreCompleto ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (c.DNI ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            _cboCliente.DataSource = lista;
            _cboCliente.DisplayMember = "NombreCompleto";
            _cboCliente.ValueMember = "IdCliente";
            if (sel.HasValue && lista.Exists(c => c.IdCliente == sel.Value))
                _cboCliente.SelectedValue = sel.Value;
        }

        // Punto 4 — detalle del plan seleccionado en la sección Venta.
        private void MostrarDetallePlan()
        {
            if (_lblPlanDetalle == null) return;
            if (_cboPlan.SelectedItem is BE.PlanSuscripcion p)
                _lblPlanDetalle.Text = string.Format(
                    I18n.T("eh.mem.plandetalle", "${0:N2} · {1} reservas/mes · premium {2} · hasta {3} invitado(s) · anticip. {4} días"),
                    p.Precio, p.ReservasMensuales, p.AccesoPremium ? "✓" : "—", p.CantidadInvitados, p.AnticipacionMaximaDias);
            else
                _lblPlanDetalle.Text = "";
        }

        // Punto 5 — alta de cliente desde esta pantalla: abre ClienteForm, persiste y lo deja seleccionado.
        private void NuevoCliente()
        {
            BE.Cliente prefill = null;   // datos del intento anterior, para reabrir sin perderlos
            while (true)
            {
                using (var form = prefill == null ? new ClienteForm() : new ClienteForm(prefill, false))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;   // canceló

                    try
                    {
                        _bllCli.Alta(MODULO, form.ClienteEditado);          // valida (edad/DNI duplicado) y persiste
                    }
                    catch (Exception ex)
                    {
                        MostrarError(ex);                                    // muestra el motivo del rechazo
                        prefill = form.ClienteEditado;                       // conserva lo cargado y reabre el diálogo
                        continue;
                    }

                    string dni = form.ClienteEditado.DNI;
                    _todosClientes = _bllCli.ObtenerTodos();                 // recarga con el nuevo cliente
                    _txtBuscarCli.Text = "";                                 // limpia filtro para que sea visible
                    FiltrarClientes();
                    var nuevo = _todosClientes.Find(c => c.DNI == dni);      // Alta no devuelve Id → se ubica por DNI
                    if (nuevo != null) _cboCliente.SelectedValue = nuevo.IdCliente;
                    MostrarEstado();
                    return;
                }
            }
        }

        private void MostrarEstado()
        {
            if (_rvPlan == null) return;
            try
            {
                int? idCli = _cboCliente.SelectedValue as int?;

                // Suscripción actual
                BE.Suscripcion s = idCli.HasValue ? _bllSus.ObtenerVigentePorCliente(idCli.Value) : null;
                if (s == null)
                {
                    _rvPlan.Text = "Sin suscripción"; _rvPlan.ForeColor = Estilo.Rojo;
                    _rvEstado.Text = "—"; _rvVence.Text = "—"; _rvReservas.Text = "—";
                }
                else
                {
                    _rvPlan.Text = s.NombrePlan; _rvPlan.ForeColor = Estilo.AzulOscuro;
                    bool vigente = s.EstaVigente();
                    bool porVencer = s.ProximaAVencer(7);
                    _rvEstado.Text = s.EstadoVigenciaCalculado(DateTime.Today).ToString();
                    _rvEstado.ForeColor = !vigente ? Estilo.Rojo : (porVencer ? Estilo.Coral : Estilo.Verde);
                    string vence = s.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "sin límite";
                    if (vigente && porVencer) vence += $"  ·  ⚠ vence en {s.DiasHastaVencimiento()} día(s)";
                    _rvVence.Text = vence;
                    _rvVence.ForeColor = (vigente && porVencer) ? Estilo.Coral : Estilo.AzulOscuro;
                    _rvReservas.Text = $"{s.ReservasConsumidasMes} / {s.ReservasDelPlan} usadas  ·  restan {s.ReservasRestantes()}";
                }

                // Contratación en proceso del cliente (Pendiente o Pagada).
                _conActual = idCli.HasValue
                    ? _bllCon.ObtenerActivas().FirstOrDefault(c => c.IdCliente == idCli.Value)
                    : null;
                if (_conActual == null)
                {
                    _rvContratacion.Text = "— ninguna —"; _rvContratacion.ForeColor = Estilo.Gris700;
                }
                else
                {
                    string extra = _conActual.IntentosPago > 0
                        ? $"  ·  {_conActual.IntentosPago}/{BLL.Contratacion.MAX_INTENTOS_PAGO} rechazos" : "";
                    _rvContratacion.Text = $"#{_conActual.IdContratacion}  ·  {_conActual.Estado}  ·  ${_conActual.Importe:0.00}{extra}";
                    _rvContratacion.ForeColor = _conActual.Estado == BE.EstadoContratacion.Pagada ? Estilo.Verde : Estilo.Coral;
                }

                ActualizarAcciones();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Habilita cada acción según el cliente y el estado de su contratación en proceso.
        private void ActualizarAcciones()
        {
            if (_btnRegistrar == null) return;
            bool hayCli       = _cboCliente.SelectedValue is int;
            bool cobrable     = _conActual != null && _conActual.PuedeCobrarse();
            bool formalizable = _conActual != null && _conActual.PuedeFormalizarse();

            _btnRegistrar.Enabled  = hayCli && _conActual == null;   // una contratación en proceso por vez
            _btnFormalizar.Enabled = formalizable;
            _btnRenovar.Enabled    = hayCli;
            _btnSuspender.Enabled  = hayCli;
            _btnReactivar.Enabled  = hayCli;
            _btnCobrarOk.Enabled   = cobrable;
            _btnCobrarFail.Enabled = cobrable;
            _cboMedio.Enabled      = cobrable;
            ActualizarCuotas();
        }

        private void ActualizarCuotas()
        {
            if (_cboCuotas == null) return;
            var medio = _cboMedio.SelectedItem is BE.MedioPago m ? m : BE.MedioPago.Efectivo;
            _cboCuotas.DataSource   = BE.Contratacion.CuotasPermitidas(medio);
            _cboCuotas.SelectedItem = 1;
            bool cobrable = _conActual != null && _conActual.PuedeCobrarse();
            _cboCuotas.Enabled = cobrable && medio == BE.MedioPago.Tarjeta;
        }

        // ── ① Venta ─────────────────────────────────────────────────────────────
        private void Registrar()
        {
            try
            {
                if (!(_cboCliente.SelectedValue is int idCli) || !(_cboPlan.SelectedValue is int idPlan))
                { MostrarError("Seleccioná un cliente y un plan."); return; }
                int id = _bllCon.CrearContratacion(MODULO, idCli, idPlan);
                Estilo.Exito(this, "Contratación registrada",
                    $"Contratación #{id} queda pendiente de pago. La cobra Caja.");
                Recargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Formalizar()
        {
            if (_conActual == null) { MostrarError("No hay una contratación pagada para formalizar."); return; }
            try
            {
                int idSus = _bllCon.Formalizar(MODULO, _conActual.IdContratacion);
                Estilo.Exito(this, "Suscripción formalizada", $"Suscripción #{idSus} vigente. Constancia generada.");
                Recargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Renovar()
        {
            try
            {
                if (!(_cboCliente.SelectedValue is int idCli) || !(_cboPlan.SelectedValue is int idPlan))
                { MostrarError("Seleccioná un cliente y un plan para renovar."); return; }
                DateTime? venc = _chkVenc.Checked ? _dtpVenc.Value.Date : (DateTime?)null;
                _bllSus.Renovar(MODULO, idCli, idPlan, venc);
                Estilo.Exito(this, "Suscripción renovada", "El cliente ya puede volver a reservar experiencias.");
                Recargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Accion(bool suspender)
        {
            try
            {
                if (!(_cboCliente.SelectedValue is int idCli)) { MostrarError("Seleccioná un cliente."); return; }
                if (suspender && !Estilo.Confirmar(this, "Suspender suscripción",
                        "¿Suspender la suscripción de este cliente? No podrá reservar hasta reactivarla.")) return;
                if (suspender) _bllSus.Suspender(MODULO, idCli); else _bllSus.Reactivar(MODULO, idCli);
                Estilo.Exito(this, suspender ? "Suscripción suspendida" : "Suscripción reactivada",
                    suspender ? "El cliente no podrá reservar hasta reactivarla." : "El cliente ya puede volver a reservar.");
                Recargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // ── ② Caja ──────────────────────────────────────────────────────────────
        private void Cobrar(bool pagoOk)
        {
            if (_conActual == null) { MostrarError("No hay una contratación pendiente de cobro."); return; }
            if (!(_cboMedio.SelectedItem is BE.MedioPago medio)) { MostrarError("Indicá el medio de pago."); return; }
            int cuotas = _cboCuotas.SelectedItem is int q ? q : 1;
            try
            {
                var r = _bllCon.RegistrarCobro(MODULO, _conActual.IdContratacion, medio, cuotas, pagoOk);
                if (r.Estado == BE.EstadoContratacion.Pagada)
                {
                    string detalleCuotas = r.Cuotas > 1
                        ? $" {r.Cuotas} cuotas de ${BE.Contratacion.ImportePorCuota(r.Importe, r.Cuotas):0.00}" +
                          (r.RecargoPorcentaje > 0 ? $" (+{r.RecargoPorcentaje:0.##}% financiación)" : "") +
                          $" — total ${r.ImporteTotal:0.00}."
                        : $" Total ${r.ImporteTotal:0.00}.";
                    Estilo.Exito(this, "Pago concretado",
                        $"Comprobante {r.NumeroComprobante}.{detalleCuotas} Ahora Venta formaliza la suscripción.");
                }
                else if (r.Estado == BE.EstadoContratacion.Cancelada)
                    Estilo.Error(this, "Contratación cancelada",
                        $"Se alcanzaron {BLL.Contratacion.MAX_INTENTOS_PAGO} pagos rechazados. La contratación quedó cancelada.");
                else
                    Estilo.Error(this, "Pago rechazado",
                        $"Intento {r.IntentosPago}/{BLL.Contratacion.MAX_INTENTOS_PAGO}. Podés reintentar el cobro.");
                Recargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

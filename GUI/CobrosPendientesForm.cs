using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// PN01 · Caja — "Cobros pendientes" (CU01-CAJ + CU02-CAJ). Dos columnas: a la izquierda la cola
    /// de contrataciones Pendiente de pago; a la derecha, el panel de cobro de la seleccionada (medio,
    /// cuotas, importe con recargo) con "Pago aprobado" / "Pago rechazado". Reusa íntegramente las
    /// reglas de BLL.Contratacion.RegistrarCobro y los cálculos puros de BE.Contratacion (no inventa
    /// fórmulas ni toca estados). Name = "CobrosPendientesForm" para el gateo por ControlMapeado.
    /// </summary>
    public class CobrosPendientesForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.Contratacion _bll = new BLL.Contratacion();
        private const string MODULO = "Cobros pendientes";

        private DataGridView _grid;
        private ComboBox _cboMedio, _cboCuotas;
        private Label _rvCliente, _rvPlan, _rvImporte, _rvIntentos, _rvTotal, _rvComprobante;
        private Button _btnOk, _btnFail, _btnIrFormalizar;
        private List<BE.Contratacion> _pendientes = new List<BE.Contratacion>();

        /// <summary>Se dispara tras un cobro (aprobado/rechazado): el Menú refresca sus contadores.</summary>
        public event Action OperacionRealizada;
        /// <summary>Atajo admin: pide abrir "Para formalizar" con esta contratación ya pagada.</summary>
        public event Action<int> SolicitaIrAFormalizar;

        public CobrosPendientesForm()
        {
            Name = "CobrosPendientesForm";
            Text = "Cobros pendientes"; Size = new Size(1040, 620); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(900, 540);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            _grid.SelectionChanged += (s, e) => MostrarDetalle();
            UI.ConectarEstadoVacio(_grid,
                "No hay cobros pendientes.\nLas nuevas contrataciones aparecen acá para cobrar.", "eh.cob.empty");

            // Panel derecho: resumen + cobro.
            var panel = UI.PanelEdicion(380);

            var resumen = UI.Resumen(); resumen.Dock = DockStyle.Top;
            _rvCliente     = UI.FilaResumenValor(resumen, "Cliente");
            _rvPlan        = UI.FilaResumenValor(resumen, "Plan");
            _rvImporte     = UI.FilaResumenValor(resumen, "Importe base");
            _rvIntentos    = UI.FilaResumenValor(resumen, "Intentos");
            _rvComprobante = UI.FilaResumenValor(resumen, "Comprobante");

            _cboMedio = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboMedio.DataSource = Enum.GetValues(typeof(BE.MedioPago));
            _cboMedio.SelectedIndexChanged += (s, e) => { ActualizarCuotas(); CalcularTotal(); };
            _cboCuotas = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboCuotas.SelectedIndexChanged += (s, e) => CalcularTotal();

            var campos = UI.GrillaCampos(120);
            UI.Seccion(campos, "Cobro", "eh.cob.sec.cobro");
            UI.Campo(campos, "Medio de pago", _cboMedio, true, "eh.lbl.mediopago");
            UI.Campo(campos, "Cuotas", _cboCuotas, false, "eh.lbl.cuotas");

            // Total a cobrar DESTACADO: banda con título a la izquierda e importe grande a la derecha.
            var bandaTotal = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Estilo.Gris100, Margin = new Padding(0, UI.Gap, 0, 0) };
            var lblTotalTit = new Label { Dock = DockStyle.Left, Width = 120, Text = I18n.T("eh.cob.total", "Total a cobrar"),
                                          Tag = "eh.cob.total", TextAlign = ContentAlignment.MiddleLeft,
                                          ForeColor = Estilo.Gris700, Font = Estilo.Normal(9.5f), Padding = new Padding(8, 0, 0, 0) };
            _rvTotal = new Label { Dock = DockStyle.Fill, Text = "—", TextAlign = ContentAlignment.MiddleRight,
                                   ForeColor = Estilo.Coral, Font = Estilo.SemiBold(16f), Padding = new Padding(0, 0, 10, 0), AutoEllipsis = true };
            bandaTotal.Controls.Add(_rvTotal);
            bandaTotal.Controls.Add(lblTotalTit);

            var contenido = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            contenido.Controls.Add(bandaTotal);
            contenido.Controls.Add(campos);
            contenido.Controls.Add(resumen);
            contenido.Controls.Add(UI.TituloSeccion("Contratación", "eh.cob.sec.contratacion"));

            _btnOk   = UI.Exito  ("Pago aprobado",  "eh.cob.btn.aprobado",  (s, e) => Cobrar(true));
            _btnFail = UI.Peligro("Pago rechazado", "eh.cob.btn.rechazado", (s, e) => Cobrar(false));
            var barraCobro = UI.BarraAcciones(_btnOk, _btnFail);
            barraCobro.BackColor = Color.White;

            panel.Controls.Add(contenido);
            panel.Controls.Add(barraCobro);

            // Grilla dentro de una tarjeta (misma identidad que el paso 1 de Nueva contratación).
            var cardGrid = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                              Margin = new Padding(UI.GapXL, UI.GapL, UI.Gap, UI.GapL), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
            cardGrid.Controls.Add(_grid);
            cardGrid.Controls.Add(UI.TituloSeccion("Cola de cobros", "eh.cob.sec.cola"));

            // Barra inferior full-width: refrescar (izq) + atajo admin "Ir a formalizar" (der).
            _btnIrFormalizar = UI.Secundario("Ir a formalizar", "eh.cob.btn.irformalizar", (s, e) => IrAFormalizar());
            var barra = UI.BarraAccionesLR(
                new[] { UI.Secundario("Actualizar", "eh.btn.refrescar", (s, e) => Cargar()) },
                new[] { _btnIrFormalizar });

            var header = UI.Encabezado("Cobros pendientes",
                "Cobrá las contrataciones derivadas por Venta. Al concretar el pago se emite el comprobante.",
                "eh.cob.titulo", "eh.cob.header.desc");

            Controls.Add(cardGrid);
            Controls.Add(panel);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.cob.titulo", this.Text); I18n.TraducirControles(this); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            // El atajo a formalizar solo tiene sentido si el usuario puede formalizar (patente Venta).
            if (_btnIrFormalizar != null)
                _btnIrFormalizar.Visible = PermisosGui.Permite(BE.Patentes.ContratacionVenta);
            Cargar();
            Traducir();
        }

        private void Cargar()
        {
            try
            {
                int? sel = Seleccionada()?.IdContratacion;
                _pendientes = _bll.ObtenerPendientes();
                _grid.DataSource = ArmarTabla(_pendientes);
                UI.Columnas(_grid, ("Numero", 9), ("Cliente", 24), ("Plan", 17), ("Tipo", 13),
                                   ("Importe", 13), ("Intentos", 11), ("Antiguedad", 16));
                PintarAtencion();
                if (sel.HasValue) SeleccionarContratacion(sel.Value);
                MostrarDetalle();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Tabla de presentación (no se exponen ids técnicos salvo el número de contratación).
        private System.Data.DataTable ArmarTabla(List<BE.Contratacion> items)
        {
            var t = new System.Data.DataTable();
            t.Columns.Add("Numero", typeof(int));
            t.Columns.Add("Cliente", typeof(string));
            t.Columns.Add("Plan", typeof(string));
            t.Columns.Add("Tipo", typeof(string));
            t.Columns.Add("Importe", typeof(string));
            t.Columns.Add("Intentos", typeof(string));
            t.Columns.Add("Antiguedad", typeof(string));
            t.Columns.Add("_Id", typeof(int));
            t.Columns.Add("_Alerta", typeof(bool));
            foreach (var c in items)
            {
                int dias = Math.Max(0, (int)(DateTime.Now - c.FechaAlta).TotalDays);
                t.Rows.Add(c.IdContratacion, c.NombreCliente ?? "—", c.NombrePlan ?? "—",
                    UI.EtiquetaTipoContratacion(c.Tipo),
                    $"${c.Importe:N2}",
                    $"{c.IntentosPago}/{BLL.Contratacion.MAX_INTENTOS_PAGO}",
                    dias == 0 ? I18n.T("eh.cob.hoy", "hoy") : string.Format(I18n.T("eh.cob.dias", "hace {0} día(s)"), dias),
                    c.IdContratacion,
                    c.IntentosPago > 0 || dias >= 3);   // requiere atención: con rechazos o antigua
            }
            return t;
        }

        private void PintarAtencion()
        {
            if (!_grid.Columns.Contains("_Id")) return;
            _grid.Columns["_Id"].Visible = false;
            _grid.Columns["_Alerta"].Visible = false;
            foreach (DataGridViewRow row in _grid.Rows)
                if (row.Cells["_Alerta"].Value is bool a && a)
                    row.DefaultCellStyle.ForeColor = Estilo.Naranja;
        }

        private BE.Contratacion Seleccionada()
        {
            if (_grid.CurrentRow?.DataBoundItem is System.Data.DataRowView drv && drv.Row.Table.Columns.Contains("_Id"))
            {
                int id = Convert.ToInt32(drv["_Id"]);
                return _pendientes.Find(c => c.IdContratacion == id);
            }
            return null;
        }

        /// <summary>Recarga la cola (para el atajo cuando el form ya estaba abierto).</summary>
        public void Refrescar() => Cargar();

        /// <summary>Selecciona en la grilla la contratación indicada (navegación entre formularios).</summary>
        public void SeleccionarContratacion(int idContratacion)
        {
            foreach (DataGridViewRow row in _grid.Rows)
                if (row.Cells["_Id"].Value is int id && id == idContratacion)
                { row.Selected = true; _grid.CurrentCell = row.Cells[0]; return; }
        }

        private void MostrarDetalle()
        {
            var c = Seleccionada();
            bool hay = c != null;
            _rvCliente.Text  = c?.NombreCliente ?? "—";
            _rvPlan.Text     = c?.NombrePlan ?? "—";
            _rvImporte.Text  = hay ? $"${c.Importe:N2}" : "—";
            _rvIntentos.Text = hay ? $"{c.IntentosPago}/{BLL.Contratacion.MAX_INTENTOS_PAGO}" : "—";
            _rvIntentos.ForeColor = (hay && c.IntentosPago > 0) ? Estilo.Naranja : Estilo.AzulOscuro;
            _rvComprobante.Text = hay && !string.IsNullOrEmpty(c.NumeroComprobante) ? c.NumeroComprobante : "—";
            _cboMedio.Enabled = _cboCuotas.Enabled = _btnOk.Enabled = _btnFail.Enabled = hay;
            ActualizarCuotas();
            CalcularTotal();
        }

        private void ActualizarCuotas()
        {
            var medio = _cboMedio.SelectedItem is BE.MedioPago m ? m : BE.MedioPago.Efectivo;
            _cboCuotas.DataSource = BE.Contratacion.CuotasPermitidas(medio);
            _cboCuotas.SelectedItem = 1;
            _cboCuotas.Enabled = Seleccionada() != null && medio == BE.MedioPago.Tarjeta;
        }

        private void CalcularTotal()
        {
            var c = Seleccionada();
            if (c == null) { _rvTotal.Text = "—"; return; }
            int cuotas = _cboCuotas.SelectedItem is int q ? q : 1;
            decimal total = BE.Contratacion.ImporteConRecargo(c.Importe, cuotas);
            decimal recargo = BE.Contratacion.RecargoPorCuotas(cuotas);
            _rvTotal.Text = cuotas > 1
                ? $"${total:N2}  ·  {cuotas}x ${BE.Contratacion.ImportePorCuota(c.Importe, cuotas):N2}" +
                  (recargo > 0 ? $"  (+{recargo:0.##}%)" : "")
                : $"${total:N2}";
        }

        private void Cobrar(bool pagoOk)
        {
            var c = Seleccionada();
            if (c == null) { MostrarError(I18n.T("eh.cob.err.seleccion", "Seleccioná una contratación de la cola.")); return; }

            // Medio y cuotas SOLO se exigen/usan para el pago aprobado (un pago rechazado no se cobró
            // con ningún medio). En rechazo se pasan valores neutros que la BLL ignora.
            BE.MedioPago medio = BE.MedioPago.Efectivo;
            int cuotas = 1;
            if (pagoOk)
            {
                if (!(_cboMedio.SelectedItem is BE.MedioPago m))
                { MostrarError(I18n.T("eh.lbl.mediopago", "Indicá el medio de pago.")); return; }
                medio = m;
                cuotas = _cboCuotas.SelectedItem is int q ? q : 1;

                // Confirmación Sí/No con el total (con recargo) antes de mover dinero.
                decimal total = BE.Contratacion.ImporteConRecargo(c.Importe, cuotas);
                string detalle = cuotas > 1
                    ? string.Format(I18n.T("eh.cob.conf.cuotas", "{0} en {1} cuota(s) de {2}"),
                        $"${total:N2}", cuotas, $"${BE.Contratacion.ImportePorCuota(c.Importe, cuotas):N2}")
                    : $"${total:N2}";
                if (!Estilo.Confirmar(this, I18n.T("eh.cob.conf.titulo", "Confirmar cobro"),
                        string.Format(I18n.T("eh.cob.conf.cuerpo", "¿Registrar el pago de {0} por {1} ({2})?"),
                            c.NombreCliente, detalle, medio)))
                    return;
            }
            try
            {
                var r = _bll.RegistrarCobro(MODULO, c.IdContratacion, medio, cuotas, pagoOk);
                if (r.Estado == BE.EstadoContratacion.Pagada)
                {
                    Estilo.Exito(this, I18n.T("eh.cob.ok.titulo", "Pago concretado"),
                        string.Format(I18n.T("eh.cob.ok.cuerpo", "Comprobante {0}. Ahora Venta puede formalizar la suscripción."), r.NumeroComprobante));
                    OperacionRealizada?.Invoke();
                    // Documento 6 del diagrama: factura / comprobante (PDF).
                    if (Estilo.Confirmar(this, I18n.T("eh.cob.vercomprob.titulo", "Comprobante"),
                            I18n.T("eh.cob.vercomprob.cuerpo", "¿Ver el comprobante de pago en PDF?")))
                        Exportacion.DocumentosContratacion.Mostrar(Exportacion.DocumentosContratacion.Comprobante(r), this);
                    // Atajo admin: ofrecer saltar a formalizar esta contratación ya pagada.
                    if (PermisosGui.Permite(BE.Patentes.ContratacionVenta) &&
                        Estilo.Confirmar(this, I18n.T("eh.cob.irform.titulo", "Ir a formalizar"),
                            string.Format(I18n.T("eh.cob.irform.cuerpo", "¿Abrir «Para formalizar» con la contratación #{0}?"), r.IdContratacion)))
                        SolicitaIrAFormalizar?.Invoke(r.IdContratacion);
                }
                else if (r.Estado == BE.EstadoContratacion.Cancelada)
                {
                    Estilo.Error(this, I18n.T("eh.cob.cancel.titulo", "Contratación cancelada"),
                        string.Format(I18n.T("eh.cob.cancel.cuerpo", "Se alcanzaron {0} pagos rechazados. La contratación quedó cancelada."), BLL.Contratacion.MAX_INTENTOS_PAGO));
                    OperacionRealizada?.Invoke();
                    // Documento 5 del diagrama: constancia de cancelación (PDF) — SOLO en esta rama
                    // (cancelación por agotar intentos), nunca en los caminos de desistimiento.
                    if (Estilo.Confirmar(this, I18n.T("eh.cob.vercancel.titulo", "Constancia de cancelación"),
                            I18n.T("eh.cob.vercancel.cuerpo", "¿Ver la constancia de cancelación en PDF?")))
                        Exportacion.DocumentosContratacion.Mostrar(
                            Exportacion.DocumentosContratacion.ConstanciaCancelacion(r, BLL.Contratacion.MAX_INTENTOS_PAGO), this);
                }
                else
                    Estilo.Error(this, I18n.T("eh.cob.rech.titulo", "Pago rechazado"),
                        string.Format(I18n.T("eh.cob.rech.cuerpo", "Intento {0}/{1}. Podés reintentar el cobro."), r.IntentosPago, BLL.Contratacion.MAX_INTENTOS_PAGO));
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void IrAFormalizar()
        {
            var c = Seleccionada();
            SolicitaIrAFormalizar?.Invoke(c?.IdContratacion ?? 0);
        }
    }
}

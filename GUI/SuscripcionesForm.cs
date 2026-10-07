using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Gestión de la SUSCRIPCIÓN de un cliente: ver estado, renovar, suspender, reactivar.</summary>
    public class SuscripcionesForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.Suscripcion     _bll     = new BLL.Suscripcion();
        private readonly BLL.Cliente         _bllCli  = new BLL.Cliente();
        private readonly BLL.PlanSuscripcion _bllPlan = new BLL.PlanSuscripcion();
        private const string MODULO = "Suscripciones";

        private ComboBox _cboCliente, _cboPlan;
        private DateTimePicker _dtpVenc;
        private CheckBox _chkVenc;
        private Label _rvPlan, _rvEstado, _rvVence, _rvReservas;

        public SuscripcionesForm()
        {
            Text = "Suscripciones"; Size = new Size(760, 560); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(640, 500);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(UI.GapXL, UI.Gap, UI.GapXL, UI.Gap) };
            var card = UI.PanelEdicion(0);
            card.Dock = DockStyle.Fill;

            var campos = UI.GrillaCampos(150);

            UI.Seccion(campos, "Cliente", "eh.sec.cliente");
            _cboCliente = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboCliente.SelectedIndexChanged += (s, e) => MostrarEstado();
            UI.Campo(campos, "Cliente", _cboCliente, true, "eh.lbl.cliente");

            UI.Seccion(campos, "Suscripción actual", "eh.sec.suscactual");
            var resumen = UI.Resumen();
            _rvPlan     = UI.FilaResumenValor(resumen, "Plan");
            _rvEstado   = UI.FilaResumenValor(resumen, "Estado");
            _rvVence    = UI.FilaResumenValor(resumen, "Vence");
            _rvReservas = UI.FilaResumenValor(resumen, "Reservas");
            UI.CampoBloque(campos, resumen);

            UI.Seccion(campos, "Renovación", "eh.sec.renovacion");
            _cboPlan = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            UI.Campo(campos, "Plan", _cboPlan, false, "eh.lbl.planrenovar");

            _chkVenc = new CheckBox { Text = "Con vencimiento", Tag = "eh.lbl.venceel", AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
            _dtpVenc = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 130, Enabled = false, Value = DateTime.Today.AddMonths(1) };
            _chkVenc.CheckedChanged += (s, e) => _dtpVenc.Enabled = _chkVenc.Checked;
            var venc = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            venc.Controls.Add(_chkVenc); venc.Controls.Add(_dtpVenc);
            UI.Campo(campos, "Vencimiento", venc, false, "eh.lbl.vencimiento");

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            scroll.Controls.Add(campos);
            card.Controls.Add(scroll);
            host.Controls.Add(card);

            var barra = UI.BarraAccionesLR(
                new[] { UI.Secundario("Actualizar", "eh.btn.refrescar", (s, e) => Recargar()) },
                new[] {
                    UI.Primario("Renovar",   "eh.btn.renovar",   (s, e) => Renovar()),
                    UI.Peligro ("Suspender", "eh.btn.suspender", (s, e) => Accion(true)),
                    UI.Exito   ("Reactivar", "eh.btn.reactivar", (s, e) => Accion(false))
                });

            var header = UI.Encabezado("Suscripciones",
                "Estado de la suscripción del cliente y acciones: renovar, suspender o reactivar.",
                "eh.frm.suscripciones", "eh.sus.header.desc");

            Controls.Add(host);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.suscripciones", this.Text); I18n.TraducirControles(this); }

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
            // mensual al abrir el módulo, para que el estado mostrado esté al día.
            try { _bll.ActualizarEstadosPorFecha(); } catch (Exception ex) { System.Diagnostics.Trace.TraceError("[Suscripciones] " + ex.Message); }
            Recargar();
            Traducir();
        }

        // Refresca clientes y planes desde la BD (preservando la selección) y re-muestra el estado.
        private void Recargar()
        {
            try
            {
                int? cli  = _cboCliente.SelectedValue as int?;
                int? plan = _cboPlan.SelectedValue as int?;
                _cboCliente.DataSource = _bllCli.ObtenerTodos(); _cboCliente.DisplayMember = "NombreCompleto"; _cboCliente.ValueMember = "IdCliente";
                _cboPlan.DataSource    = _bllPlan.ObtenerActivos(); _cboPlan.DisplayMember = "Nombre"; _cboPlan.ValueMember = "IdPlan";
                if (cli.HasValue)  _cboCliente.SelectedValue = cli.Value;
                if (plan.HasValue) _cboPlan.SelectedValue    = plan.Value;
                MostrarEstado();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void MostrarEstado()
        {
            if (_rvPlan == null) return;
            try
            {
                if (!(_cboCliente.SelectedValue is int idCli)) return;
                var s = _bll.ObtenerVigentePorCliente(idCli);
                if (s == null)
                {
                    _rvPlan.Text = "Sin suscripción"; _rvPlan.ForeColor = Estilo.Rojo;
                    _rvEstado.Text = "—"; _rvVence.Text = "—"; _rvReservas.Text = "—";
                    return;
                }
                _rvPlan.Text     = s.NombrePlan; _rvPlan.ForeColor = Estilo.AzulOscuro;
                // Estado REAL considerando la fecha: una Activa pasada de vencimiento se muestra
                // como Vencida (el texto y el color coinciden), sin esperar a la tarea de sistema.
                bool vigente = s.EstaVigente();
                bool porVencer = s.ProximaAVencer(7);
                _rvEstado.Text = s.EstadoVigenciaCalculado(DateTime.Today).ToString();
                _rvEstado.ForeColor = !vigente ? Estilo.Rojo : (porVencer ? Estilo.Coral : Estilo.Verde);

                string vence = s.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "sin límite";
                if (vigente && porVencer)
                    vence += $"  ·  ⚠ vence en {s.DiasHastaVencimiento()} día(s)";
                _rvVence.Text = vence;
                _rvVence.ForeColor = (vigente && porVencer) ? Estilo.Coral : Estilo.AzulOscuro;

                _rvReservas.Text = $"{s.ReservasConsumidasMes} / {s.ReservasDelPlan} usadas  ·  restan {s.ReservasRestantes()}";
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
                _bll.Renovar(MODULO, idCli, idPlan, venc);
                Estilo.Exito(this, "Suscripción renovada", "El cliente ya puede volver a reservar experiencias.");
                MostrarEstado();
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
                if (suspender) _bll.Suspender(MODULO, idCli); else _bll.Reactivar(MODULO, idCli);
                MostrarOk(suspender ? "Suscripción suspendida." : "Suscripción reactivada.");
                MostrarEstado();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

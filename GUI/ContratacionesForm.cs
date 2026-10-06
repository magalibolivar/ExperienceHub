using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// PN01 — Comercialización de la suscripción (code-only). Un solo form cubre el ciclo:
    ///   • Venta registra la contratación (queda Pendiente de pago)          — CU01-VTA
    ///   • Caja cobra y emite el comprobante en un paso; al 3er intento
    ///     fallido la contratación se cancela                               — CU01-CAJ + CU02-CAJ
    ///   • Venta formaliza la suscripción vigente                           — CU01-VTA (cierre)
    /// La grilla muestra las contrataciones "en proceso" (Pendiente o Pagada): una fila va
    /// Pendiente → (cobrar) → Pagada → (formalizar) → sale de la cola.
    /// </summary>
    public class ContratacionesForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.Contratacion   _bll     = new BLL.Contratacion();
        private readonly BLL.Cliente         _bllCli  = new BLL.Cliente();
        private readonly BLL.PlanSuscripcion _bllPlan = new BLL.PlanSuscripcion();
        private const string MODULO = "Comercializacion";

        private ComboBox _cboCliente, _cboPlan, _cboMedio, _cboCuotas;
        private DataGridView _grid;

        public ContratacionesForm()
        {
            Text = "Contrataciones (Caja)"; Size = new Size(960, 600); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(820, 520);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            UI.ConectarEstadoVacio(_grid,
                "No hay contrataciones en proceso.\nRegistrá una nueva (Venta) para iniciar el cobro.",
                "eh.empty.contrataciones");

            // Alta de contratación (Venta: cliente + plan) y medio de pago (Caja), en la barra superior.
            _cboCliente = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboPlan    = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboMedio   = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboMedio.DataSource = Enum.GetValues(typeof(BE.MedioPago));
            // Cuotas: solo se habilitan para Tarjeta; para el resto queda en 1 y deshabilitado.
            _cboCuotas  = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboMedio.SelectedIndexChanged += (s, e) => ActualizarCuotas();

            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Cliente",       _cboCliente, 240, "eh.lbl.cliente"),
                UI.FiltroCampo("Plan",          _cboPlan,    200, "eh.lbl.plan"),
                UI.FiltroCampo("Medio de pago", _cboMedio,   160, "eh.lbl.mediopago"),
                UI.FiltroCampo("Cuotas",        _cboCuotas,  110, "eh.lbl.cuotas"));

            var barra = UI.BarraAccionesLR(
                new[]
                {
                    UI.Secundario("Refrescar",              "eh.btn.refrescar",        (s, e) => Cargar()),
                    UI.Primario  ("Registrar contratación", "eh.btn.nuevacontratacion",(s, e) => Crear())
                },
                new[]
                {
                    UI.Exito   ("Cobrar (pago OK)",        "eh.btn.cobrar",     (s, e) => Cobrar(true)),
                    UI.Peligro ("Intento fallido",         "eh.btn.intento",    (s, e) => Cobrar(false)),
                    UI.Primario("Formalizar suscripción",  "eh.btn.formalizar", (s, e) => Formalizar())
                });

            var header = UI.Encabezado("Comercialización de la suscripción",
                "Venta registra la contratación (pendiente de pago); Caja cobra y emite el comprobante; Venta formaliza la suscripción.",
                "eh.frm.contrataciones", "eh.con.header.desc");

            Controls.Add(_grid);
            Controls.Add(filtros);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.contrataciones", this.Text); I18n.TraducirControles(this); }

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
                _cboCliente.DataSource = _bllCli.ObtenerTodos();    _cboCliente.DisplayMember = "NombreCompleto"; _cboCliente.ValueMember = "IdCliente";
                _cboPlan.DataSource    = _bllPlan.ObtenerActivos(); _cboPlan.DisplayMember   = "Nombre";          _cboPlan.ValueMember    = "IdPlan";
            }
            catch (Exception ex) { MostrarError(ex); }
            ActualizarCuotas();
            Cargar();
            Traducir();
        }

        // Habilita y puebla el combo de cuotas según el medio de pago (solo Tarjeta financia).
        private void ActualizarCuotas()
        {
            var medio = _cboMedio.SelectedItem is BE.MedioPago m ? m : BE.MedioPago.Efectivo;
            _cboCuotas.DataSource = BE.Contratacion.CuotasPermitidas(medio);
            _cboCuotas.Enabled    = medio == BE.MedioPago.Tarjeta;
            _cboCuotas.SelectedItem = 1;
        }

        private void Cargar()
        {
            try
            {
                _grid.DataSource = _bll.ObtenerActivas();
                UI.Columnas(_grid,
                    ("IdContratacion", 8), ("NombreCliente", 26), ("NombrePlan", 18),
                    ("Importe", 12), ("Estado", 16), ("IntentosPago", 8), ("FechaAlta", 16));
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private BE.Contratacion Seleccionada() => _grid.CurrentRow?.DataBoundItem as BE.Contratacion;

        // CU01-VTA — Venta registra la contratación (pendiente de pago).
        private void Crear()
        {
            try
            {
                if (!(_cboCliente.SelectedValue is int idCli) || !(_cboPlan.SelectedValue is int idPlan))
                { MostrarError("Seleccioná un cliente y un plan."); return; }
                int id = _bll.CrearContratacion(MODULO, idCli, idPlan);
                Estilo.Exito(this, "Contratación registrada",
                    $"Contratación #{id} queda pendiente de pago. Derivala a Caja para el cobro.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // CU01-CAJ + CU02-CAJ — Caja cobra (y emite comprobante) o registra intento fallido.
        private void Cobrar(bool pagoOk)
        {
            var c = Seleccionada();
            if (c == null) { MostrarError("Seleccioná una contratación de la cola."); return; }
            if (!(_cboMedio.SelectedItem is BE.MedioPago medio)) { MostrarError("Indicá el medio de pago."); return; }
            int cuotas = _cboCuotas.SelectedItem is int q ? q : 1;
            try
            {
                var r = _bll.RegistrarCobro(MODULO, c.IdContratacion, medio, cuotas, pagoOk);
                if (r.Estado == BE.EstadoContratacion.Pagada)
                {
                    string detalleCuotas = r.Cuotas > 1
                        ? $" {r.Cuotas} cuotas de ${BE.Contratacion.ImportePorCuota(r.Importe, r.Cuotas):0.00}" +
                          (r.RecargoPorcentaje > 0 ? $" (+{r.RecargoPorcentaje:0.##}% financiación)" : "") +
                          $" — total ${r.ImporteTotal:0.00}."
                        : $" Total ${r.ImporteTotal:0.00}.";
                    Estilo.Exito(this, "Pago concretado",
                        $"Comprobante {r.NumeroComprobante}.{detalleCuotas} Ahora formalizá la suscripción.");
                }
                else if (r.Estado == BE.EstadoContratacion.Cancelada)
                    Estilo.Error(this, "Contratación cancelada",
                        $"Se alcanzaron {BLL.Contratacion.MAX_INTENTOS_PAGO} intentos de pago fallidos. La contratación quedó cancelada.");
                else
                    MostrarOk($"Intento fallido registrado ({r.IntentosPago}/{BLL.Contratacion.MAX_INTENTOS_PAGO}). Podés reintentar el cobro.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // CU01-VTA (cierre) — Venta formaliza la suscripción vigente a partir del pago.
        private void Formalizar()
        {
            var c = Seleccionada();
            if (c == null) { MostrarError("Seleccioná una contratación pagada."); return; }
            try
            {
                int idSus = _bll.Formalizar(MODULO, c.IdContratacion);
                Estilo.Exito(this, "Suscripción formalizada",
                    $"Suscripción #{idSus} vigente. Constancia generada.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// PN01 · Venta — "Para formalizar" (cierre de CU01-VTA). Grilla de contrataciones Pagadas (ya
    /// cobradas por Caja) + detalle lateral; "Formalizar suscripción" genera la suscripción vigente
    /// (BLL.Contratacion.Formalizar) y emite la constancia. Name = "FormalizacionForm" para el gateo
    /// por ControlMapeado. Reusa las reglas de negocio; no altera estados ni cálculos.
    /// </summary>
    public class FormalizacionForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.Contratacion _bll    = new BLL.Contratacion();
        private readonly BLL.Suscripcion   _bllSus = new BLL.Suscripcion();
        private const string MODULO = "Para formalizar";

        private DataGridView _grid;
        private Label _rvCliente, _rvPlan, _rvImporte, _rvComprobante, _rvFechaPago;
        private Button _btnFormalizar;
        private List<BE.Contratacion> _pagadas = new List<BE.Contratacion>();

        /// <summary>Se dispara tras formalizar: el Menú refresca sus contadores.</summary>
        public event Action OperacionRealizada;

        public FormalizacionForm()
        {
            Name = "FormalizacionForm";
            Text = "Para formalizar"; Size = new Size(1000, 600); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(880, 520);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            _grid.SelectionChanged += (s, e) => MostrarDetalle();
            UI.ConectarEstadoVacio(_grid,
                "No hay contrataciones para formalizar.\nAparecen acá cuando Caja concreta el pago.", "eh.form.empty");

            var panel = UI.PanelEdicion(360);
            var resumen = UI.Resumen(); resumen.Dock = DockStyle.Top;
            _rvCliente     = UI.FilaResumenValor(resumen, "Cliente");
            _rvPlan        = UI.FilaResumenValor(resumen, "Plan");
            _rvImporte     = UI.FilaResumenValor(resumen, "Importe cobrado");
            _rvComprobante = UI.FilaResumenValor(resumen, "Comprobante");
            _rvFechaPago   = UI.FilaResumenValor(resumen, "Fecha de pago");

            var contenido = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            contenido.Controls.Add(resumen);
            contenido.Controls.Add(UI.TituloSeccion("Contratación pagada", "eh.form.sec.detalle"));

            _btnFormalizar = UI.Primario("Formalizar suscripción", "eh.form.btn.formalizar", (s, e) => Formalizar());
            var barraForm = UI.BarraAcciones(_btnFormalizar);
            barraForm.BackColor = Color.White;

            panel.Controls.Add(contenido);
            panel.Controls.Add(barraForm);

            var barra = UI.BarraAccionesLR(
                new[] { UI.Secundario("Actualizar", "eh.btn.refrescar", (s, e) => Cargar()) },
                new Button[0]);

            // Grilla dentro de una tarjeta (misma identidad que el resto de PN01).
            var cardGrid = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                              Margin = new Padding(UI.GapXL, UI.GapL, UI.Gap, UI.GapL), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
            cardGrid.Controls.Add(_grid);
            cardGrid.Controls.Add(UI.TituloSeccion("Contrataciones pagadas", "eh.form.sec.cola"));

            var header = UI.Encabezado("Para formalizar",
                "Contrataciones ya cobradas. Formalizá la suscripción para dejarla vigente.",
                "eh.form.titulo", "eh.form.header.desc");

            Controls.Add(cardGrid);
            Controls.Add(panel);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.form.titulo", this.Text); I18n.TraducirControles(this); }

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
                int? sel = Seleccionada()?.IdContratacion;
                _pagadas = _bll.ObtenerPagadas();
                _grid.DataSource = ArmarTabla(_pagadas);
                UI.Columnas(_grid, ("Numero", 9), ("Cliente", 24), ("Plan", 16), ("Tipo", 12),
                                   ("FechaPago", 15), ("Comprobante", 18), ("Importe", 11));
                if (_grid.Columns.Contains("_Id")) _grid.Columns["_Id"].Visible = false;
                if (sel.HasValue) SeleccionarContratacion(sel.Value);
                MostrarDetalle();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private System.Data.DataTable ArmarTabla(List<BE.Contratacion> items)
        {
            var t = new System.Data.DataTable();
            t.Columns.Add("Numero", typeof(int));
            t.Columns.Add("Cliente", typeof(string));
            t.Columns.Add("Plan", typeof(string));
            t.Columns.Add("Tipo", typeof(string));
            t.Columns.Add("FechaPago", typeof(string));
            t.Columns.Add("Comprobante", typeof(string));
            t.Columns.Add("Importe", typeof(string));
            t.Columns.Add("_Id", typeof(int));
            foreach (var c in items)
                t.Rows.Add(c.IdContratacion, c.NombreCliente ?? "—", c.NombrePlan ?? "—",
                    UI.EtiquetaTipoContratacion(c.Tipo),
                    c.FechaPago?.ToString("dd/MM/yyyy HH:mm") ?? "—",
                    c.NumeroComprobante ?? "—",
                    $"${(c.ImporteTotal > 0 ? c.ImporteTotal : c.Importe):N2}",
                    c.IdContratacion);
            return t;
        }

        private BE.Contratacion Seleccionada()
        {
            if (_grid.CurrentRow?.DataBoundItem is System.Data.DataRowView drv && drv.Row.Table.Columns.Contains("_Id"))
            {
                int id = Convert.ToInt32(drv["_Id"]);
                return _pagadas.Find(c => c.IdContratacion == id);
            }
            return null;
        }

        /// <summary>Recarga la cola (para el atajo cuando el form ya estaba abierto).</summary>
        public void Refrescar() => Cargar();

        /// <summary>Selecciona la contratación indicada (navegación entre formularios).</summary>
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
            _rvCliente.Text     = c?.NombreCliente ?? "—";
            _rvPlan.Text        = c?.NombrePlan ?? "—";
            _rvImporte.Text     = hay ? $"${(c.ImporteTotal > 0 ? c.ImporteTotal : c.Importe):N2}" : "—";
            _rvComprobante.Text = hay ? (c.NumeroComprobante ?? "—") : "—";
            _rvFechaPago.Text   = hay ? (c.FechaPago?.ToString("dd/MM/yyyy HH:mm") ?? "—") : "—";
            _btnFormalizar.Enabled = hay && c.PuedeFormalizarse();
        }

        private void Formalizar()
        {
            var c = Seleccionada();
            if (c == null) { MostrarError(I18n.T("eh.form.err.seleccion", "Seleccioná una contratación pagada.")); return; }
            try
            {
                bool esRenov = c.Tipo == BE.TipoContratacion.Renovacion;
                int idSus = _bll.Formalizar(MODULO, c.IdContratacion);
                Estilo.Exito(this,
                    esRenov ? I18n.T("eh.form.okrenov.titulo", "Renovación formalizada")
                            : I18n.T("eh.form.ok.titulo", "Suscripción formalizada"),
                    string.Format(esRenov
                        ? I18n.T("eh.form.okrenov.cuerpo", "Suscripción #{0} renovada: vencimiento extendido un mes.")
                        : I18n.T("eh.form.ok.cuerpo", "Suscripción #{0} vigente. Constancia generada."), idSus));
                OperacionRealizada?.Invoke();
                // Documento 7 del diagrama: constancia de suscripción (PDF).
                var s = _bllSus.ObtenerVigentePorCliente(c.IdCliente);
                if (s != null && Estilo.Confirmar(this, I18n.T("eh.form.verconst.titulo", "Constancia de suscripción"),
                        I18n.T("eh.form.verconst.cuerpo", "¿Ver la constancia de suscripción en PDF?")))
                    Exportacion.DocumentosContratacion.Mostrar(
                        Exportacion.DocumentosContratacion.ConstanciaSuscripcion(c, s), this);
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

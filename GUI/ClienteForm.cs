using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;
using System.Linq;

namespace GUI
{
    /// <summary>
    /// Diálogo modal para dar de alta o modificar un cliente.
    /// Se abre desde el formulario Clientes con ShowDialog().
    /// Devuelve DialogResult.OK con ClienteEditado cargado si el usuario confirma.
    /// </summary>
    public partial class ClienteForm : Form
    {
        // ── Resultado del diálogo ─────────────────────────────────────────────
        public BE.Cliente ClienteEditado { get; private set; }

        // ── Modo del formulario ───────────────────────────────────────────────
        private readonly bool _esEdicion;
        private readonly BE.Cliente _clienteOriginal;

        private List<BE.PlanSuscripcion> _planes;
        private CheckedListBox _clbIntereses;

        /// <summary>Constructor para ALTA (cliente nuevo).</summary>
        public ClienteForm() : this(null) { }

        /// <summary>
        /// Constructor para ALTA o EDICIÓN.
        /// Si cliente es null, modo alta; si tiene datos, modo edición.
        /// </summary>
        public ClienteForm(BE.Cliente cliente)
        {
            InitializeComponent();
            try { string ico = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico"); if (System.IO.File.Exists(ico)) this.Icon = new System.Drawing.Icon(ico); } catch { }

            _esEdicion       = cliente != null;
            _clienteOriginal = cliente;

            CargarPlanes();
            RellenarComboCiudades();
            ConstruirSelectorIntereses();
            AplicarIdioma(GestorIdioma.IdiomaActual);

            if (_esEdicion) CargarDatosExistentes();

            ReorganizarLayout();
        }

        // Rediseño del diálogo en SECCIONES (reutiliza los mismos controles/handlers del Designer):
        //   Datos personales · Contacto · Suscripción  (izquierda)  +  Intereses (derecha).
        private void ReorganizarLayout()
        {
            this.SuspendLayout();
            this.Controls.Clear();               // los controles se reubican (no se destruyen)
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false; this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new System.Drawing.Size(680, 610);
            this.BackColor = System.Drawing.Color.White;

            // Footer: Cancelar (secundario) + Guardar (primario)
            btnGuardar.AccessibleDescription  = "rol:primario";
            btnCancelar.AccessibleDescription = "rol:secundario";
            var footer = UI.BarraAcciones(btnGuardar, btnCancelar);
            footer.BackColor = System.Drawing.Color.White;

            lblMensaje.Dock = DockStyle.Bottom; lblMensaje.Height = 24;
            lblMensaje.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblMensaje.Padding = new Padding(UI.GapXL, 0, 0, 0);
            lblMensaje.ForeColor = Estilo.Rojo;

            // Derecha: Intereses
            var derecha = new Panel { Dock = DockStyle.Right, Width = 220, Padding = new Padding(0, 4, UI.GapXL, 0), BackColor = System.Drawing.Color.White };
            _clbIntereses.Dock = DockStyle.Fill; _clbIntereses.BorderStyle = BorderStyle.FixedSingle;
            var secInt = UI.TituloSeccion("Intereses"); secInt.Dock = DockStyle.Top;
            derecha.Controls.Add(_clbIntereses);
            derecha.Controls.Add(secInt);

            // Izquierda: campos agrupados en secciones
            var campos = UI.GrillaCampos(150);
            UI.Seccion(campos, "Datos personales");
            UI.Campo(campos, "Nombre",              txtNombre,          true);
            UI.Campo(campos, "Apellido",            txtApellido,        true);
            UI.Campo(campos, "DNI (7–8 dígitos)",   txtDNI,             true);
            UI.Campo(campos, "Fecha de nacimiento", dtpFechaNacimiento, true);
            UI.Seccion(campos, "Contacto");
            UI.Campo(campos, "Email",               txtEmail,           false);
            UI.Campo(campos, "Ciudad",              cmbMetodoPago,      true);
            UI.Seccion(campos, "Suscripción");
            UI.Campo(campos, "Plan",                cmbPlan,            false);
            var venc = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            chkVencimiento.AutoSize = true; chkVencimiento.Margin = new Padding(0, 4, 8, 0);
            venc.Controls.Add(chkVencimiento); venc.Controls.Add(dtpVencimiento);
            UI.Campo(campos, "Vencimiento",         venc,               false);

            var izq = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(UI.GapXL, 0, UI.GapL, 0), BackColor = System.Drawing.Color.White };
            izq.Controls.Add(campos);

            var header = UI.Encabezado(_esEdicion ? "Editar cliente" : "Nuevo cliente",
                "Datos del cliente, su ciudad, plan de suscripción e intereses.");

            this.Controls.Add(izq);        // Fill (centro)
            this.Controls.Add(derecha);    // Right (intereses)
            this.Controls.Add(footer);     // Bottom (botones)
            this.Controls.Add(lblMensaje); // Bottom (mensaje, arriba de los botones)
            this.Controls.Add(header);     // Top (título)

            this.AcceptButton = btnGuardar;
            this.CancelButton = btnCancelar;
            Estilo.Aplicar(this);
            this.ResumeLayout();
        }

        // Selector de intereses, ubicado en el área derecha libre del diálogo.
        private void ConstruirSelectorIntereses()
        {
            this.ClientSize = new System.Drawing.Size(620, this.ClientSize.Height);
            var lbl = new Label { Text = "Intereses", Location = new System.Drawing.Point(392, 40), AutoSize = true };
            _clbIntereses = new CheckedListBox
            {
                Location = new System.Drawing.Point(392, 62),
                Size = new System.Drawing.Size(210, 400),
                CheckOnClick = true, IntegralHeight = false,
                DisplayMember = "Nombre"
            };
            try
            {
                foreach (var i in new BLL.Interes().ObtenerTodos())
                    _clbIntereses.Items.Add(i);
            }
            catch { }
            this.Controls.Add(lbl);
            this.Controls.Add(_clbIntereses);
        }

        // ── Traducción ────────────────────────────────────────────────────────

        /// <summary>
        /// Aplica el idioma activo al abrir el diálogo.
        /// ClienteForm es modal → no necesita Observer completo, basta con leer en construcción.
        /// </summary>
        private void AplicarIdioma(Idioma idioma)
        {
            var t = Traductor.ObtenerTraducciones(idioma);
            string T(string key, string fallback) => t.ContainsKey(key) ? t[key].Texto : fallback;

            this.Text       = _esEdicion ? T("frm.editarcliente", "Editar Cliente")
                                         : T("frm.nuevocliente",  "Nuevo Cliente");
            btnGuardar.Text = _esEdicion ? T("btn.guardar.cambios",  "Guardar Cambios")
                                         : T("btn.registrar.cliente","Registrar Cliente");
            btnCancelar.Text  = T("btn.cancelar",        "Cancelar");
            lblNombre.Text          = T("lbl.cli.nombre",      "Nombre *");
            lblApellido.Text        = T("lbl.cli.apellido",    "Apellido *");
            lblDNI.Text             = T("lbl.cli.dni",         "DNI * (7-8 dígitos)");
            lblEmail.Text           = T("lbl.cli.email",       "Email");
            lblFechaNacimiento.Text = T("lbl.cli.fechanac",    "Fecha de Nacimiento *");
            lblMetodoPago.Text      = T("lbl.cli.ciudad",      "Ciudad *");
            lblPlan.Text            = T("lbl.cli.plan",        "Plan de Suscripción *");
            chkVencimiento.Text     = T("lbl.cli.vencimiento", "Fecha de Vencimiento");

            // Actualizar ítem "— Sin plan —" del combo de planes (índice 0)
            if (cmbPlan.Items.Count > 0)
                cmbPlan.Items[0] = T("combo.cli.sinplan", "— Sin plan —");
        }

        // El combo antes usado para el Método de Pago se reutiliza como selector de Ciudad.
        private void RellenarComboCiudades()
        {
            try
            {
                var ciudades = new BLL.Ciudad().ObtenerActivas();
                cmbMetodoPago.DataSource    = null;
                cmbMetodoPago.DisplayMember = "Nombre";
                cmbMetodoPago.ValueMember   = "IdCiudad";
                cmbMetodoPago.DataSource    = ciudades;
                if (ciudades.Count > 0) cmbMetodoPago.SelectedIndex = 0;
            }
            catch { }
        }

        // ── Eventos del Designer ──────────────────────────────────────────────

        private void ChkVencimiento_CheckedChanged(object sender, EventArgs e)
        {
            dtpVencimiento.Enabled = chkVencimiento.Checked;
        }

        private void BtnCancelar_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        // ── Carga de datos ────────────────────────────────────────────────────

        private void CargarPlanes()
        {
            try
            {
                var bllPlan = new BLL.PlanSuscripcion();
                _planes = bllPlan.ObtenerActivos();

                var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
                string sinPlan = t.ContainsKey("combo.cli.sinplan") ? t["combo.cli.sinplan"].Texto : "— Sin plan —";
                cmbPlan.Items.Clear();
                cmbPlan.Items.Add(sinPlan);
                foreach (var p in _planes)
                    cmbPlan.Items.Add($"{p.Nombre}  ({p.ReservasMensuales} reservas/mes — ${p.Precio:N2})");

                cmbPlan.SelectedIndex = 0;
            }
            catch
            {
                var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
                string errPlanes = t.ContainsKey("err.cli.errorplanes") ? t["err.cli.errorplanes"].Texto : "— Error al cargar planes —";
                cmbPlan.Items.Add(errPlanes);
                cmbPlan.SelectedIndex = 0;
            }
        }

        private void CargarDatosExistentes()
        {
            txtNombre.Text   = _clienteOriginal.Nombre;
            txtApellido.Text = _clienteOriginal.Apellido;
            txtDNI.Text      = _clienteOriginal.DNI;
            txtEmail.Text    = _clienteOriginal.Email ?? "";

            // Seleccionar la ciudad del cliente
            if (_clienteOriginal.IdCiudad.HasValue)
                cmbMetodoPago.SelectedValue = _clienteOriginal.IdCiudad.Value;

            // Seleccionar el plan de la suscripción actual
            var sus = _clienteOriginal.Suscripcion;
            if (sus != null && _planes != null)
            {
                int planIdx = _planes.FindIndex(p => p.IdPlan == sus.IdPlan);
                cmbPlan.SelectedIndex = planIdx >= 0 ? planIdx + 1 : 0;  // +1 por "Sin plan"
            }

            // Fecha de nacimiento
            dtpFechaNacimiento.Value = _clienteOriginal.FechaNacimiento.HasValue
                ? _clienteOriginal.FechaNacimiento.Value
                : DateTime.Today.AddYears(-18);

            // Fecha de vencimiento de la suscripción
            if (sus != null && sus.FechaVencimiento.HasValue)
            {
                chkVencimiento.Checked = true;
                dtpVencimiento.Enabled = true;
                dtpVencimiento.Value   = sus.FechaVencimiento.Value;
            }

            // Marcar los intereses actuales del cliente
            if (_clbIntereses != null && _clienteOriginal.Intereses != null)
                for (int i = 0; i < _clbIntereses.Items.Count; i++)
                    if (_clbIntereses.Items[i] is BE.Interes it &&
                        _clienteOriginal.Intereses.Exists(x => x.IdInteres == it.IdInteres))
                        _clbIntereses.SetItemChecked(i, true);
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            lblMensaje.Text = string.Empty;

            try
            {
                int? idPlan = null;
                if (cmbPlan.SelectedIndex > 0 && _planes != null && _planes.Count >= cmbPlan.SelectedIndex)
                    idPlan = _planes[cmbPlan.SelectedIndex - 1].IdPlan;

                int? idCiudad = cmbMetodoPago.SelectedValue as int?;
                DateTime? venc = chkVencimiento.Checked ? dtpVencimiento.Value.Date : (DateTime?)null;

                ClienteEditado = new BE.Cliente
                {
                    IdCliente       = _esEdicion ? _clienteOriginal.IdCliente : 0,
                    Nombre          = txtNombre.Text.Trim(),
                    Apellido        = txtApellido.Text.Trim(),
                    DNI             = txtDNI.Text.Trim(),
                    Email           = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim(),
                    IdCiudad        = idCiudad,
                    FechaNacimiento = dtpFechaNacimiento.Value.Date,
                    FechaAlta       = _esEdicion ? _clienteOriginal.FechaAlta : DateTime.Now,
                    // La suscripción (plan + vencimiento) viaja en la navegación Suscripcion.
                    Suscripcion     = idPlan.HasValue
                        ? new BE.Suscripcion { IdPlan = idPlan.Value, FechaVencimiento = venc, Estado = BE.EstadoSuscripcion.Activa }
                        : null
                };

                // Intereses marcados
                if (_clbIntereses != null)
                    foreach (var obj in _clbIntereses.CheckedItems)
                        if (obj is BE.Interes it) ClienteEditado.Intereses.Add(it);

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                lblMensaje.Text = $"✗ {ex.Message}";
            }
        }
    }
}

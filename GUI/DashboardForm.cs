using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// Panel de Control de ExperienceHub. Resume el estado del negocio con KPIs reales (clientes,
    /// experiencias disponibles/completas, reservas del día/pendientes, suscripciones por vencer,
    /// backup e integridad) + la actividad de negocio reciente. Carga asíncrona + auto-refresh.
    /// Toda la métrica proviene de BLL (no se calcula nada en la GUI).
    /// </summary>
    public class DashboardForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.Experiencia          _bllExp      = new BLL.Experiencia();
        private readonly BLL.Cliente              _bllCliente  = new BLL.Cliente();
        private readonly BLL.Reserva              _bllReserva  = new BLL.Reserva();
        private readonly BLL.ReporteJornada       _reporte     = new BLL.ReporteJornada();
        private readonly BLL.Bitacora             _bllBitacora = new BLL.Bitacora();
        private readonly BLL.AnalisisSuscripciones _bllSusc    = new BLL.AnalisisSuscripciones();

        // 8 KPIs (2 filas x 4). Índices: 0 Clientes · 1 Exp. disp. · 2 Exp. completas · 3 Reservas hoy
        //                                4 Reservas pend. · 5 Susc. por vencer · 6 Días s/ backup · 7 Integridad
        private Label[] _kpi;
        private DataGridView _dgvActividad;
        private System.Windows.Forms.Timer _timer;

        public DashboardForm() : this(null) { }

        public DashboardForm(List<BE.Permiso> permisos)
        {
            this.Text        = "Panel de Control";
            this.Size        = new Size(1040, 660);
            this.MinimumSize = new Size(760, 520);
            this.BackColor   = Estilo.Lienzo;
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            this.Font    = Estilo.Normal(9.5f);
            this.Padding = new Padding(24);

            // ── Actividad reciente (tarjeta que ocupa el resto) ──
            var cardAct = new TarjetaPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 16) };
            var lblAct = new Label
            {
                Text = "Actividad de negocio reciente", Dock = DockStyle.Top, Height = 28,
                Font = Estilo.Bold(11f), ForeColor = Estilo.AzulOscuro, BackColor = Color.White, Tag = "eh.dash.actividad"
            };
            _dgvActividad = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                BackgroundColor = Color.White, BorderStyle = BorderStyle.None
            };
            Estilo.EstilizarGrid(_dgvActividad);
            cardAct.Controls.Add(_dgvActividad);
            cardAct.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.White });
            cardAct.Controls.Add(lblAct);

            // ── KPIs (2 filas x 4 columnas) ──
            var kpis = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 200, ColumnCount = 4, RowCount = 2,
                Padding = new Padding(0, 0, 0, 16), BackColor = Color.Transparent
            };
            for (int i = 0; i < 4; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            for (int i = 0; i < 2; i++) kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            _kpi = new Label[8];
            string[] etiquetas =
            {
                "CLIENTES", "EXPERIENCIAS DISPONIBLES", "EXPERIENCIAS COMPLETAS", "RESERVAS DE HOY",
                "RESERVAS PENDIENTES", "SUSCRIPCIONES POR VENCER", "DÍAS SIN BACKUP", "INTEGRIDAD"
            };
            string[] tags =
            {
                "eh.dash.kpi.clientes", "eh.dash.kpi.expdisp", "eh.dash.kpi.expcompl", "eh.dash.kpi.reshoy",
                "eh.dash.kpi.respend", "eh.dash.kpi.suscvencer", "eh.dash.kpi.backup", "eh.dash.kpi.integridad"
            };
            for (int i = 0; i < 8; i++)
                kpis.Controls.Add(TarjetaKpi(etiquetas[i], tags[i], out _kpi[i]), i % 4, i / 4);

            // ── Encabezado ──
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.Transparent };
            var titulo = new Label
            {
                Text = "Panel de Control", Dock = DockStyle.Top, Height = 40, AutoSize = false,
                Font = Estilo.Titulo(22f), ForeColor = Estilo.AzulOscuro, Tag = "eh.frm.dashboard"
            };
            var slogan = new Label
            {
                Text = "Estado del negocio de un vistazo.", Dock = DockStyle.Top, Height = 22, AutoSize = false,
                Font = new Font(Estilo.Fuente, 10f, FontStyle.Italic), ForeColor = Estilo.Gris700, Tag = "eh.dash.slogan"
            };
            header.Controls.Add(slogan);
            header.Controls.Add(titulo);

            this.Controls.Add(cardAct);
            this.Controls.Add(kpis);
            this.Controls.Add(header);

            _timer = new System.Windows.Forms.Timer { Interval = 60000 };
            _timer.Tick += (s, e) => Cargar();
            _timer.Start();
        }

        /// <summary>Tarjeta KPI: etiqueta en mayúsculas arriba, número grande abajo. Sin íconos decorativos.</summary>
        private TarjetaPanel TarjetaKpi(string etiqueta, string tag, out Label numero)
        {
            var card = new TarjetaPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 12), Padding = new Padding(16, 12, 16, 12) };
            var lbl = new Label
            {
                Text = etiqueta, Dock = DockStyle.Top, Height = 26, AutoSize = false, Tag = tag,
                Font = Estilo.Bold(8f), ForeColor = Estilo.Gris700, BackColor = Color.White
            };
            numero = new Label
            {
                Text = "—", Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
                Font = Estilo.Titulo(22f), ForeColor = Estilo.AzulOscuro, BackColor = Color.White
            };
            card.Controls.Add(numero);
            card.Controls.Add(lbl);
            return card;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.BackColor = Estilo.Lienzo;
            GestorIdioma.SuscribirObservador(this);
            Traducir();
            Cargar();
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.dashboard", this.Text); I18n.TraducirControles(this); }

        private void Cargar()
        {
            Task.Run(() =>
            {
                int cli = 0, expDisp = 0, expCompl = 0, resHoy = 0, resPend = 0, suscVencer = 0, bkp = 0;
                bool integridadOk = true; DataTable act = null;
                DateTime hoy = DateTime.Today;

                try { cli = _bllCliente.ObtenerTodos().Count; } catch { }
                try { expDisp = _bllExp.ObtenerDisponibles().Count; } catch { }
                try { expCompl = _bllExp.ObtenerTodos().Count(x => x.Estado == BE.EstadoExperiencia.Completa); } catch { }
                try
                {
                    var todas = _bllReserva.ObtenerTodos();
                    resHoy  = todas.Count(r => r.FechaReserva.Date == hoy);
                    resPend = todas.Count(r => r.Estado == BE.EstadoReserva.Pendiente);
                }
                catch { }
                try { suscVencer = _bllSusc.Analizar().PorVencer; } catch { }
                try { bkp = _reporte.ObtenerDiasSinBackup(); } catch { }
                try { integridadOk = BLL.Configuracion.VerificarIntegridadDV(out BLL.ResultadoIntegridad _); } catch { }
                try { act = _bllBitacora.ObtenerTodosNegocio(); } catch { }

                if (IsDisposed) return;
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        _kpi[0].Text = cli.ToString();
                        _kpi[1].Text = expDisp.ToString();
                        _kpi[2].Text = expCompl.ToString();
                        _kpi[3].Text = resHoy.ToString();
                        _kpi[4].Text = resPend.ToString();
                        _kpi[5].Text = suscVencer.ToString();
                        _kpi[6].Text = bkp < 0 ? "—" : bkp.ToString();

                        _kpi[5].ForeColor = suscVencer > 0 ? Estilo.Naranja : Estilo.AzulOscuro;
                        _kpi[6].ForeColor = bkp > 7 ? Estilo.Coral : Estilo.AzulOscuro;

                        string txtInteg = I18n.T(integridadOk ? "eh.dash.integro" : "eh.dash.revisar",
                                                 integridadOk ? "Íntegro" : "Revisar");
                        _kpi[7].Text = txtInteg;
                        _kpi[7].Font = Estilo.Titulo(16f);
                        _kpi[7].ForeColor = integridadOk ? Estilo.Verde : Estilo.Rojo;

                        if (act != null)
                        {
                            _dgvActividad.DataSource = act;
                            foreach (DataGridViewColumn c in _dgvActividad.Columns)
                                if (c.Name.StartsWith("Id")) c.Visible = false;
                        }
                    }));
                }
                catch { }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { GestorIdioma.DesuscribirObservador(this); } catch { }
            try { if (_timer != null) { _timer.Stop(); _timer.Dispose(); } } catch { }
            base.OnFormClosing(e);
        }
    }
}

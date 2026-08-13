using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// CU-ANA-07 (PdN 8) — Reportes Comerciales por Agente de Reservas.
    /// KPIs + ranking de actividad (reservas, efectivas, canceladas, asistidas, clientes, tasa de
    /// cancelación) por agente. La agregación vive en BLL.AnalisisComercial; la GUI solo presenta.
    /// </summary>
    public class ReportesComercialesForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.AnalisisComercial _bll = new BLL.AnalisisComercial();

        private DataGridView _grid;
        private DateTimePicker _desde, _hasta;
        private Label[] _kpi;

        public ReportesComercialesForm()
        {
            Text = "Reportes Comerciales por Agente"; Size = new Size(1000, 620);
            MinimumSize = new Size(840, 500); BackColor = Estilo.Lienzo;
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(UI.GapXL, 0, UI.GapXL, UI.GapL) };
            cont.Controls.Add(_grid);
            UI.ConectarEstadoVacio(_grid, "No hay actividad comercial registrada en el período seleccionado.", "eh.ana.comercial.vacio");

            _desde = NuevoPicker();
            _hasta = NuevoPicker();
            var btn = UI.Primario("Actualizar", "eh.btn.actualizar", (s, e) => Cargar());
            btn.Margin = new Padding(0, 18, 0, 0);
            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Desde", _desde, 150, "eh.lbl.desde"),
                UI.FiltroCampo("Hasta", _hasta, 150, "eh.lbl.hasta"),
                btn);

            var kpis = UI.TiraKPIs(out _kpi, "Agentes", "Reservas totales", "Tasa cancelación", "Agente más activo");

            var header = UI.Encabezado("Reportes Comerciales por Agente",
                "Actividad de reservas por Agente de Reservas para evaluar desempeño y detectar diferencias.",
                "eh.frm.comercial", "eh.ana.comercial.desc");

            Controls.Add(cont);
            Controls.Add(filtros);
            Controls.Add(kpis);
            Controls.Add(header);
        }

        private static DateTimePicker NuevoPicker() =>
            new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Value = DateTime.Today };

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            Cargar();
            Traducir();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.comercial", this.Text); I18n.TraducirControles(this); }

        private void Cargar()
        {
            try
            {
                DateTime? d = _desde.Checked ? (DateTime?)_desde.Value.Date : null;
                DateTime? h = _hasta.Checked ? (DateTime?)_hasta.Value.Date : null;
                List<BE.ReporteComercialAgente> datos = _bll.PorAgente(d, h);

                _grid.DataSource = datos;
                UI.Columnas(_grid,
                    ("Agente", 26), ("Reservas", 12), ("Efectivas", 12), ("Canceladas", 12),
                    ("Asistidas", 12), ("ClientesAtendidos", 14), ("TasaCancelacionTexto", 14));
                Encabezados();
                ActualizarKPIs(datos);
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Encabezados()
        {
            void H(string col, string txt) { if (_grid.Columns.Contains(col)) _grid.Columns[col].HeaderText = txt; }
            H("Agente", "Agente de Reservas"); H("Reservas", "Reservas"); H("Efectivas", "Efectivas");
            H("Canceladas", "Cancel."); H("Asistidas", "Asistidas");
            H("ClientesAtendidos", "Clientes"); H("TasaCancelacionTexto", "Tasa cancel.");
            foreach (var c in new[] { "Reservas", "Efectivas", "Canceladas", "Asistidas", "ClientesAtendidos", "TasaCancelacionTexto" })
                if (_grid.Columns.Contains(c)) _grid.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        private void ActualizarKPIs(List<BE.ReporteComercialAgente> datos)
        {
            if (_kpi == null) return;
            int totalRes = datos.Sum(a => a.Reservas);
            int totalCan = datos.Sum(a => a.Canceladas);
            _kpi[0].Text = datos.Count.ToString();
            _kpi[1].Text = totalRes.ToString();
            _kpi[2].Text = (totalRes > 0 ? (double)totalCan / totalRes * 100 : 0).ToString("0") + "%";
            var top = datos.OrderByDescending(a => a.Reservas).FirstOrDefault();
            _kpi[3].Text = top != null && top.Reservas > 0 ? top.Agente : "—";
        }
    }
}

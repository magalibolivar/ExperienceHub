using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// CU-ANA-03 — Análisis de popularidad y ocupación de experiencias.
    /// Muestra KPIs + un ranking curado (reservas, % ocupación, espera, cancelaciones, calificación,
    /// segmento). Toda la agregación vive en BLL.AnalisisExperiencias; la GUI solo presenta.
    /// </summary>
    public class AnalisisExperienciasForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.AnalisisExperiencias _bll = new BLL.AnalisisExperiencias();

        private DataGridView _grid;
        private DateTimePicker _desde, _hasta;
        private Label[] _kpi;

        public AnalisisExperienciasForm()
        {
            Text = "Análisis de Experiencias"; Size = new Size(1040, 640);
            MinimumSize = new Size(860, 520); BackColor = Estilo.Lienzo;
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(UI.GapXL, 0, UI.GapXL, UI.GapL) };
            cont.Controls.Add(_grid);
            UI.ConectarEstadoVacio(_grid, "No hay experiencias para el período seleccionado.", "eh.ana.exp.vacio");

            _desde = NuevoPicker();
            _hasta = NuevoPicker();
            var btn = UI.Primario("Actualizar", "eh.btn.actualizar", (s, e) => Cargar());
            btn.Margin = new Padding(0, 18, 0, 0);
            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Desde", _desde, 150, "eh.lbl.desde"),
                UI.FiltroCampo("Hasta", _hasta, 150, "eh.lbl.hasta"),
                btn);

            var kpis = UI.TiraKPIs(out _kpi,
                "Experiencias", "Ocupación promedio", "Más solicitada", "Baja demanda");

            var header = UI.Encabezado("Análisis de Experiencias",
                "Popularidad, ocupación y demanda de cada experiencia para decidir qué promover, mantener o revisar.",
                "eh.frm.analisisexp", "eh.ana.exp.desc");

            Controls.Add(cont);
            Controls.Add(filtros);
            Controls.Add(kpis);
            Controls.Add(header);
        }

        private static DateTimePicker NuevoPicker()
        {
            return new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Value = DateTime.Today };
        }

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
        private void Traducir() { this.Text = I18n.T("eh.frm.analisisexp", this.Text); I18n.TraducirControles(this); }

        private void Cargar()
        {
            try
            {
                DateTime? d = _desde.Checked ? (DateTime?)_desde.Value.Date : null;
                DateTime? h = _hasta.Checked ? (DateTime?)_hasta.Value.Date : null;
                List<BE.AnalisisExperiencia> datos = _bll.Ranking(d, h);

                _grid.DataSource = datos;
                UI.Columnas(_grid,
                    ("Experiencia", 24), ("Categoria", 15), ("Ciudad", 13),
                    ("Reservas", 9), ("OcupacionTexto", 11), ("EnEspera", 10),
                    ("Canceladas", 9), ("CalificacionTexto", 10), ("SegmentoCorto", 11));
                Encabezados();
                ActualizarKPIs(datos);
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Encabezados()
        {
            void H(string col, string txt) { if (_grid.Columns.Contains(col)) _grid.Columns[col].HeaderText = txt; }
            H("Experiencia", "Experiencia");     H("Categoria", "Categoría");   H("Ciudad", "Ciudad");
            H("Reservas", "Reservas");           H("OcupacionTexto", "Ocup. %");
            H("EnEspera", "En espera");          H("Canceladas", "Cancel.");
            H("CalificacionTexto", "Calif.");    H("SegmentoCorto", "Demanda");
            if (_grid.Columns.Contains("Reservas"))   _grid.Columns["Reservas"].DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleCenter;
            if (_grid.Columns.Contains("EnEspera"))   _grid.Columns["EnEspera"].DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleCenter;
            if (_grid.Columns.Contains("Canceladas")) _grid.Columns["Canceladas"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (_grid.Columns.Contains("OcupacionTexto")) _grid.Columns["OcupacionTexto"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        private void ActualizarKPIs(List<BE.AnalisisExperiencia> datos)
        {
            if (_kpi == null) return;
            _kpi[0].Text = datos.Count.ToString();
            double ocupProm = datos.Count > 0 ? datos.Average(a => a.Ocupacion) : 0;
            _kpi[1].Text = (ocupProm * 100).ToString("0") + "%";
            var top = datos.OrderByDescending(a => a.Reservas).FirstOrDefault();
            _kpi[2].Text = top != null && top.Reservas > 0 ? top.Experiencia : "—";
            _kpi[3].Text = datos.Count(a => a.Segmento == "Baja demanda").ToString();
        }
    }
}

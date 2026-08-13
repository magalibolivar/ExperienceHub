using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// CU-ANA-05 — Análisis de cancelaciones. Tasa de cancelación por experiencia y por categoría, y
    /// conteo por motivo. Vista conmutable por combo. La agregación vive en BLL.AnalisisCancelaciones.
    /// </summary>
    public class AnalisisCancelacionesForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.AnalisisCancelaciones _bll = new BLL.AnalisisCancelaciones();

        private DataGridView _grid;
        private DateTimePicker _desde, _hasta;
        private ComboBox _vista;
        private Label[] _kpi;
        private BE.ResumenCancelaciones _ultimo;

        public AnalisisCancelacionesForm()
        {
            Text = "Análisis de Cancelaciones"; Size = new Size(980, 640);
            MinimumSize = new Size(820, 520); BackColor = Estilo.Lienzo;
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(UI.GapXL, 0, UI.GapXL, UI.GapL) };
            cont.Controls.Add(_grid);
            UI.ConectarEstadoVacio(_grid, "No se registraron cancelaciones en el período seleccionado.", "eh.ana.cancel.vacio");

            _desde = NuevoPicker();
            _hasta = NuevoPicker();
            _vista = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _vista.Items.AddRange(new object[] { "Por experiencia", "Por categoría", "Por motivo" });
            _vista.SelectedIndex = 0;
            _vista.SelectedIndexChanged += (s, e) => Mostrar();
            var btn = UI.Primario("Actualizar", "eh.btn.actualizar", (s, e) => Cargar());
            btn.Margin = new Padding(0, 18, 0, 0);
            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Desde", _desde, 140, "eh.lbl.desde"),
                UI.FiltroCampo("Hasta", _hasta, 140, "eh.lbl.hasta"),
                UI.FiltroCampo("Ver", _vista, 170, "eh.lbl.ver"),
                btn);

            var kpis = UI.TiraKPIs(out _kpi, "Reservas", "Canceladas", "Tasa global", "Motivos distintos");

            var header = UI.Encabezado("Análisis de Cancelaciones",
                "Tasa de cancelación por experiencia y categoría, y motivos más frecuentes, para detectar problemas.",
                "eh.frm.cancelaciones", "eh.ana.cancel.desc");

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
        private void Traducir() { this.Text = I18n.T("eh.frm.cancelaciones", this.Text); I18n.TraducirControles(this); }

        private void Cargar()
        {
            try
            {
                DateTime? d = _desde.Checked ? (DateTime?)_desde.Value.Date : null;
                DateTime? h = _hasta.Checked ? (DateTime?)_hasta.Value.Date : null;
                _ultimo = _bll.Analizar(d, h);

                if (_kpi != null)
                {
                    _kpi[0].Text = _ultimo.TotalReservas.ToString();
                    _kpi[1].Text = _ultimo.TotalCanceladas.ToString();
                    _kpi[2].Text = (_ultimo.TasaGlobal * 100).ToString("0") + "%";
                    _kpi[3].Text = _ultimo.PorMotivo.Count.ToString();
                }
                Mostrar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Mostrar()
        {
            if (_ultimo == null) return;
            bool porMotivo = _vista.SelectedIndex == 2;
            List<BE.FilaCancelacion> datos =
                _vista.SelectedIndex == 0 ? _ultimo.PorExperiencia :
                _vista.SelectedIndex == 1 ? _ultimo.PorCategoria   :
                                            _ultimo.PorMotivo;

            _grid.DataSource = datos;
            if (porMotivo)
                UI.Columnas(_grid, ("Clave", 70), ("Canceladas", 30));
            else
                UI.Columnas(_grid, ("Clave", 52), ("Total", 16), ("Canceladas", 16), ("TasaTexto", 16));
            Encabezados(porMotivo);
        }

        private void Encabezados(bool porMotivo)
        {
            void H(string col, string txt) { if (_grid.Columns.Contains(col)) _grid.Columns[col].HeaderText = txt; }
            H("Clave", porMotivo ? "Motivo" : (_vista.SelectedIndex == 1 ? "Categoría" : "Experiencia"));
            H("Total", "Reservas"); H("Canceladas", "Cancel."); H("TasaTexto", "Tasa");
            foreach (var c in new[] { "Total", "Canceladas", "TasaTexto" })
                if (_grid.Columns.Contains(c)) _grid.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// CU-ANA-06 — Análisis del comportamiento de suscripciones. Estado de la base (activas/suspendidas/
    /// vencidas/por vencer) y consumo promedio del saldo, global y por plan. Agregación en
    /// BLL.AnalisisSuscripciones.
    /// </summary>
    public class AnalisisSuscripcionesForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.AnalisisSuscripciones _bll = new BLL.AnalisisSuscripciones();

        private DataGridView _grid;
        private NumericUpDown _diasVence;
        private Label[] _kpi;

        public AnalisisSuscripcionesForm()
        {
            Text = "Análisis de Suscripciones"; Size = new Size(980, 620);
            MinimumSize = new Size(820, 500); BackColor = Estilo.Lienzo;
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(UI.GapXL, 0, UI.GapXL, UI.GapL) };
            cont.Controls.Add(_grid);
            UI.ConectarEstadoVacio(_grid, "Todavía no hay suscripciones para analizar.", "eh.ana.susc.vacio");

            _diasVence = new NumericUpDown { Minimum = 0, Maximum = 365, Value = BLL.AnalisisSuscripciones.DIAS_POR_VENCER };
            var btn = UI.Primario("Actualizar", "eh.btn.actualizar", (s, e) => Cargar());
            btn.Margin = new Padding(0, 18, 0, 0);
            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Vence en ≤ (días)", _diasVence, 150, "eh.lbl.venceen"),
                btn);

            var kpis = UI.TiraKPIs(out _kpi, "Suscripciones", "Activas", "Por vencer", "Consumo prom.");

            var header = UI.Encabezado("Análisis de Suscripciones",
                "Estado y consumo de las suscripciones por plan, para detectar oportunidades de fidelización.",
                "eh.frm.analisissusc", "eh.ana.susc.desc");

            Controls.Add(cont);
            Controls.Add(filtros);
            Controls.Add(kpis);
            Controls.Add(header);
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
        private void Traducir() { this.Text = I18n.T("eh.frm.analisissusc", this.Text); I18n.TraducirControles(this); }

        private void Cargar()
        {
            try
            {
                BE.ResumenSuscripciones r = _bll.Analizar((int)_diasVence.Value);

                if (_kpi != null)
                {
                    _kpi[0].Text = r.Total.ToString();
                    _kpi[1].Text = r.Activas.ToString();
                    _kpi[2].Text = r.PorVencer.ToString();
                    _kpi[3].Text = (r.ConsumoPromedio * 100).ToString("0") + "%";
                }

                _grid.DataSource = r.PorPlan;
                UI.Columnas(_grid,
                    ("Plan", 26), ("Total", 11), ("Activas", 11), ("Suspendidas", 13),
                    ("Vencidas", 11), ("PorVencer", 12), ("ConsumoTexto", 16));
                Encabezados();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Encabezados()
        {
            void H(string col, string txt) { if (_grid.Columns.Contains(col)) _grid.Columns[col].HeaderText = txt; }
            H("Plan", "Plan"); H("Total", "Total"); H("Activas", "Activas");
            H("Suspendidas", "Suspend."); H("Vencidas", "Vencidas");
            H("PorVencer", "Por vencer"); H("ConsumoTexto", "Consumo prom.");
            foreach (var c in new[] { "Total", "Activas", "Suspendidas", "Vencidas", "PorVencer", "ConsumoTexto" })
                if (_grid.Columns.Contains(c)) _grid.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
    }
}

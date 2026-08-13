using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// CU-ANA-04 — Detección de clientes en riesgo de abandono.
    /// Regla explícita configurable (días sin reservar + suscripción vencida/por vencer). Muestra KPIs
    /// por nivel + un listado priorizado con los motivos. La regla vive en BLL.AnalisisAbandono.
    /// </summary>
    public class ClientesEnRiesgoForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.AnalisisAbandono _bll = new BLL.AnalisisAbandono();

        private DataGridView _grid;
        private NumericUpDown _diasSin, _diasVence;
        private Label[] _kpi;

        public ClientesEnRiesgoForm()
        {
            Text = "Clientes en Riesgo de Abandono"; Size = new Size(1040, 640);
            MinimumSize = new Size(860, 520); BackColor = Estilo.Lienzo;
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(UI.GapXL, 0, UI.GapXL, UI.GapL) };
            cont.Controls.Add(_grid);
            UI.ConectarEstadoVacio(_grid, "No hay clientes en riesgo con los criterios actuales. ¡Buena señal!", "eh.ana.riesgo.vacio");

            _diasSin   = new NumericUpDown { Minimum = 1, Maximum = 3650, Value = BLL.AnalisisAbandono.DIAS_SIN_RESERVA };
            _diasVence = new NumericUpDown { Minimum = 0, Maximum = 365,  Value = BLL.AnalisisAbandono.DIAS_POR_VENCER };
            var btn = UI.Primario("Actualizar", "eh.btn.actualizar", (s, e) => Cargar());
            btn.Margin = new Padding(0, 18, 0, 0);
            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Días sin reservar ≥", _diasSin, 150, "eh.lbl.diassinreservar"),
                UI.FiltroCampo("Vence en ≤ (días)", _diasVence, 150, "eh.lbl.venceen"),
                btn);

            var kpis = UI.TiraKPIs(out _kpi, "En riesgo", "Riesgo alto", "Riesgo medio", "Riesgo bajo");

            var header = UI.Encabezado("Clientes en Riesgo de Abandono",
                "Clientes sin actividad reciente y con suscripción vencida o por vencer, para accionar retención.",
                "eh.frm.riesgo", "eh.ana.riesgo.desc");

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
        private void Traducir() { this.Text = I18n.T("eh.frm.riesgo", this.Text); I18n.TraducirControles(this); }

        private void Cargar()
        {
            try
            {
                List<BE.ClienteEnRiesgo> datos = _bll.Detectar((int)_diasSin.Value, (int)_diasVence.Value);
                _grid.DataSource = datos;
                UI.Columnas(_grid,
                    ("Cliente", 20), ("Plan", 14), ("UltimaReservaTexto", 12),
                    ("DiasSinReservarTexto", 12), ("Suscripcion", 14), ("Nivel", 10), ("MotivosTexto", 28));
                Encabezados();
                ColorearNivel();
                ActualizarKPIs(datos);
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Encabezados()
        {
            void H(string col, string txt) { if (_grid.Columns.Contains(col)) _grid.Columns[col].HeaderText = txt; }
            H("Cliente", "Cliente");                     H("Plan", "Plan");
            H("UltimaReservaTexto", "Última reserva");   H("DiasSinReservarTexto", "Días s/ reservar");
            H("Suscripcion", "Suscripción");             H("Nivel", "Riesgo");
            H("MotivosTexto", "Motivos");
            if (_grid.Columns.Contains("DiasSinReservarTexto")) _grid.Columns["DiasSinReservarTexto"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (_grid.Columns.Contains("Nivel")) _grid.Columns["Nivel"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        // Colorea la celda de nivel con la paleta (sin rojo/verde crudos: coral/naranja/gris).
        private void ColorearNivel()
        {
            if (!_grid.Columns.Contains("Nivel")) return;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                string n = row.Cells["Nivel"].Value?.ToString();
                Color c = n == "Alto" ? Estilo.Coral : n == "Medio" ? Estilo.Naranja : Estilo.Gris400;
                row.Cells["Nivel"].Style.ForeColor = c;
                row.Cells["Nivel"].Style.Font = Estilo.SemiBold(9f);
            }
        }

        private void ActualizarKPIs(List<BE.ClienteEnRiesgo> datos)
        {
            if (_kpi == null) return;
            _kpi[0].Text = datos.Count.ToString();
            _kpi[1].Text = datos.Count(x => x.Nivel == "Alto").ToString();
            _kpi[2].Text = datos.Count(x => x.Nivel == "Medio").ToString();
            _kpi[3].Text = datos.Count(x => x.Nivel == "Bajo").ToString();
        }
    }
}

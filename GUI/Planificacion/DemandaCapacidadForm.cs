using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 · B — Demanda y capacidad. Analiza ocupación, demanda insatisfecha y disponibilidad. Usa
    /// navegación secundaria (Ocupación | Lista de espera | Detalle de capacidad): se muestra UNA
    /// visualización por vista, aprovechando todo el ancho y alto. Las barras horizontales reservan
    /// espacio real para los nombres y permiten recorrer TODAS las experiencias por scroll.
    /// </summary>
    internal sealed class DemandaCapacidadForm : PlanificacionFormBase
    {
        private Label _kOcup, _kEspera, _kVencidas;
        private GraficoControl _gOcup, _gEspera;
        private DataGridView _gridCap;
        private Label[] _chips;
        private Control _cardOcup, _cardEspera, _cardCap;
        private int _sub;

        public DemandaCapacidadForm() { Inicializar(); }

        protected override string TituloKey => "eh.pln.tab.demanda";
        protected override string TituloDef => "Demanda y capacidad";
        protected override string SubKey => "eh.pln.demanda.sub";
        protected override string SubDef => "Identificá experiencias con exceso o baja demanda y su disponibilidad.";
        protected override string ContextoAyuda => "pn03.demanda";

        protected override void ConstruirCuerpo(Panel host)
        {
            var kpis = KpiRow(out var k,
                ("eh.pln.kpi.ocupprom", "Ocupación promedio"),
                ("eh.pln.kpi.espera", "En lista de espera"),
                ("eh.pln.kpi.vencidas", "Ofertas vencidas"));
            _kOcup = k[0]; _kEspera = k[1]; _kVencidas = k[2];

            var nav = NavSecundaria(Seleccionar, out _chips,
                ("eh.pln.subnav.ocupacion", "Ocupación"),
                ("eh.pln.subnav.espera", "Lista de espera"),
                ("eh.pln.subnav.capacidad", "Detalle de capacidad"));

            var contenido = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(0, UI.Gap, 0, 0) };

            _gOcup = new GraficoControl { Tipo = GraficoControl.TipoGrafico.BarrasHorizontal, MaxForzado = 100 };
            _gEspera = new GraficoControl { Tipo = GraficoControl.TipoGrafico.BarrasHorizontal };
            AplicarTextosGraficos();
            _cardOcup = TarjetaDeGrafico(_gOcup, conScroll: true);
            _cardEspera = TarjetaDeGrafico(_gEspera, conScroll: true);

            _cardCap = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White, Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.Gap), Margin = new Padding(0, 0, UI.GapL, UI.GapL) };
            var tCap = new Label { Dock = DockStyle.Top, Height = 24, Text = I18n.T("eh.pln.sec.capacidad", "Capacidad y disponibilidad"), Tag = "eh.pln.sec.capacidad", Font = Estilo.SemiBold(11f), ForeColor = Estilo.AzulOscuro };
            _gridCap = UI.Grilla();
            _gridCap.AutoGenerateColumns = true;
            ((TarjetaPanel)_cardCap).Controls.Add(_gridCap);
            ((TarjetaPanel)_cardCap).Controls.Add(tCap);
            UI.ConectarEstadoVacio(_gridCap, I18n.T("eh.pln.vacio.capacidad", "Aplicá un período para ver la capacidad por experiencia."), "eh.pln.vacio.capacidad");

            contenido.Controls.Add(_cardOcup);
            contenido.Controls.Add(_cardEspera);
            contenido.Controls.Add(_cardCap);

            host.Controls.Add(contenido);
            host.Controls.Add(nav);
            host.Controls.Add(kpis);

            Seleccionar(0);
        }

        private void AplicarTextosGraficos()
        {
            _gOcup.Titulo = I18n.T("eh.pln.g.ocup", "Ocupación por experiencia");
            _gOcup.Subtitulo = I18n.T("eh.pln.g.ocup.sub", "Ocupación = reservados / cupo máximo");
            _gEspera.Titulo = I18n.T("eh.pln.g.espera", "Lista de espera por experiencia");
            _gEspera.Subtitulo = I18n.T("eh.pln.g.espera.sub", "Clientes esperando un lugar");
        }

        private void Seleccionar(int idx)
        {
            _sub = idx;
            PintarChips(_chips, idx);
            if (_cardOcup != null) _cardOcup.Visible = idx == 0;
            if (_cardEspera != null) _cardEspera.Visible = idx == 1;
            if (_cardCap != null) _cardCap.Visible = idx == 2;
        }

        protected override void Repintar()
        {
            var t = Tablero();
            _kOcup.Text = OcupacionPromedio(t).ToString("0.0") + "%";
            _kEspera.Text = t.EnEsperaTotal.ToString();
            _kVencidas.Text = t.OfertasVencidas.ToString();

            _gOcup.SetDatos(t.Experiencias.Where(x => x.CupoMaximo > 0).OrderBy(x => x.Ocupacion)
                .Select((x, i) => new GraficoControl.Punto(x.Nombre, (double)x.Ocupacion, GraficoControl.ColorPorIndice(i), "%")));
            _gEspera.SetDatos(t.Experiencias.Where(x => x.EnEspera > 0).OrderByDescending(x => x.EnEspera)
                .Select((x, i) => new GraficoControl.Punto(x.Nombre, x.EnEspera, GraficoControl.ColorPorIndice(i))));

            _gridCap.DataSource = null;
            _gridCap.DataSource = t.Experiencias.OrderBy(x => x.Ocupacion).ToList();
            UI.Columnas(_gridCap, ("Nombre", 34), ("LugaresReservados", 16), ("CupoMaximo", 14), ("EnEspera", 14), ("Ocupacion", 16));
            SetHeader(_gridCap, "Nombre", I18n.T("eh.pln.col.experiencia", "Experiencia"));
            SetHeader(_gridCap, "LugaresReservados", I18n.T("eh.pln.col.reservados", "Reservados"));
            SetHeader(_gridCap, "CupoMaximo", I18n.T("eh.pln.col.cupo", "Cupo máx."));
            SetHeader(_gridCap, "EnEspera", I18n.T("eh.pln.col.espera", "En espera"));
            SetHeader(_gridCap, "Ocupacion", I18n.T("eh.pln.col.ocupacion", "Ocupación %"));
        }

        private static decimal OcupacionPromedio(BE.TableroPeriodo t)
        {
            var conCupo = t.Experiencias.Where(x => x.CupoMaximo > 0).ToList();
            return conCupo.Count == 0 ? 0 : Math.Round(conCupo.Average(x => x.Ocupacion), 1);
        }

        private static void SetHeader(DataGridView g, string prop, string texto)
        {
            if (g.Columns.Contains(prop)) g.Columns[prop].HeaderText = texto;
        }

        protected override void TraducirExtra()
        {
            AplicarTextosGraficos();
            if (Puede(BE.Patentes.Planificacion)) Try(Repintar);
        }
    }
}

using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 · C — Desempeño y calidad. Separa calidad (satisfacción por organizador), comportamiento
    /// (inasistencia por organizador) y utilización (uso de planes), cada una en su propia vista con un
    /// gráfico amplio. Barras horizontales: nombres de organizador legibles y scroll para verlos todos.
    /// </summary>
    internal sealed class DesempenoCalidadForm : PlanificacionFormBase
    {
        private Label _kCalif, _kInasist, _kTardias;
        private GraficoControl _gCalif, _gInasist, _gPlanes;
        private Label[] _chips;
        private Control _cardCalif, _cardInasist, _cardPlanes;

        public DesempenoCalidadForm() { Inicializar(); }

        protected override string TituloKey => "eh.pln.tab.desempeno";
        protected override string TituloDef => "Desempeño y calidad";
        protected override string SubKey => "eh.pln.desempeno.sub";
        protected override string SubDef => "Calidad, asistencia y utilización para decidir sobre organizadores y planes.";
        protected override string ContextoAyuda => "pn03.desempeno";

        protected override void ConstruirCuerpo(Panel host)
        {
            var kpis = KpiRow(out var k,
                ("eh.pln.kpi.calif", "Calificación promedio"),
                ("eh.pln.kpi.inasistpct", "% inasistencia"),
                ("eh.pln.kpi.canctardias", "Cancelaciones tardías"));
            _kCalif = k[0]; _kInasist = k[1]; _kTardias = k[2];

            var nav = NavSecundaria(Seleccionar, out _chips,
                ("eh.pln.subnav.satisfaccion", "Satisfacción"),
                ("eh.pln.subnav.asistencia", "Asistencia"),
                ("eh.pln.subnav.utilizacion", "Utilización de planes"));

            var contenido = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(0, UI.Gap, 0, 0) };

            _gCalif = new GraficoControl { Tipo = GraficoControl.TipoGrafico.BarrasHorizontal, MaxForzado = 5 };
            _gInasist = new GraficoControl { Tipo = GraficoControl.TipoGrafico.BarrasHorizontal, MaxForzado = 100 };
            _gPlanes = new GraficoControl { Tipo = GraficoControl.TipoGrafico.BarrasHorizontal, MaxForzado = 100 };
            AplicarTextosGraficos();
            _cardCalif = TarjetaDeGrafico(_gCalif, conScroll: true);
            _cardInasist = TarjetaDeGrafico(_gInasist, conScroll: true);
            _cardPlanes = TarjetaDeGrafico(_gPlanes, conScroll: true);

            contenido.Controls.Add(_cardCalif);
            contenido.Controls.Add(_cardInasist);
            contenido.Controls.Add(_cardPlanes);

            host.Controls.Add(contenido);
            host.Controls.Add(nav);
            host.Controls.Add(kpis);

            Seleccionar(0);
        }

        private void AplicarTextosGraficos()
        {
            _gCalif.Titulo = I18n.T("eh.pln.g.calif", "Calificación por organizador");
            _gCalif.Subtitulo = I18n.T("eh.pln.g.calif.sub", "Promedio de 1 a 5");
            _gInasist.Titulo = I18n.T("eh.pln.g.inasist", "% de inasistencia por organizador");
            _gInasist.Subtitulo = I18n.T("eh.pln.g.inasist.sub", "Inasistencias / (asistencias + inasistencias)");
            _gPlanes.Titulo = I18n.T("eh.pln.g.planes", "Uso de planes");
            _gPlanes.Subtitulo = I18n.T("eh.pln.g.planes.sub", "Reservas usadas / permitidas");
        }

        private void Seleccionar(int idx)
        {
            PintarChips(_chips, idx);
            if (_cardCalif != null) _cardCalif.Visible = idx == 0;
            if (_cardInasist != null) _cardInasist.Visible = idx == 1;
            if (_cardPlanes != null) _cardPlanes.Visible = idx == 2;
        }

        protected override void Repintar()
        {
            var t = Tablero();
            _kCalif.Text = CalifProm(t).ToString("0.0");
            _kInasist.Text = InasistGeneral(t).ToString("0.0") + "%";
            _kTardias.Text = t.CancelacionesTardias.ToString();

            _gCalif.SetDatos(t.Organizadores.Where(o => o.Calificaciones > 0).OrderBy(o => o.CalificacionPromedio)
                .Select((o, i) => new GraficoControl.Punto(o.Nombre, (double)o.CalificacionPromedio, GraficoControl.ColorPorIndice(i))));
            _gInasist.SetDatos(t.Organizadores.Where(o => (o.Asistencias + o.Inasistencias) > 0).OrderByDescending(o => o.PctInasistencia)
                .Select((o, i) => new GraficoControl.Punto(o.Nombre, (double)o.PctInasistencia, GraficoControl.ColorPorIndice(i), "%")));
            _gPlanes.SetDatos(t.Planes.OrderByDescending(p => p.PctUso)
                .Select((p, i) => new GraficoControl.Punto(p.Nombre, (double)p.PctUso, GraficoControl.ColorPorIndice(i), "%")));
        }

        private static decimal CalifProm(BE.TableroPeriodo t)
        {
            var c = t.Organizadores.Where(o => o.Calificaciones > 0).ToList();
            return c.Count == 0 ? 0 : Math.Round(c.Average(o => o.CalificacionPromedio), 1);
        }

        private static decimal InasistGeneral(BE.TableroPeriodo t)
        {
            int a = t.Organizadores.Sum(o => o.Asistencias), i = t.Organizadores.Sum(o => o.Inasistencias);
            return (a + i) > 0 ? Math.Round(100m * i / (a + i), 1) : 0;
        }

        protected override void TraducirExtra()
        {
            AplicarTextosGraficos();
            if (Puede(BE.Patentes.Planificacion)) Try(Repintar);
        }
    }
}

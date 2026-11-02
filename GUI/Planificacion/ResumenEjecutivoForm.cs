using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 · A — Resumen ejecutivo. Visión general del período: KPIs principales, evolución mensual de
    /// reservas (líneas) y distribución por estado (torta), con una lectura automática del período. Dos
    /// gráficos amplios (sin comprimir). Aloja además las acciones de VISTA (Exportar PDF del tablero,
    /// Guardar/Abrir vista de análisis — serialización A03), que operan sobre el período y filtros vigentes.
    /// </summary>
    internal sealed class ResumenEjecutivoForm : PlanificacionFormBase
    {
        private const string MODULO = "Planificación";
        private Label _kIngresos, _kAltas, _kEspera, _kInasist;
        private GraficoControl _gEvolucion, _gEstados;
        private Label _lblInterpret;

        public ResumenEjecutivoForm() { Inicializar(); }

        protected override string TituloKey => "eh.pln.tab.resumen";
        protected override string TituloDef => "Resumen ejecutivo";
        protected override string SubKey => "eh.pln.resumen.sub";
        protected override string SubDef => "Visión general del período: qué pasó con la oferta y la demanda.";
        protected override string ContextoAyuda => "pn03.resumen";

        protected override Control[] AccionesSecundarias() => new Control[]
        {
            UI.Secundario(I18n.T("eh.btn.abrirvista", "Abrir vista"), "eh.btn.abrirvista", (s, e) => Try(AbrirVista)),
            UI.Secundario(I18n.T("eh.btn.guardarvista", "Guardar vista"), "eh.btn.guardarvista", (s, e) => Try(GuardarVista)),
            UI.Secundario(I18n.T("eh.btn.exportarpdf", "Exportar PDF"), "eh.btn.exportarpdf", (s, e) => Try(ExportarPdf)),
        };

        protected override void ConstruirCuerpo(Panel host)
        {
            var kpis = KpiRow(out var k,
                ("eh.pln.kpi.ingresos", "Ingresos por planes"),
                ("eh.pln.kpi.altas", "Altas de suscripción"),
                ("eh.pln.kpi.espera", "En lista de espera"),
                ("eh.pln.kpi.inasist", "Inasistencias"));
            _kIngresos = k[0]; _kAltas = k[1]; _kEspera = k[2]; _kInasist = k[3];

            _gEvolucion = new GraficoControl { Tipo = GraficoControl.TipoGrafico.Lineas };
            _gEstados = new GraficoControl { Tipo = GraficoControl.TipoGrafico.Torta };
            AplicarTextosGraficos();

            var interp = new TarjetaPanel { Dock = DockStyle.Bottom, Height = 96, Radio = 10, BackColor = Color.White, Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.Gap), Margin = new Padding(0) };
            var tInterp = new Label { Dock = DockStyle.Top, Height = 22, Text = I18n.T("eh.pln.sec.interpret", "Lectura del período"), Tag = "eh.pln.sec.interpret", Font = Estilo.SemiBold(10.5f), ForeColor = Estilo.AzulOscuro };
            _lblInterpret = new Label { Dock = DockStyle.Fill, Text = "", Font = Estilo.Normal(9.5f), ForeColor = Estilo.Gris700 };
            interp.Controls.Add(_lblInterpret);
            interp.Controls.Add(tInterp);

            var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Estilo.Lienzo };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            tl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tl.Controls.Add(TarjetaDeGrafico(_gEvolucion, conScroll: true), 0, 0);
            tl.Controls.Add(TarjetaDeGrafico(_gEstados, conScroll: false), 1, 0);

            host.Controls.Add(tl);
            host.Controls.Add(interp);
            host.Controls.Add(kpis);
        }

        private void AplicarTextosGraficos()
        {
            _gEvolucion.Titulo = I18n.T("eh.pln.g.evolucion", "Evolución mensual de reservas");
            _gEvolucion.Subtitulo = I18n.T("eh.pln.g.evolucion.sub", "Reservas efectivas por mes (confirmadas + asistió + no asistió)");
            _gEstados.Titulo = I18n.T("eh.pln.g.estados", "Distribución de reservas por estado");
            _gEstados.Subtitulo = I18n.T("eh.pln.g.estados.sub", "Sobre el total de reservas del período");
        }

        protected override void Repintar()
        {
            var t = Tablero();
            _kIngresos.Text = "$ " + t.IngresosPlanes.ToString("0");
            _kAltas.Text = t.Altas.ToString();
            _kEspera.Text = t.EnEsperaTotal.ToString();
            _kInasist.Text = t.Inasistencias.ToString();

            _gEvolucion.SetDatos(t.ReservasMensuales
                .Select(m => new GraficoControl.Punto($"{m.Mes:00}/{m.Anio % 100:00}", m.Reservas, Estilo.AzulDeSeleccion)));
            _gEstados.SetDatos(t.ReservasPorEstado
                .Select(x => new GraficoControl.Punto(PlanificacionTextos.EstadoReserva(x.Estado), x.Cantidad, PlanificacionTextos.ColorEstadoReserva(x.Estado))));

            _lblInterpret.Text = Interpretar(t);
        }

        private static string Interpretar(BE.TableroPeriodo t)
        {
            var partes = new List<string>();
            int totalRes = t.ReservasPorEstado.Sum(x => x.Cantidad);
            partes.Add(string.Format(I18n.T("eh.pln.interp.reservas", "Reservas del período: {0}."), totalRes));
            if (t.EnEsperaTotal > 0)
                partes.Add(string.Format(I18n.T("eh.pln.interp.espera", "{0} cliente(s) en lista de espera (demanda insatisfecha)."), t.EnEsperaTotal));
            int bajas = t.Experiencias.Count(x => x.CupoMaximo > 0 && x.Ocupacion < 40m);
            if (bajas > 0)
                partes.Add(string.Format(I18n.T("eh.pln.interp.ocupbaja", "{0} experiencia(s) con ocupación por debajo del 40%."), bajas));
            if (totalRes > 0 && t.Inasistencias > 0)
                partes.Add(string.Format(I18n.T("eh.pln.interp.inasist", "Inasistencias: {0} ({1}% de las reservas)."), t.Inasistencias, Math.Round(100m * t.Inasistencias / totalRes, 1)));
            return partes.Count == 0 ? I18n.T("eh.pln.interp.vacio", "Sin datos suficientes en el período seleccionado.") : string.Join("  ", partes);
        }

        protected override void TraducirExtra()
        {
            AplicarTextosGraficos();
            if (Puede(BE.Patentes.Planificacion)) Try(Repintar);
        }

        // ── Acciones de vista ───────────────────────────────────────────────────────
        private void ExportarPdf()
        {
            var t = Tablero();
            var recs = ContextoPlanificacion.ObtenerRecomendaciones(Analitica);
            var doc = Exportacion.InformePlanificacion.DocumentoTablero(t, recs, TextoFiltros());
            new Exportacion.ExportadorPdfArchivo().Exportar(doc, this);
        }

        private string TextoFiltros()
        {
            var partes = new List<string>();
            if (CboCiudad.SelectedItem is ItemFiltro ci && ci.Id.HasValue) partes.Add(ci.ToString());
            if (CboCategoria.SelectedItem is ItemFiltro ca && ca.Id.HasValue) partes.Add(ca.ToString());
            if (CboOrganizador.SelectedItem is ItemFiltro o && o.Id.HasValue) partes.Add(o.ToString());
            return partes.Count == 0 ? I18n.T("eh.pln.sinfiltros", "Sin filtros (todo el catálogo)") : string.Join(" · ", partes);
        }

        private void GuardarVista()
        {
            using (var dlg = new InputDialog(I18n.T("eh.btn.guardarvista", "Guardar vista"), I18n.T("eh.pln.nombrevista", "Nombre de la vista de análisis:"), false))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.InputText)) return;
                var cfg = new BE.ConfiguracionInforme
                {
                    Nombre = dlg.InputText.Trim(),
                    PeriodoDesde = ContextoPlanificacion.Desde,
                    PeriodoHasta = ContextoPlanificacion.Hasta,
                    FiltroCiudad = ContextoPlanificacion.Ciudad,
                    FiltroCategoria = ContextoPlanificacion.Categoria,
                    FiltroOrganizador = ContextoPlanificacion.Organizador,
                    Umbrales = ContextoPlanificacion.UmbralesVista ?? Analitica.CargarUmbrales(),
                    GraficosVisibles = { "evolucion", "estados", "ocupacion", "espera", "calif", "inasist", "planes", "antesdesp" }
                };
                string ruta = Servicios.Serializacion.SerializadorInformes.Guardar(cfg);
                Estilo.Exito(this, I18n.T("eh.btn.guardarvista", "Guardar vista"), string.Format(I18n.T("eh.pln.vistaguardada", "Vista guardada en:\n{0}"), ruta));
            }
        }

        private void AbrirVista()
        {
            var nombres = Servicios.Serializacion.SerializadorInformes.Listar();
            if (nombres.Count == 0)
            {
                Estilo.Info(this, I18n.T("eh.btn.abrirvista", "Abrir vista"), I18n.T("eh.pln.sinvistas", "Todavía no hay vistas de análisis guardadas."));
                return;
            }
            using (var dlg = new InputDialog(I18n.T("eh.btn.abrirvista", "Abrir vista"),
                string.Format(I18n.T("eh.pln.elegirvista", "Escribí el nombre de la vista a abrir:\nDisponibles: {0}"), string.Join(", ", nombres)), false))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.InputText)) return;
                var ruta = System.IO.Path.Combine(Servicios.Serializacion.SerializadorInformes.CarpetaInformes(), dlg.InputText.Trim() + ".xml");
                var cfg = Servicios.Serializacion.SerializadorInformes.Cargar(ruta);
                // Restaura período + filtros + umbrales en el contexto compartido.
                ContextoPlanificacion.Desde = cfg.PeriodoDesde == default(DateTime) ? ContextoPlanificacion.Desde : cfg.PeriodoDesde;
                ContextoPlanificacion.Hasta = cfg.PeriodoHasta == default(DateTime) ? ContextoPlanificacion.Hasta : cfg.PeriodoHasta;
                ContextoPlanificacion.Ciudad = cfg.FiltroCiudad;
                ContextoPlanificacion.Categoria = cfg.FiltroCategoria;
                ContextoPlanificacion.Organizador = cfg.FiltroOrganizador;
                ContextoPlanificacion.UmbralesVista = cfg.Umbrales;
                ContextoPlanificacion.Invalidar();
                DtDesde.Value = cfg.PeriodoDesde == default(DateTime) ? DtDesde.Value : cfg.PeriodoDesde;
                DtHasta.Value = cfg.PeriodoHasta == default(DateTime) ? DtHasta.Value : cfg.PeriodoHasta;
                SeleccionarEnCombo(CboCiudad, cfg.FiltroCiudad);
                SeleccionarEnCombo(CboCategoria, cfg.FiltroCategoria);
                SeleccionarEnCombo(CboOrganizador, cfg.FiltroOrganizador);
                Repintar();
                Estilo.Exito(this, I18n.T("eh.btn.abrirvista", "Abrir vista"), string.Format(I18n.T("eh.pln.vistaabierta", "Vista '{0}' aplicada."), cfg.Nombre));
            }
        }
    }
}

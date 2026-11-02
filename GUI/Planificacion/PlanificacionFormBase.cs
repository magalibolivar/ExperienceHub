using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 — Base común de los formularios ANALÍTICOS de planificación (Resumen, Demanda, Desempeño,
    /// Decisiones). Aporta el encabezado, la barra de filtros compartida (período/ciudad/categoría/
    /// organizador) enlazada a <see cref="ContextoPlanificacion"/>, la carga de combos, el idioma en
    /// caliente y helpers de layout (KPIs, tarjetas de gráfico, navegación secundaria). Cada subclase
    /// arma su cuerpo en <see cref="ConstruirCuerpo"/> y repinta en <see cref="Repintar"/>.
    /// </summary>
    internal abstract class PlanificacionFormBase : FormBase, IIdiomaObserver
    {
        protected readonly BLL.Analitica Analitica = new BLL.Analitica();

        protected DateTimePicker DtDesde, DtHasta;
        protected ComboBox CboCiudad, CboCategoria, CboOrganizador;
        protected Panel Cuerpo;
        /// <summary>Barra de filtros (período/ciudad/categoría/organizador). Pertenece a la GENERACIÓN
        /// de recomendaciones: las subclases con vistas que no la necesitan (p. ej. "Resultados", que
        /// consulta un plan ya creado con su propio período fijo) pueden ocultarla con Visible=false.</summary>
        protected Panel Filtros;
        private FlowLayoutPanel _barraSecundaria;

        protected abstract string TituloKey { get; }
        protected abstract string TituloDef { get; }
        protected abstract string SubKey { get; }
        protected abstract string SubDef { get; }
        /// <summary>Patente mínima para ver el contenido analítico (default: Planificación).</summary>
        protected virtual string PatenteVer => BE.Patentes.Planificacion;
        protected virtual bool MostrarFiltros => true;
        /// <summary>Contexto de ayuda contextual (ver <see cref="Ayuda.AyudaContenido"/>). Null = sin botón "?".</summary>
        protected virtual string ContextoAyuda => null;

        protected abstract void ConstruirCuerpo(Panel host);
        protected abstract void Repintar();
        protected virtual void TraducirExtra() { }
        /// <summary>Controles de la barra secundaria (Exportar/Guardar/Abrir vista). Vacío = sin barra.</summary>
        protected virtual Control[] AccionesSecundarias() => new Control[0];

        protected sealed class ItemFiltro
        {
            public int? Id { get; }
            private readonly string _t;
            public ItemFiltro(int? id, string t) { Id = id; _t = t; }
            public override string ToString() => _t;
        }

        protected void Inicializar()
        {
            Text = I18n.T(TituloKey, TituloDef);
            BackColor = Estilo.Lienzo;
            Size = new Size(1160, 740);
            MinimumSize = new Size(1024, 660);

            var encabezado = UI.Encabezado(I18n.T(TituloKey, TituloDef), I18n.T(SubKey, SubDef), TituloKey, SubKey, ContextoAyuda);
            Cuerpo = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, Padding = new Padding(UI.GapXL, UI.Gap, UI.GapXL, UI.GapL) };

            Controls.Add(Cuerpo);   // Fill primero

            var secundarias = AccionesSecundarias();
            if (secundarias.Length > 0)
            {
                _barraSecundaria = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top, Height = 44, BackColor = Estilo.Lienzo,
                    Padding = new Padding(UI.GapXL, 0, UI.GapXL, UI.Gap), FlowDirection = FlowDirection.RightToLeft, WrapContents = false
                };
                foreach (var c in secundarias) _barraSecundaria.Controls.Add(c);
                Controls.Add(_barraSecundaria);
            }

            if (MostrarFiltros)
            {
                DtDesde = new DateTimePicker { Format = DateTimePickerFormat.Short };
                DtHasta = new DateTimePicker { Format = DateTimePickerFormat.Short };
                CboCiudad = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CboCategoria = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                CboOrganizador = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

                Filtros = UI.BarraFiltros(
                    UI.FiltroCampo(I18n.T("eh.pln.desde", "Desde"), DtDesde, 110, "eh.pln.desde"),
                    UI.FiltroCampo(I18n.T("eh.pln.hasta", "Hasta"), DtHasta, 110, "eh.pln.hasta"),
                    UI.FiltroCampo(I18n.T("eh.pln.ciudad", "Ciudad"), CboCiudad, 150, "eh.pln.ciudad"),
                    UI.FiltroCampo(I18n.T("eh.pln.categoria", "Categoría"), CboCategoria, 150, "eh.pln.categoria"),
                    UI.FiltroCampo(I18n.T("eh.pln.organizador", "Organizador"), CboOrganizador, 170, "eh.pln.organizador"),
                    UI.Primario(I18n.T("eh.btn.aplicar", "Aplicar"), "eh.btn.aplicar", (s, e) => Try(AplicarFiltros)));
                Controls.Add(Filtros);
                CargarCombos();
                SincronizarDesdeContexto();
            }

            Controls.Add(encabezado);   // último → arriba de todo

            ConstruirCuerpo(Cuerpo);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            if (Puede(PatenteVer)) Try(Repintar);
            else MostrarSinPermiso();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected virtual void MostrarSinPermiso()
        {
            Cuerpo.Controls.Clear();
            Cuerpo.Controls.Add(new Label
            {
                Dock = DockStyle.Fill, Text = I18n.T("eh.pln.sinpermiso", "No tenés permiso para ver la analítica del período."),
                Tag = "eh.pln.sinpermiso", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Estilo.Gris400, Font = Estilo.Normal(11f)
            });
        }

        // ── Filtros / contexto ──────────────────────────────────────────────────────
        private void CargarCombos()
        {
            try
            {
                CboCiudad.Items.Add(new ItemFiltro(null, I18n.T("eh.pln.todas", "Todas")));
                foreach (var c in new BLL.Ciudad().ObtenerActivas()) CboCiudad.Items.Add(new ItemFiltro(c.IdCiudad, c.Nombre));
                CboCiudad.SelectedIndex = 0;
                CboCategoria.Items.Add(new ItemFiltro(null, I18n.T("eh.pln.todas", "Todas")));
                foreach (var c in new BLL.Categoria().ObtenerActivas()) CboCategoria.Items.Add(new ItemFiltro(c.IdCategoria, c.Nombre));
                CboCategoria.SelectedIndex = 0;
                CboOrganizador.Items.Add(new ItemFiltro(null, I18n.T("eh.pln.todos", "Todos")));
                foreach (var o in new BLL.Organizador().ObtenerActivos()) CboOrganizador.Items.Add(new ItemFiltro(o.IdOrganizador, o.Nombre));
                CboOrganizador.SelectedIndex = 0;
            }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("[Planificacion.CargarCombos] " + ex.Message); }
        }

        private void SincronizarDesdeContexto()
        {
            DtDesde.Value = Clamp(ContextoPlanificacion.Desde, DtDesde);
            DtHasta.Value = Clamp(ContextoPlanificacion.Hasta, DtHasta);
            SeleccionarEnCombo(CboCiudad, ContextoPlanificacion.Ciudad);
            SeleccionarEnCombo(CboCategoria, ContextoPlanificacion.Categoria);
            SeleccionarEnCombo(CboOrganizador, ContextoPlanificacion.Organizador);
        }

        private static DateTime Clamp(DateTime v, DateTimePicker dtp)
            => v < dtp.MinDate ? dtp.MinDate : (v > dtp.MaxDate ? dtp.MaxDate : v);

        private void AplicarFiltros()
        {
            ContextoPlanificacion.Desde = DtDesde.Value.Date;
            ContextoPlanificacion.Hasta = DtHasta.Value.Date;
            ContextoPlanificacion.Ciudad = Sel(CboCiudad);
            ContextoPlanificacion.Categoria = Sel(CboCategoria);
            ContextoPlanificacion.Organizador = Sel(CboOrganizador);
            ContextoPlanificacion.Invalidar();
            Repintar();
        }

        protected int? Sel(ComboBox cbo) => (cbo?.SelectedItem as ItemFiltro)?.Id;

        protected static void SeleccionarEnCombo(ComboBox cbo, int? id)
        {
            if (cbo == null) return;
            for (int i = 0; i < cbo.Items.Count; i++)
                if (cbo.Items[i] is ItemFiltro it && it.Id == id) { cbo.SelectedIndex = i; return; }
            if (cbo.Items.Count > 0) cbo.SelectedIndex = 0;
        }

        protected BE.TableroPeriodo Tablero() => ContextoPlanificacion.ObtenerTablero(Analitica);

        // ── Permisos ──────────────────────────────────────────────────────────────
        protected static bool Puede(string patente)
        {
            if (!Seguridad.SessionManager.IsLoggedIn) return false;
            var sm = Seguridad.SessionManager.GetInstance();
            return sm.Usuario.EsAdministrador || sm.TienePermiso(patente);
        }

        // ── Helpers de layout reutilizables ─────────────────────────────────────────
        protected FlowLayoutPanel KpiRow(out Label[] valores, params (string Tag, string Def)[] items)
        {
            var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 86, BackColor = Estilo.Lienzo, Padding = new Padding(0, 0, 0, UI.Gap), WrapContents = false, AutoScroll = false };
            valores = new Label[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var card = new TarjetaPanel { Radio = 10, Width = 210, Height = 74, Margin = new Padding(0, 0, UI.GapL, 0), BackColor = Color.White, Padding = new Padding(UI.GapL, UI.Gap, UI.Gap, UI.Gap) };
                var val = new Label { Text = "—", Dock = DockStyle.Fill, Font = Estilo.SemiBold(19f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
                var lbl = new Label { Text = I18n.T(it.Tag, it.Def), Tag = it.Tag, Dock = DockStyle.Bottom, Height = 20, Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris700, AutoEllipsis = true };
                card.Controls.Add(val); card.Controls.Add(lbl);
                row.Controls.Add(card);
                valores[i] = val;
            }
            return row;
        }

        protected TarjetaPanel TarjetaDeGrafico(GraficoControl g, bool conScroll)
        {
            var card = new TarjetaPanel { Radio = 10, BackColor = Color.White, Dock = DockStyle.Fill, Padding = new Padding(12), Margin = new Padding(0, 0, UI.GapL, UI.GapL) };
            if (conScroll)
            {
                var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
                card.Controls.Add(host);
                GraficoControl.Montar(host, g);
            }
            else
            {
                g.Dock = DockStyle.Fill;
                card.Controls.Add(g);
            }
            return card;
        }

        /// <summary>Barra de navegación secundaria (chips). Devuelve el panel; el callback recibe el índice.</summary>
        protected Panel NavSecundaria(Action<int> onSelect, out Label[] chips, params (string Tag, string Def)[] items)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Estilo.Lienzo };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo, WrapContents = false };
            var arr = new Label[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                var lbl = new Label
                {
                    Text = I18n.T(items[i].Tag, items[i].Def), Tag = items[i].Tag, AutoSize = false, Width = 170, Height = 30,
                    TextAlign = ContentAlignment.MiddleCenter, Font = Estilo.Normal(9.5f), ForeColor = Estilo.Gris700,
                    Cursor = Cursors.Hand, Margin = new Padding(0, 4, 6, 0), BackColor = Estilo.Gris100
                };
                lbl.Click += (s, e) => onSelect(idx);
                arr[i] = lbl;
                flow.Controls.Add(lbl);
            }
            panel.Controls.Add(flow);
            chips = arr;
            return panel;
        }

        protected static void PintarChips(Label[] chips, int activo)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                bool sel = i == activo;
                chips[i].BackColor = sel ? Estilo.AzulDeSeleccion : Estilo.Gris100;
                chips[i].ForeColor = sel ? Color.White : Estilo.Gris700;
                chips[i].Font = sel ? Estilo.SemiBold(9.5f) : Estilo.Normal(9.5f);
            }
        }

        // ── i18n ────────────────────────────────────────────────────────────────────
        public void UpdateLanguage(Idioma idioma)
        {
            Text = I18n.T(TituloKey, TituloDef);
            I18n.TraducirControles(this);
            TraducirExtra();
        }

        protected void Try(Action a) { try { a(); } catch (Exception ex) { MostrarError(ex); } }

        // ── Botón compacto (para pantallas densas en información) ───────────────────────────────
        // El tamaño "estándar" de Estilo.EstilizarBoton (mínimo 100×34, pensado para barras con pocos
        // botones sueltos) se ve desproporcionado al lado de grillas/fichas chicas. Se llama DESPUÉS
        // de EstilizarBoton para angostar el piso mínimo sin tocar el sistema de diseño global.
        protected static void Compacto(Button b)
        {
            b.MinimumSize = new Size(78, 27);
            b.Padding = new Padding(10, 0, 10, 0);
            b.Font = Estilo.SemiBold(8.75f);
        }

        // ── Resultados SIMULADOS del impacto de lo ya ejecutado ─────────────────────────────────
        // Vive en la base (en vez de en PlanesAccionForm directamente) para no acoplar la etapa
        // "③ Resultados" del recorrido PN03 a un único formulario concreto. Disponible apenas hay al
        // menos una tarea ejecutada — no requiere esperar el período de observación ni que el plan
        // esté Cerrado. Es DECLARADAMENTE ficticio (badge "SIMULADO" + aclaración compacta) y
        // determinista (SimuladorResultados.Simular siembra el Random con el Id real de cada
        // recomendación, así los números no cambian entre una apertura y la siguiente). NO llama a
        // EvaluarResultados ni toca BE.KpiSnapshot — es una proyección aparte, nunca se mezcla con el
        // snapshot real.
        //
        // Jerarquía (de arriba hacia abajo): indicadores clave Antes→Después → gráficos comparativos →
        // resultados por recomendación ejecutada. Todo en UN único flujo vertical (nunca dos layouts
        // distintos apilados): cada bloque es Dock.Top y se mide por su contenido.
        //
        // SIN encabezado propio ni botón de descarga acá: el título del plan, el estado y "Exportar
        // PDF" ya los muestra ConstruirEncabezado() arriba de CUALQUIER etapa (Recomendaciones/Tareas/
        // Resultados) — tenerlos acá TAMBIÉN duplicaba el título del plan y mostraba DOS botones de PDF
        // uno al lado del otro. Ahora ExportarPdf() (en PlanesAccionForm) decide sola cuál de los dos
        // informes generar según la etapa, así que un único botón alcanza siempre.
        protected Control ConstruirImpactoSimulado(BE.PlanAccion p, List<BE.RecomendacionPlan> recs)
        {
            // Dock.Top + AutoSize (NO Fill): este control se agrega dentro de un contenedor Dock.Fill
            // con AutoScroll propio (PlanesAccionForm._contenido)
            // — mismo patrón que el resto de las vistas de este recorrido (ver ConstruirSeguimiento). Se
            // mide por su contenido real y el padre se encarga de scrollear si no entra todo.
            var host = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White, Padding = new Padding(0, 0, 0, UI.GapL) };

            var resumen = SimuladorResultados.Calcular(recs);

            if (resumen.EjecutadasCount == 0)
            {
                var vacio = new Label { Dock = DockStyle.Top, Height = 60, Text = I18n.T("eh.pln.sim.vacio", "Todavía no se ejecutó ninguna tarea de este plan. Cuando ejecutes alguna en «Tareas», acá vas a ver una proyección de su impacto."), ForeColor = Estilo.Gris400, Font = Estilo.Normal(9.5f), TextAlign = ContentAlignment.MiddleCenter };
                host.Controls.Add(vacio);
                return host;
            }

            // ── Resultados por recomendación ejecutada: PRIORIZADO — primero en agregarse (queda más
            // abajo que indicadores/gráficos, pero mucho más arriba que en la versión anterior, donde
            // un banner gigante lo empujaba fuera de la pantalla). ─────────────────────────────────
            var titDet = UI.TituloSeccion(I18n.T("eh.pln.sim.detalle.titulo", "Resultados por recomendación ejecutada"), "eh.pln.sim.detalle.titulo");
            titDet.Dock = DockStyle.Top; titDet.Height = 22; titDet.Font = Estilo.SemiBold(9f); titDet.Padding = new Padding(0, 4, 0, 2);

            var grid = UI.Grilla();
            Estilo.EstilizarGrid(grid);
            var t = new System.Data.DataTable();
            t.Columns.Add("colEnt", typeof(string)); t.Columns.Add("colAcc", typeof(string));
            t.Columns.Add("colRes", typeof(string)); t.Columns.Add("colEsp", typeof(string));
            t.Columns.Add("colOcu", typeof(string)); t.Columns.Add("colIng", typeof(string));
            t.Columns.Add("colRdo", typeof(string)); t.Columns.Add("colDet", typeof(string));
            foreach (var im in resumen.Impactos)
            {
                string Delta(decimal a, decimal d, string fmt) => !im.TieneImpacto ? "—" : (d - a).ToString(fmt.StartsWith("$") ? "$ +#,##0;$ -#,##0;0" : "+0;-0;0");
                t.Rows.Add(
                    im.Rec.NombreEntidad, PlanificacionTextos.Accion(im.Rec.AccionSugerida),
                    Delta(im.ReservasAntes, im.ReservasDespues, "0"),
                    Delta(im.EsperaAntes, im.EsperaDespues, "0"),
                    im.TieneImpacto ? (im.OcupDespues - im.OcupAntes).ToString("+0;-0;0") + " pp" : "—",
                    Delta(im.IngresosAntes, im.IngresosDespues, "$"),
                    im.TieneImpacto ? (im.Mejora ? I18n.T("eh.pln.sim.resultado.mejoro", "Mejoró") : I18n.T("eh.pln.sim.resultado.neutro", "Neutro")) : I18n.T("eh.pln.sim.resultado.sinimpacto", "Sin impacto medible"),
                    im.Resumen);
            }
            grid.DataSource = t;
            grid.AutoGenerateColumns = true;
            grid.DataBindingComplete += (s, e) =>
            {
                if (grid.Columns["colEnt"] == null) return;
                grid.Columns["colEnt"].HeaderText = I18n.T("eh.pln.col.entidad", "Entidad");
                grid.Columns["colAcc"].HeaderText = I18n.T("eh.pln.rec.accion", "Acción sugerida");
                grid.Columns["colRes"].HeaderText = I18n.T("eh.pln.sim.col.reservas", "Δ Reservas");
                grid.Columns["colEsp"].HeaderText = I18n.T("eh.pln.sim.col.espera", "Δ Espera");
                grid.Columns["colOcu"].HeaderText = I18n.T("eh.pln.sim.col.ocup", "Δ Ocupación");
                grid.Columns["colIng"].HeaderText = I18n.T("eh.pln.sim.col.ingresos", "Δ Ingresos");
                grid.Columns["colRdo"].HeaderText = I18n.T("eh.pln.col.decision", "Resultado");
                grid.Columns["colDet"].HeaderText = I18n.T("eh.pdf.sim.detalle", "Detalle");
                grid.Columns["colEnt"].FillWeight = 14; grid.Columns["colAcc"].FillWeight = 14;
                grid.Columns["colRes"].FillWeight = 9; grid.Columns["colEsp"].FillWeight = 9;
                grid.Columns["colOcu"].FillWeight = 9; grid.Columns["colIng"].FillWeight = 10;
                grid.Columns["colRdo"].FillWeight = 10; grid.Columns["colDet"].FillWeight = 25;
            };
            var gridPanel = new TarjetaPanel { Dock = DockStyle.Top, Height = Math.Min(280, 40 + resumen.Impactos.Count * 32 + 12), Radio = 8, BackColor = Color.White, BordeColor = Estilo.Gris200, Padding = new Padding(1), Margin = new Padding(0, 0, 0, UI.GapL) };
            gridPanel.Controls.Add(grid);

            host.Controls.Add(gridPanel);
            host.Controls.Add(titDet);

            // ── Gráficos comparativos: MISMOS datos que el ribbon de abajo y que el PDF — no son
            // decorativos, representan ocupación, reservas y lista de espera antes/después. ──────────
            var titGraf = UI.TituloSeccion(I18n.T("eh.pln.sim.sec.graficos", "Comparativo visual"), "eh.pln.sim.sec.graficos");
            titGraf.Dock = DockStyle.Top; titGraf.Height = 22; titGraf.Font = Estilo.SemiBold(9f); titGraf.Padding = new Padding(0, 4, 0, 2);
            var graficos = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 220, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };

            var gOcup = new GraficoControl { Titulo = I18n.T("eh.pln.sim.graf.ocup", "Ocupación promedio (%)"), Tipo = GraficoControl.TipoGrafico.BarrasAgrupadas };
            gOcup.SetSeries(new[] { I18n.T("eh.pln.sim.kpi.ocup", "Ocupación promedio") },
                new GraficoControl.Serie(I18n.T("eh.pln.seg.antes", "Antes"), Estilo.Gris400, new[] { (double)resumen.OcupAntes }),
                new GraficoControl.Serie(I18n.T("eh.pln.seg.despues", "Después"), Estilo.AzulDeSeleccion, new[] { (double)resumen.OcupDespues }));
            gOcup.MaxForzado = 100;

            var gVol = new GraficoControl { Titulo = I18n.T("eh.pln.sim.graf.vol", "Reservas y lista de espera"), Tipo = GraficoControl.TipoGrafico.BarrasAgrupadas };
            gVol.SetSeries(new[] { I18n.T("eh.pln.sim.kpi.reservas", "Reservas"), I18n.T("eh.pln.sim.kpi.espera", "Lista de espera") },
                new GraficoControl.Serie(I18n.T("eh.pln.seg.antes", "Antes"), Estilo.Gris400, new[] { (double)resumen.ReservasAntes, (double)resumen.EsperaAntes }),
                new GraficoControl.Serie(I18n.T("eh.pln.seg.despues", "Después"), Estilo.Verde, new[] { (double)resumen.ReservasDespues, (double)resumen.EsperaDespues }));

            graficos.Controls.Add(TarjetaGraficoChica(gOcup));
            graficos.Controls.Add(TarjetaGraficoChica(gVol));

            host.Controls.Add(graficos);
            host.Controls.Add(titGraf);

            // ── Indicadores clave Antes→Después ("Resumen de impacto" + "Indicadores") ──────────
            var titKpi = UI.TituloSeccion(I18n.T("eh.pln.sim.sec.indicadores", "Indicadores clave (Antes → Después)"), "eh.pln.sim.sec.indicadores");
            titKpi.Dock = DockStyle.Top; titKpi.Height = 22; titKpi.Font = Estilo.SemiBold(9f); titKpi.Padding = new Padding(0, 4, 0, 2);
            var kpis = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };
            kpis.Controls.Add(TarjetaKpiSim(I18n.T("eh.pln.sim.kpi.ocup", "Ocupación promedio"), resumen.OcupAntes, resumen.OcupDespues, "0", UnidadKpi.PuntosPorcentuales, true));
            kpis.Controls.Add(TarjetaKpiSim(I18n.T("eh.pln.sim.kpi.reservas", "Reservas"), resumen.ReservasAntes, resumen.ReservasDespues, "0", UnidadKpi.Cantidad, true));
            kpis.Controls.Add(TarjetaKpiSim(I18n.T("eh.pln.sim.kpi.espera", "Lista de espera"), resumen.EsperaAntes, resumen.EsperaDespues, "0", UnidadKpi.Cantidad, false));
            kpis.Controls.Add(TarjetaKpiSim(I18n.T("eh.pln.sim.kpi.cancel", "Cancelaciones"), resumen.CancelAntes, resumen.CancelDespues, "0", UnidadKpi.Cantidad, false));
            kpis.Controls.Add(TarjetaKpiSim(I18n.T("eh.pln.sim.kpi.ingresos", "Ingresos estimados"), resumen.IngresosAntes, resumen.IngresosDespues, "$ #,##0", UnidadKpi.Moneda, true));
            kpis.Controls.Add(TarjetaEfectividad(I18n.T("eh.pln.sim.kpi.efectividad", "Efectividad de las recomendaciones"), resumen));

            host.Controls.Add(kpis);
            host.Controls.Add(titKpi);

            var tit = UI.TituloSeccion(I18n.T("eh.pln.sim.titulo", "Resultados del plan"), "eh.pln.sim.titulo");
            tit.Dock = DockStyle.Top;
            host.Controls.Add(tit);
            return host;
        }

        // SIN Dock: dentro de un FlowLayoutPanel el Dock de un hijo no se traduce en un tamaño real
        // (el hijo queda en 0×0 y no se ve nada) — hay que darle Width/Height EXPLÍCITOS, igual que el
        // resto de las tarjetas que viven en FlowLayoutPanel en este archivo (KPIs, chips, etc.).
        private static TarjetaPanel TarjetaGraficoChica(GraficoControl g)
        {
            var card = new TarjetaPanel { Width = 360, Height = 206, Radio = 8, BackColor = Color.White, BordeColor = Estilo.Gris200, Padding = new Padding(10), Margin = new Padding(0, 0, UI.Gap, 0) };
            g.Dock = DockStyle.Fill;
            card.Controls.Add(g);
            return card;
        }

        private enum UnidadKpi { Cantidad, Moneda, PuntosPorcentuales }

        // Tarjeta KPI Antes→Después con mini-barra proporcional (sin librería de gráficos: dos franjas
        // de ancho relativo al valor, gris=Antes / color=Después). Distingue EXPLÍCITAMENTE puntos
        // porcentuales (ocupación: ya es un %, el delta se etiqueta "pp" para no confundirlo con un
        // cambio relativo) de cantidades/montos (reservas/espera/cancelaciones/ingresos: el delta es la
        // diferencia absoluta + el cambio relativo "% vs antes" como dato secundario, igual que en el PDF).
        private Control TarjetaKpiSim(string titulo, decimal antes, decimal despues, string formato, UnidadKpi unidad, bool subirEsMejor)
        {
            var card = new TarjetaPanel { Width = 198, Height = 106, Radio = 8, BackColor = Color.White, BordeColor = Estilo.Gris200, Padding = new Padding(10, 8, 10, 6), Margin = new Padding(0, 0, UI.Gap, UI.Gap) };
            decimal delta = despues - antes;
            bool mejora = subirEsMejor ? delta >= 0 : delta <= 0;
            Color colorDelta = delta == 0 ? Estilo.Gris400 : (mejora ? Estilo.Verde : Estilo.Rojo);
            string sufijo = unidad == UnidadKpi.PuntosPorcentuales ? "%" : "";

            var cap = new Label { Dock = DockStyle.Top, Height = 15, Font = Estilo.SemiBold(7.75f), ForeColor = Estilo.Gris700, Text = titulo, AutoEllipsis = true };
            var valores = new Label { Dock = DockStyle.Top, Height = 20, Font = Estilo.SemiBold(10f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true, Text = antes.ToString(formato) + sufijo + " → " + despues.ToString(formato) + sufijo };
            string deltaTxt = unidad == UnidadKpi.PuntosPorcentuales
                ? (delta >= 0 ? "+" : "") + delta.ToString("0") + " pp"
                : (delta >= 0 ? "+" : "") + delta.ToString(formato);
            // Cambio relativo, SOLO para cantidades/montos (no para puntos porcentuales, que ya son un
            // porcentaje — mostrar "% de un %" confunde más de lo que aclara).
            if (unidad != UnidadKpi.PuntosPorcentuales && antes != 0)
                deltaTxt += " (" + (delta >= 0 ? "+" : "") + Math.Round(100m * delta / Math.Abs(antes), 0).ToString("0") + "%)";
            var deltaLbl = new Label { Dock = DockStyle.Top, Height = 15, Font = Estilo.Bold(7.75f), ForeColor = colorDelta, Text = deltaTxt, AutoEllipsis = true };

            decimal max = Math.Max(1m, Math.Max(Math.Abs(antes), Math.Abs(despues)));
            var barHost = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = Color.White, Padding = new Padding(0, 4, 0, 0) };
            int wAntes = (int)(158m * Math.Abs(antes) / max), wDespues = (int)(158m * Math.Abs(despues) / max);
            var barAntes = new Panel { Dock = DockStyle.Top, Height = 5, Width = Math.Max(2, wAntes), BackColor = Estilo.Gris200 };
            var barDespues = new Panel { Dock = DockStyle.Top, Height = 5, Width = Math.Max(2, wDespues), BackColor = colorDelta, Margin = new Padding(0, 2, 0, 0) };
            barHost.Controls.Add(barDespues); barHost.Controls.Add(barAntes);

            card.Controls.Add(barHost); card.Controls.Add(deltaLbl); card.Controls.Add(valores); card.Controls.Add(cap);
            return card;
        }

        // Tarjeta de efectividad CON explicación del cálculo (evita el "100% engañoso" cuando la base
        // es chica): subtítulo SIEMPRE visible con "X de Y tareas mejoraron" — nunca solo el número.
        private Control TarjetaEfectividad(string titulo, SimuladorResultados.Resumen resumen)
        {
            decimal pct = resumen.Efectividad;
            int conImpacto = resumen.ConImpacto, mejoraron = resumen.MejoraronCount;
            Color col = conImpacto == 0 ? Estilo.Gris400 : (pct >= 66 ? Estilo.Verde : pct >= 33 ? Estilo.Amarillo : Estilo.Rojo);
            var card = new TarjetaPanel { Width = 210, Height = 106, Radio = 8, BackColor = Color.White, BordeColor = Estilo.Gris200, Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 0, UI.Gap, UI.Gap), FranjaAncho = 4, FranjaColor = col };
            var cap = new Label { Dock = DockStyle.Top, Height = 15, Font = Estilo.SemiBold(7.75f), ForeColor = Estilo.Gris700, Text = titulo, AutoEllipsis = true };
            var valor = new Label { Dock = DockStyle.Top, Height = 28, Font = Estilo.SemiBold(17f), ForeColor = col, Text = conImpacto == 0 ? "—" : pct.ToString("0") + "%" };
            var sub = new Label { Dock = DockStyle.Fill, Font = Estilo.Normal(7.25f), ForeColor = Estilo.Gris400, Text = conImpacto == 0
                ? I18n.T("eh.pln.sim.sinbase", "Ninguna tarea ejecutada tiene impacto medible todavía.")
                : string.Format(I18n.T("eh.pln.sim.basecount2", "{0} de {1} tarea(s) con impacto mejoraron (≥3 de 5 indicadores positivos)."), mejoraron, conImpacto) };
            card.Controls.Add(sub); card.Controls.Add(valor); card.Controls.Add(cap);
            return card;
        }

        // Informe PDF del impacto SIMULADO — mismo motor real (PDFsharp → archivo .pdf elegido por el
        // usuario, sin impresora virtual ni mock). Reutiliza los mismos números ya calculados en
        // pantalla (resumen), para que lo que se descarga sea EXACTAMENTE lo que se está viendo.
        protected void ExportarInformeSimulado(BE.PlanAccion p, SimuladorResultados.Resumen resumen)
        {
            var doc = Exportacion.InformePlanificacion.DocumentoImpactoSimulado(p, resumen);
            new Exportacion.ExportadorPdfArchivo().Exportar(doc, this);
        }
    }
}

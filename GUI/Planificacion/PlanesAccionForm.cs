using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using T = GUI.Planificacion.PlanificacionTextos;

namespace GUI.Planificacion
{
    /// <summary>
    /// PN03 · E — Planes de acción (administración y seguimiento). Esquema MAESTRO-DETALLE:
    ///  • Izquierda: indicadores globales COMPACTOS + filtro + lista de planes.
    ///  • Derecha (detalle): encabezado compacto del plan (nombre/estado/período/cantidades + PDF),
    ///    PasoBar de progreso (bajo) y el CONTENIDO de la etapa consultada — solo una por vez:
    ///       Analizar y decidir → recomendaciones y decisiones.
    ///       Ejecutar el plan   → tareas (aprobadas) y su ejecución.
    ///       Evaluar resultados → comparativo Antes/Después y conclusiones.
    /// Respeta transiciones, permisos y bitácoras de BLL.PlanAccionService (no las altera).
    /// </summary>
    internal sealed class PlanesAccionForm : PlanificacionFormBase
    {
        private const string MODULO = "Planificación";
        private readonly BLL.PlanAccionService _svc = new BLL.PlanAccionService();

        private enum ModoRec { Decision, Ejecucion, Lectura }

        private Label _kPend, _kAprob, _kEjec, _kCerr;
        private Label[] _chips;
        private int _filtro;   // 0 activos · 1 finalizados · 2 todos
        private PasoBar _pasoBar;
        private int _etapa = ProcesoPlanificacion.Analizar;   // etapa/sección consultada en el detalle
        private FlowLayoutPanel _flowPlanes;
        private Label _lblVacio;
        private Panel _encabezadoHost;   // encabezado compacto del plan (se reconstruye por plan)
        private Panel _franjaAccion;     // franja FIJA de "Próxima acción": siempre el mismo lugar
        private Panel _contenido;        // contenido de la etapa consultada
        // Guard de reentrancia: evita que un doble clic dispare la misma transición de estado dos
        // veces mientras la primera todavía está en curso (confirmación modal + llamada a BLL).
        private bool _accionEnCurso;
        private BE.PlanAccion _sel;
        private List<BE.PlanAccion> _cache = new List<BE.PlanAccion>();
        private readonly List<(TarjetaPanel Card, BE.PlanAccion Plan)> _cards = new List<(TarjetaPanel, BE.PlanAccion)>();

        // ── Flujo "Nuevo plan" (ex DecisionesRecomendacionesForm) ───────────────────────────────
        // Antes vivía en una pantalla MDI aparte ("Decisiones y recomendaciones"). Se fusionó acá:
        // las dos pantallas mostraban el MISMO stepper de 3 etapas y generar/elegir recomendaciones
        // es, en los hechos, el primer paso de ESTE mismo recorrido (Analizar) — tenerlo en otra
        // ventana obligaba a saltar entre dos lugares para una sola tarea. Nota de permisos: la
        // pantalla separada se habilitaba con la patente Planificacion (la tienen roles analíticos
        // que NO pueden crear planes); como PlanesAccionForm exige PlanAccion para abrir, un usuario
        // con Planificacion pero sin PlanAccion deja de poder generar/ver recomendaciones acá — se
        // acepta ese recorte a cambio de una sola pantalla (ver conversación de diseño).
        private bool _modoNuevoPlan;
        private readonly List<(CheckBox Chk, BE.RecomendacionGenerada Rec)> _seleccionNuevo = new List<(CheckBox, BE.RecomendacionGenerada)>();
        private FlowLayoutPanel _flowNuevo;
        private Label _vacioNuevo, _hallazgosNuevo, _resumenSelNuevo;

        public PlanesAccionForm() { Inicializar(); }

        protected override string TituloKey => "eh.pln.sec.planes";
        protected override string TituloDef => "Planes de acción";
        protected override string SubKey => "eh.pln.planes.sub";
        protected override string SubDef => "Generá recomendaciones, decidí, ejecutá tareas y seguí los resultados.";
        protected override string ContextoAyuda => "pn03.planes";
        protected override string PatenteVer => BE.Patentes.PlanAccion;
        // Los filtros de período/ciudad/categoría/organizador (de la base) solo hacen falta durante el
        // flujo "Nuevo plan" (generar recomendaciones) — se ocultan el resto del tiempo (ver MostrarNuevoPlan).
        protected override bool MostrarFiltros => true;

        protected override void ConstruirCuerpo(Panel host)
        {
            // ── Indicadores globales COMPACTOS (una fila de 48px, no las tarjetas altas) ──
            var kpiTitulo = new Label { Dock = DockStyle.Top, Height = 18, Text = I18n.T("eh.pln.resumenplanes", "Resumen de todos los planes"), Tag = "eh.pln.resumenplanes", Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris700 };
            var tiraKpis = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Estilo.Lienzo, WrapContents = false, Padding = new Padding(0, 0, 0, UI.Gap) };
            tiraKpis.Controls.Add(ChipKpi("eh.pln.kpi.planespend", "Pendientes", out _kPend, Estilo.Amarillo));
            tiraKpis.Controls.Add(ChipKpi("eh.pln.kpi.planesaprob", "Aprobados", out _kAprob, Estilo.AzulDeSeleccion));
            tiraKpis.Controls.Add(ChipKpi("eh.pln.kpi.planesejec", "En ejecución", out _kEjec, Estilo.Naranja));
            tiraKpis.Controls.Add(ChipKpi("eh.pln.kpi.planescerr", "Cerrados", out _kCerr, Estilo.Verde));

            var barra = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Estilo.Lienzo };
            var chipsPanel = NavSecundaria(SeleccionarFiltro, out _chips,
                ("eh.pln.filtro.activos", "Activos"), ("eh.pln.filtro.finalizados", "Finalizados"), ("eh.pln.filtro.todos", "Todos"));
            chipsPanel.Dock = DockStyle.Left; chipsPanel.Width = 560;
            var refrescar = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 260, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Estilo.Lienzo };
            refrescar.Controls.Add(UI.Secundario(I18n.T("eh.btn.refrescar", "↻ Actualizar"), "eh.btn.refrescar", (s, e) => Try(Repintar)));
            // Punto de entrada al flujo "Nuevo plan" (generar recomendaciones → elegir → crear) — antes
            // era una pantalla MDI aparte ("Decisiones y recomendaciones"); ver comentario en los campos.
            refrescar.Controls.Add(UI.Primario(I18n.T("eh.btn.nuevoplan", "+ Nuevo plan"), "eh.btn.nuevoplan", (s, e) => Try(MostrarNuevoPlan)));
            barra.Controls.Add(chipsPanel);
            barra.Controls.Add(refrescar);

            var area = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White, Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.Gap), Margin = new Padding(0, UI.Gap, 0, 0) };
            var lista = new Panel { Dock = DockStyle.Left, Width = 300, BackColor = Color.White, Padding = new Padding(0, 0, UI.Gap, 0) };
            _flowPlanes = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown };
            _lblVacio = new Label { Dock = DockStyle.Fill, Text = I18n.T("eh.pln.vacio.planes", "Todavía no hay planes. Creá uno a partir de las recomendaciones del período."), Tag = "eh.pln.vacio.planes", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Estilo.Gris400, Font = Estilo.Normal(9.5f) };
            lista.Controls.Add(_flowPlanes);
            lista.Controls.Add(_lblVacio);

            // ── Panel DETALLE (derecha): encabezado + PasoBar + franja de "Próxima acción" + contenido ──
            // La franja es el punto fijo que responde "qué hacer ahora": antes, las 4 acciones de
            // transición de estado (Aprobar/Ejecución/Cerrar/Evaluar) vivían sueltas en el encabezado,
            // apareciendo y desapareciendo sin aviso. Ahora hay UN solo lugar, siempre en el mismo
            // sitio (debajo del stepper), que muestra la transición disponible + por qué.
            var detalleWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(UI.GapL, 0, 0, 0) };
            _encabezadoHost = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.White };
            _pasoBar = new PasoBar { Dock = DockStyle.Top, Height = 54, Pasos = ProcesoPlanificacion.Etiquetas() };
            _pasoBar.PasoClicado += OnPasoClicado;
            _franjaAccion = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.White, Padding = new Padding(0, 4, 0, 4) };
            _contenido = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
            detalleWrap.Controls.Add(_contenido);
            detalleWrap.Controls.Add(_franjaAccion);
            detalleWrap.Controls.Add(_pasoBar);
            detalleWrap.Controls.Add(_encabezadoHost);

            area.Controls.Add(detalleWrap);
            area.Controls.Add(lista);

            host.Controls.Add(area);
            host.Controls.Add(barra);
            host.Controls.Add(tiraKpis);
            host.Controls.Add(kpiTitulo);

            // Oculta por defecto: solo hace falta durante "Nuevo plan" (ver MostrarNuevoPlan/CancelarNuevoPlan).
            if (Filtros != null) Filtros.Visible = false;

            PintarChips(_chips, 0);
            ActualizarPasoBar();
        }

        private TarjetaPanel ChipKpi(string tag, string def, out Label valor, Color acento)
        {
            var card = new TarjetaPanel { Radio = 8, Width = 172, Height = 44, BackColor = Color.White, Margin = new Padding(0, 0, UI.GapL, 0), Padding = new Padding(10, 2, 8, 2), FranjaAncho = 4, FranjaColor = acento };
            valor = new Label { Text = "0", Dock = DockStyle.Left, Width = 32, Font = Estilo.SemiBold(15f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.MiddleLeft };
            var lbl = new Label { Text = I18n.T(tag, def), Tag = tag, Dock = DockStyle.Fill, Font = Estilo.Normal(8f), ForeColor = Estilo.Gris700, TextAlign = ContentAlignment.MiddleLeft };
            card.Controls.Add(lbl);
            card.Controls.Add(valor);
            return card;
        }

        private void SeleccionarFiltro(int idx) { _filtro = idx; PintarChips(_chips, idx); Try(RenderPlanes); }

        protected override void Repintar() => RenderPlanes();

        /// <summary>Entrada desde la navegación contextual: selecciona el plan indicado (si llega) y
        /// posiciona el recorrido en la etapa dada (o la sugerida si no es válida).</summary>
        public void AbrirEnContexto(int? planId, int etapa)
        {
            Try(RenderPlanes);
            if (planId.HasValue) SeleccionarPorId(planId.Value);
            if (etapa == ProcesoPlanificacion.Evaluar) { MostrarEvaluacion(); return; }
            _etapa = EtapaValida(etapa) ? etapa : ProcesoPlanificacion.EtapaSugerida(_sel);
            ActualizarPasoBar();
            RenderDetalle();
        }

        private bool EtapaValida(int etapa)
        {
            // Bloqueado solo depende de "hay o no plan seleccionado" (ver ProcesoPlanificacion.Estados):
            // ni recs ni tieneEvaluacion cambian esa parte, así que no hace falta consultarlos acá.
            var estados = ProcesoPlanificacion.Estados(_sel, null, false, HayCerrados());
            return etapa >= 0 && etapa < estados.Length && estados[etapa] != PasoBar.EstadoPaso.Bloqueado;
        }

        private bool HayCerrados() => _cache != null && _cache.Any(p => p.Estado == BE.EstadoPlanAccion.Cerrado);

        private void ActualizarPasoBar()
        {
            if (_pasoBar == null) return;
            var recs = _sel != null && Puede(BE.Patentes.PlanAccion)
                ? _svc.ObtenerRecomendaciones(_sel.IdPlanAccion) : null;
            bool tieneEvaluacion = _sel != null && Puede(BE.Patentes.PlanAccion) && _svc.TieneEvaluacion(_sel.IdPlanAccion);
            _pasoBar.Pasos = ProcesoPlanificacion.Etiquetas();
            _pasoBar.Estados = ProcesoPlanificacion.Estados(_sel, recs, tieneEvaluacion, HayCerrados());   // progreso REAL del plan
            _pasoBar.Pendientes = ProcesoPlanificacion.Pendientes(_sel, recs);
            // Sección consultada (marca azul bajo el pill): en el flujo "Nuevo plan" es siempre
            // Analizar (ahí vive), aunque no haya ningún BE.PlanAccion seleccionado todavía.
            _pasoBar.Seccion = _modoNuevoPlan ? ProcesoPlanificacion.Analizar : (_sel != null ? _etapa : -1);
        }

        private void OnPasoClicado(int etapa)
        {
            // Sin plan seleccionado, "Analizar" abre el flujo de generar/elegir recomendaciones
            // (antes era otra pantalla MDI — ahora vive acá mismo, ver MostrarNuevoPlan).
            if (_sel == null && etapa == ProcesoPlanificacion.Analizar) { MostrarNuevoPlan(); return; }
            if (etapa == ProcesoPlanificacion.Evaluar) { MostrarEvaluacion(); return; }
            if (!EtapaValida(etapa)) return;   // etapa bloqueada para este plan
            _etapa = etapa;
            ActualizarPasoBar();
            RenderDetalle();
        }

        // ── Flujo "Nuevo plan": generar recomendaciones del período, elegir y crear ─────────────
        private void MostrarNuevoPlan()
        {
            _modoNuevoPlan = true;
            _sel = null;
            if (Filtros != null) Filtros.Visible = true;
            Resaltar();
            ActualizarPasoBar();
            RenderDetalle();
        }

        private void CancelarNuevoPlan()
        {
            _modoNuevoPlan = false;
            if (Filtros != null) Filtros.Visible = false;
            RenderPlanes();
        }

        /// <summary>Lleva a Evaluar. Si el plan actual ya es evaluable (En ejecución o Cerrado) muestra su
        /// evaluación; si no, abre los planes FINALIZADOS para consultar resultados anteriores.</summary>
        private void MostrarEvaluacion()
        {
            _modoNuevoPlan = false;
            if (Filtros != null) Filtros.Visible = false;
            // Si YA hay un plan seleccionado, se muestra SU "Resultados" (aunque todavía no tenga
            // evaluación real — ConstruirVistaEvaluar/ConstruirImpactoSimulado ya explican por qué,
            // en vez de saltar en silencio a otro plan Cerrado sin avisar). El fallback de "ir a
            // consultar resultados de un plan ya finalizado" queda reservado para cuando NO hay
            // ningún plan seleccionado (p. ej. clic en el stepper sin haber elegido nada todavía).
            if (_sel != null)
            {
                _etapa = ProcesoPlanificacion.Evaluar;
                ActualizarPasoBar(); RenderDetalle();
                return;
            }
            _filtro = 1; PintarChips(_chips, 1);
            RenderPlanes();
            var cerrado = _cache.Where(p => p.Estado == BE.EstadoPlanAccion.Cerrado)
                                .OrderByDescending(p => p.FechaCierre ?? p.FechaCreacion).FirstOrDefault();
            if (cerrado == null)
            {
                Estilo.Info(this, I18n.T("eh.pln.evaluar.titulodlg", "Evaluar resultados"),
                    I18n.T("eh.pln.evaluar.sincerrados", "Todavía no hay planes finalizados para evaluar."));
                return;
            }
            _sel = cerrado;
            ContextoPlanificacion.PlanSeleccionadoId = cerrado.IdPlanAccion;
            _etapa = ProcesoPlanificacion.Evaluar;
            Resaltar(); ActualizarPasoBar(); RenderDetalle();
        }

        private void RenderPlanes()
        {
            if (!Puede(BE.Patentes.PlanAccion)) return;
            _cache = _svc.ObtenerPlanes() ?? new List<BE.PlanAccion>();
            _kPend.Text = _cache.Count(p => p.Estado == BE.EstadoPlanAccion.Borrador).ToString();
            _kAprob.Text = _cache.Count(p => p.Estado == BE.EstadoPlanAccion.Aprobado).ToString();
            _kEjec.Text = _cache.Count(p => p.Estado == BE.EstadoPlanAccion.EnEjecucion).ToString();
            _kCerr.Text = _cache.Count(p => p.Estado == BE.EstadoPlanAccion.Cerrado).ToString();

            var filtrados = _cache.Where(p =>
                _filtro == 2 ||
                (_filtro == 0 && p.Estado != BE.EstadoPlanAccion.Cerrado) ||
                (_filtro == 1 && p.Estado == BE.EstadoPlanAccion.Cerrado))
                .OrderByDescending(p => p.FechaCreacion).ToList();

            _flowPlanes.Controls.Clear();
            _cards.Clear();
            _lblVacio.Visible = filtrados.Count == 0;
            _flowPlanes.Visible = filtrados.Count > 0;
            foreach (var p in filtrados)
            {
                var card = TarjetaPlanMini(p);
                _cards.Add((card, p));
                _flowPlanes.Controls.Add(card);
            }
            if (_sel != null && filtrados.All(p => p.IdPlanAccion != _sel.IdPlanAccion)) _sel = null;
            // Mientras se arma un plan nuevo (_modoNuevoPlan) NO se autoselecciona ningún plan
            // existente: _sel debe seguir null para que el flujo de generación quede visible.
            if (_sel == null && !_modoNuevoPlan && filtrados.Count > 0)
            {
                _sel = filtrados.FirstOrDefault(p => p.IdPlanAccion == (ContextoPlanificacion.PlanSeleccionadoId ?? -1))
                       ?? filtrados[0];
                _etapa = ProcesoPlanificacion.EtapaSugerida(_sel);
            }
            Resaltar();
            ActualizarPasoBar();
            RenderDetalle();
        }

        private TarjetaPanel TarjetaPlanMini(BE.PlanAccion p)
        {
            var card = new TarjetaPanel { Radio = 10, Width = 272, Height = 92, BackColor = Color.White, BordeColor = Estilo.Gris200, Margin = new Padding(0, 0, 0, UI.Gap), Padding = new Padding(UI.Gap, UI.Gap, UI.Gap, UI.Gap), Cursor = Cursors.Hand };
            var tit = new Label { Dock = DockStyle.Top, Height = 22, Text = "#" + p.IdPlanAccion + " · " + p.Titulo, Font = Estilo.SemiBold(10f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true };
            var per = new Label { Dock = DockStyle.Top, Height = 18, Text = p.PeriodoDesde.ToString("dd/MM/yy") + " – " + p.PeriodoHasta.ToString("dd/MM/yy"), Font = Estilo.Normal(8f), ForeColor = Estilo.Gris700 };
            var pill = T.Pill(T.EstadoPlan(p.Estado), T.ColorEstadoPlan(p.Estado)); pill.Dock = DockStyle.Bottom;
            card.Controls.Add(per); card.Controls.Add(tit); card.Controls.Add(pill);
            EventHandler sel = (s, e) => Try(() =>
            {
                _sel = p;
                _modoNuevoPlan = false;   // elegir un plan existente sale del flujo "Nuevo plan"
                if (Filtros != null) Filtros.Visible = false;
                ContextoPlanificacion.PlanSeleccionadoId = p.IdPlanAccion;
                _etapa = ProcesoPlanificacion.EtapaSugerida(p);
                Resaltar(); ActualizarPasoBar(); RenderDetalle();
            });
            card.Click += sel; tit.Click += sel; per.Click += sel; pill.Click += sel;
            return card;
        }

        private void Resaltar()
        {
            foreach (var (card, plan) in _cards)
            {
                bool s = _sel != null && plan.IdPlanAccion == _sel.IdPlanAccion;
                card.RellenoColor = s ? Estilo.AzulDeFondo : Color.White;
                card.BackColor = s ? Estilo.AzulDeFondo : Color.White;
                card.BordeColor = s ? Estilo.AzulDeSeleccion : Estilo.Gris200;
                card.BordeGrosor = s ? 2f : 1f;
                card.Invalidate();
            }
        }

        private void SeleccionarPorId(int id)
        {
            var p = _cache.FirstOrDefault(x => x.IdPlanAccion == id);
            if (p != null)
            {
                _sel = p;
                _modoNuevoPlan = false;
                if (Filtros != null) Filtros.Visible = false;
                ContextoPlanificacion.PlanSeleccionadoId = id;
                Resaltar(); ActualizarPasoBar(); RenderDetalle();
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  DETALLE: encabezado + contenido de la etapa consultada
        // ══════════════════════════════════════════════════════════════════════════
        // Punto único de entrada para redibujar el detalle: varios lugares del formulario lo llaman
        // directamente (clic en una tarjeta de plan, cambio de etapa, acciones de estado) sin pasar
        // todos por Try(...). Se protege ACÁ, en el origen, para que ningún llamador —actual o
        // futuro— pueda tirar abajo el formulario entero si algo falla construyendo el contenido.
        private void RenderDetalle()
        {
            try { RenderDetalleInterno(); }
            catch (Exception ex)
            {
                _contenido.Controls.Clear();
                _contenido.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Estilo.Rojo, Font = Estilo.Normal(9.5f), Text = ex.Message });
                MostrarError(ex);
            }
        }

        private void RenderDetalleInterno()
        {
            _encabezadoHost.Controls.Clear();
            _franjaAccion.Controls.Clear();
            _contenido.Controls.Clear();

            if (_modoNuevoPlan)
            {
                _encabezadoHost.Visible = true;
                _franjaAccion.Visible = false;   // no hay transición de estado posible sin plan todavía
                ConstruirEncabezadoNuevoPlan();
                ConstruirFlujoNuevoPlan();
                return;
            }

            if (_sel == null)
            {
                _encabezadoHost.Visible = false;
                _franjaAccion.Visible = false;
                _contenido.Controls.Add(new Label { Dock = DockStyle.Fill, Text = I18n.T("eh.pln.plan.sinsel", "Seleccioná un plan para ver su detalle, decidir sobre sus recomendaciones y seguir su ejecución."), Tag = "eh.pln.plan.sinsel", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Estilo.Gris400, Font = Estilo.Normal(10f) });
                return;
            }
            _encabezadoHost.Visible = true;
            _franjaAccion.Visible = true;
            var recs = _svc.ObtenerRecomendaciones(_sel.IdPlanAccion) ?? new List<BE.RecomendacionPlan>();
            ConstruirEncabezado(_sel, recs);
            ConstruirFranjaAccion(_sel, recs);

            if (_etapa == ProcesoPlanificacion.Analizar)      ConstruirVistaAnalizar(recs);
            else if (_etapa == ProcesoPlanificacion.Ejecutar) ConstruirVistaEjecutar(recs);
            else                                              ConstruirVistaEvaluar(recs);
        }

        // ── Encabezado del flujo "Nuevo plan" (mismo zócalo que el encabezado de un plan real,
        // pero con un botón "Cancelar" en vez de "Exportar PDF") ───────────────────────────────
        private void ConstruirEncabezadoNuevoPlan()
        {
            var btnCancelar = UI.Secundario(I18n.T("eh.btn.cancelar", "Cancelar"), "eh.btn.cancelar", (s, e) => Try(CancelarNuevoPlan));
            var acciones = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.White, Margin = new Padding(0, 4, 0, 0) };
            acciones.Controls.Add(btnCancelar);
            Estilo.EstilizarBoton(btnCancelar);
            Compacto(btnCancelar);

            var tit = new Label { Dock = DockStyle.Fill, Text = I18n.T("eh.pln.nuevoplan.titulo", "Nuevo plan — generá recomendaciones del período"), Font = Estilo.SemiBold(12.5f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            var linea = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Estilo.Gris200 };

            _encabezadoHost.Controls.Add(tit);
            _encabezadoHost.Controls.Add(acciones);
            _encabezadoHost.Controls.Add(linea);
        }

        // ── Cuerpo del flujo "Nuevo plan": Generar recomendaciones → elegir con checkbox → Crear
        // plan — MISMA lógica que tenía DecisionesRecomendacionesForm (ex pantalla MDI separada),
        // ahora operando sobre el período/filtros de la franja Filtros (de la base), visible solo
        // mientras este flujo está activo. ──────────────────────────────────────────────────────
        private void ConstruirFlujoNuevoPlan()
        {
            var card = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White, Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.Gap) };
            var titRecs = new Label { Dock = DockStyle.Top, Height = 24, Text = I18n.T("eh.pln.sec.recomperiodo", "Recomendaciones del período"), Tag = "eh.pln.sec.recomperiodo", Font = Estilo.SemiBold(11f), ForeColor = Estilo.AzulOscuro };
            _flowNuevo = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight };
            _vacioNuevo = new Label { Dock = DockStyle.Fill, Text = I18n.T("eh.pln.vacio.recs", "Generá las recomendaciones del período para verlas con su evidencia."), Tag = "eh.pln.vacio.recs", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Estilo.Gris400, Font = Estilo.Normal(10.5f) };
            card.Controls.Add(_flowNuevo);
            card.Controls.Add(_vacioNuevo);
            card.Controls.Add(titRecs);

            _hallazgosNuevo = new Label { Dock = DockStyle.Top, Height = 20, Font = Estilo.Normal(9.5f), ForeColor = Estilo.Gris700 };

            var acciones = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, BackColor = Color.White, WrapContents = false, Margin = new Padding(0, 0, 0, UI.Gap) };
            var btnGenerar = UI.Secundario(I18n.T("eh.btn.generarrec", "Generar recomendaciones"), "eh.btn.generarrec", (s, e) => Try(GenerarRecomendaciones));
            var btnCrear = UI.Primario(I18n.T("eh.pln.crearplansel", "Crear plan con las seleccionadas"), "eh.pln.crearplansel", (s, e) => Try(CrearPlanDesdeSeleccion));
            acciones.Controls.Add(btnGenerar);
            acciones.Controls.Add(btnCrear);
            Estilo.EstilizarBoton(btnGenerar); Compacto(btnGenerar);
            Estilo.EstilizarBoton(btnCrear); Compacto(btnCrear);
            _resumenSelNuevo = new Label { AutoSize = false, Width = 240, Height = 28, Margin = new Padding(UI.Gap, 5, 0, 0), Font = Estilo.Normal(9f), ForeColor = Estilo.Gris700, TextAlign = ContentAlignment.MiddleLeft };
            acciones.Controls.Add(_resumenSelNuevo);

            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            cont.Controls.Add(card);
            cont.Controls.Add(_hallazgosNuevo);
            cont.Controls.Add(acciones);
            _contenido.Controls.Add(cont);

            RenderRecsGeneradas(ContextoPlanificacion.RecsPeriodo);
        }

        private void GenerarRecomendaciones()
        {
            ContextoPlanificacion.RecsPeriodo = null;
            var recs = ContextoPlanificacion.ObtenerRecomendaciones(Analitica);
            RenderRecsGeneradas(recs);
            Estilo.Info(this, I18n.T("eh.pln.sec.recomperiodo", "Recomendaciones del período"),
                string.Format(I18n.T("eh.pln.rec.cuenta", "Se generaron {0} recomendación(es)."), recs.Count));
        }

        private void RenderRecsGeneradas(List<BE.RecomendacionGenerada> recs)
        {
            if (_flowNuevo == null) return;   // la vista ya no está montada (se cambió de etapa mientras tanto)
            _flowNuevo.Controls.Clear();
            _seleccionNuevo.Clear();
            bool hay = recs != null && recs.Count > 0;
            _vacioNuevo.Visible = !hay;
            _flowNuevo.Visible = hay;

            if (recs == null)
            {
                _hallazgosNuevo.Text = I18n.T("eh.pln.decisiones.ctx", "Generá las recomendaciones para analizar el período vigente.");
                return;
            }
            _hallazgosNuevo.Text = ResumenHallazgosNuevo(recs);
            if (!hay) return;

            int cols = Cuerpo.ClientSize.Width >= 1000 ? 2 : 1;
            int ancho = Math.Max(380, (_flowNuevo.ClientSize.Width - UI.GapL * (cols + 1)) / cols);
            foreach (var r in recs) _flowNuevo.Controls.Add(TarjetaRecNueva(r, ancho));
            ActualizarResumenSeleccionNuevo();
        }

        private void ActualizarResumenSeleccionNuevo()
        {
            if (_resumenSelNuevo == null) return;
            int n = _seleccionNuevo.Count(x => x.Chk.Checked);
            _resumenSelNuevo.Text = n == 0
                ? I18n.T("eh.pln.sel.ninguna", "Ninguna recomendación seleccionada.")
                : string.Format(I18n.T("eh.pln.sel.cuenta", "{0} seleccionada(s) para el plan."), n);
        }

        private string ResumenHallazgosNuevo(List<BE.RecomendacionGenerada> recs)
        {
            if (recs.Count == 0) return I18n.T("eh.pln.decisiones.sinhallazgos", "Sin hallazgos: los indicadores están dentro de los umbrales.");
            var porTipo = recs.GroupBy(r => r.Tipo).Select(gr => $"{gr.Count()} {T.Tipo(gr.Key)}");
            return string.Format(I18n.T("eh.pln.decisiones.hallazgos", "{0} recomendación(es): "), recs.Count) + string.Join(" · ", porTipo);
        }

        private TarjetaPanel TarjetaRecNueva(BE.RecomendacionGenerada r, int ancho)
        {
            var card = new TarjetaPanel { Radio = 10, Width = ancho, Height = 168, RellenoColor = Estilo.Lienzo, BackColor = Estilo.Lienzo, BordeColor = Estilo.Gris200, Margin = new Padding(0, 0, UI.GapL, UI.GapL), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.Gap), FranjaAncho = 4, FranjaColor = T.ColorTipo(r.Tipo) };

            var fila = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Estilo.Lienzo };
            var chk = new CheckBox { Dock = DockStyle.Left, Width = 26, BackColor = Estilo.Lienzo };
            chk.CheckedChanged += (s, e) => ActualizarResumenSeleccionNuevo();
            var tipo = new Label { Dock = DockStyle.Fill, Text = T.Tipo(r.Tipo), Font = Estilo.Bold(8.5f), ForeColor = T.ColorTipo(r.Tipo), TextAlign = ContentAlignment.MiddleLeft };
            fila.Controls.Add(tipo);
            fila.Controls.Add(chk);

            var ent = new Label { Dock = DockStyle.Top, Height = 24, Text = r.NombreEntidad, Font = Estilo.SemiBold(11f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true };
            var acc = new Label { Dock = DockStyle.Top, Height = 22, Text = I18n.T("eh.pln.rec.accion", "Acción sugerida") + ": " + T.Accion(r.AccionSugerida), Font = Estilo.Normal(9.5f), ForeColor = Estilo.Gris700, AutoEllipsis = true };
            var evi = new Label { Dock = DockStyle.Fill, Text = I18n.T("eh.pln.rec.evidencia", "Evidencia") + ": " + r.Evidencia, Font = Estilo.Normal(9f), ForeColor = Estilo.Gris700 };

            card.Controls.Add(evi);
            card.Controls.Add(acc);
            card.Controls.Add(ent);
            card.Controls.Add(fila);
            _seleccionNuevo.Add((chk, r));
            return card;
        }

        private void CrearPlanDesdeSeleccion()
        {
            var elegidas = _seleccionNuevo.Where(x => x.Chk.Checked).Select(x => x.Rec).ToList();
            if (elegidas.Count == 0)
            {
                Estilo.Info(this, I18n.T("eh.pln.crearplansel", "Crear plan con las seleccionadas"), I18n.T("eh.pln.sinseleccion", "Seleccioná al menos una recomendación para el plan."));
                return;
            }
            using (var dlg = new InputDialog(I18n.T("eh.btn.crearplan", "Crear plan de acción"), I18n.T("eh.pln.tituloplan", "Nombre del plan de acción:"), false))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.InputText)) return;
                int? idGerente = null;
                try { idGerente = new BLL.Usuario().ObtenerUsuarioActivo()?.Id; } catch { }
                int idPlan = _svc.CrearPlan(dlg.InputText.Trim(), Tablero(), elegidas,
                    ContextoPlanificacion.Ciudad, ContextoPlanificacion.Categoria, ContextoPlanificacion.Organizador, idGerente);

                _modoNuevoPlan = false;
                if (Filtros != null) Filtros.Visible = false;
                ContextoPlanificacion.PlanSeleccionadoId = idPlan;
                // Plan recién creado = Borrador con TODAS sus recomendaciones en Pendiente (CrearPlan no
                // decide ninguna): aterrizar en "Tareas" las mostraría vacías. Se queda en "Recomendaciones",
                // que es donde hay algo concreto para hacer (aprobar/descartar cada una).
                _etapa = ProcesoPlanificacion.Analizar;
                Estilo.Exito(this, I18n.T("eh.btn.crearplan", "Crear plan de acción"), string.Format(I18n.T("eh.pln.creado", "Plan #{0} creado en Borrador."), idPlan));
                RenderPlanes();
                SeleccionarPorId(idPlan);   // fuerza la selección sin importar el chip de filtro (Activos/Finalizados/Todos) activo
            }
        }

        // ── Franja FIJA de "Próxima acción" ─────────────────────────────────────────
        // Siempre en el mismo lugar (debajo del stepper), independiente de qué etapa se esté
        // consultando: refleja el ESTADO REAL del plan, no la sección que se está mirando.
        //   • Si hay una transición disponible y el usuario tiene el permiso → título + botón único.
        //   • Si hay una transición disponible pero el usuario NO tiene el permiso → título, sin botón,
        //     con una aclaración de que falta permiso (nunca un botón que al tocarlo falle solo).
        //   • Si no hay ninguna transición todavía (p. ej. período de observación en curso) → el motivo.
        //   • Debajo, una línea de contexto informativa (recomendaciones/tareas pendientes) que NUNCA
        //     bloquea: la BLL no exige decidir ni ejecutar todo para avanzar de estado.
        private void ConstruirFranjaAccion(BE.PlanAccion p, List<BE.RecomendacionPlan> recs)
        {
            bool tieneEvaluacion = _svc.TieneEvaluacion(p.IdPlanAccion);
            var (titulo, tagBoton, textoBoton) = T.ProximaAccion(p, tieneEvaluacion, DateTime.Now);
            string patenteReq = PatenteDeTransicion(tagBoton);
            bool tienePermiso = patenteReq == null || Puede(patenteReq);

            var tarjeta = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 8, RellenoColor = Estilo.AzulDeFondo, BackColor = Color.White, BordeColor = Estilo.Gris200, Padding = new Padding(UI.GapL, 6, UI.Gap, 6) };

            var textos = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var (contexto, esAviso) = ContextoFranja(p, recs);
            if (!tienePermiso)
            {
                contexto = I18n.T("eh.pln.franja.sinpermiso", "No tenés permiso para esta acción; consultá con un Administrador.");
                esAviso = false;
            }
            if (!string.IsNullOrEmpty(contexto))
                textos.Controls.Add(new Label { Dock = DockStyle.Top, Height = 18, Text = contexto, Font = Estilo.Normal(8.5f), ForeColor = esAviso ? Estilo.Naranja : Estilo.Gris700, AutoEllipsis = true });
            textos.Controls.Add(new Label { Dock = DockStyle.Top, Height = 20, Text = titulo, Font = Estilo.SemiBold(10f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true });
            tarjeta.Controls.Add(textos);

            if (tagBoton != null && tienePermiso)
            {
                // El botón se arma DESPUÉS del OnLoad del form (en cada repintado de la franja), así
                // que no pasa por el recorrido único de Estilo.Aplicar → hay que estilizarlo a mano
                // (mismo motivo que Estilo.EstilizarGrid en ConstruirMaestroDetalle). Se envuelve en un
                // FlowLayoutPanel (en vez de Dock=Right directo sobre el botón) para que
                // EstilizarBoton lo AUTO-DIMENSIONE a su texto: un Button con Dock=Right dentro de un
                // panel común se ESTIRA verticalmente a la altura del contenedor (eso hacía que el
                // botón de la franja se viera gigante).
                var btn = UI.Primario(textoBoton, tagBoton, (s, e) => Try(() => EjecutarProximaAccion(tagBoton)));
                var btnWrap = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, 3, 0, 0) };
                btnWrap.Controls.Add(btn);
                Estilo.EstilizarBoton(btn);
                Compacto(btn);
                tarjeta.Controls.Add(btnWrap);
            }

            _franjaAccion.Controls.Add(tarjeta);
        }

        // Patente REAL que exige cada transición en BLL.PlanAccionService — "Marcar en ejecución" usa
        // PlanAccionEjecutar; las otras tres usan PlanAccionAprobar. Tal cual están hoy las guardas de
        // BLL (AprobarPlan/MarcarEnEjecucion/CerrarPlan/EvaluarResultados): no se inventa ninguna nueva.
        private static string PatenteDeTransicion(string tagBoton)
        {
            switch (tagBoton)
            {
                case "eh.btn.aprobarplan":
                case "eh.btn.cerrarplan":
                case "eh.btn.evaluarresultados": return BE.Patentes.PlanAccionAprobar;
                case "eh.btn.enejecucion":       return BE.Patentes.PlanAccionEjecutar;
                default:                          return null;   // sin botón → no hay permiso que validar
            }
        }

        // Línea de contexto bajo el titular: SOLO informativo, nunca bloquea (ninguna de estas cuentas
        // impide la transición en BLL — verificado contra PlanAccionService.CerrarPlan: solo exige
        // PuedeCerrar(), nunca "sin tareas pendientes"). Prioriza lo relevante al momento: tareas sin
        // ejecutar antes de cerrar; recomendaciones sin decidir mientras el plan todavía puede
        // aprobarse/ejecutarse. `esAviso` distingue "dato neutro" de "ojo con esto antes de seguir" —
        // la franja lo pinta distinto para que no se lea como una contradicción con el botón disponible.
        private static (string Texto, bool EsAviso) ContextoFranja(BE.PlanAccion p, List<BE.RecomendacionPlan> recs)
        {
            int pendDec = recs.Count(r => r.Decision == BE.DecisionRecomendacion.Pendiente);
            int aprobSinEjec = recs.Count(r => r.Decision == BE.DecisionRecomendacion.Aprobada && !r.Ejecutada);
            if (p.PuedeCerrar() && aprobSinEjec > 0)
                return (string.Format(I18n.T("eh.pln.franja.aprobsinejec", "Podés cerrar igual: quedan {0} tarea(s) aprobada(s) sin ejecutar (se van a poder seguir ejecutando después de cerrar)."), aprobSinEjec), true);
            if (pendDec > 0 && (p.Estado == BE.EstadoPlanAccion.Borrador || p.Estado == BE.EstadoPlanAccion.Aprobado))
                return (string.Format(I18n.T("eh.pln.franja.pend", "{0} recomendación(es) todavía sin decidir. No hace falta decidirlas para aprobar el plan."), pendDec), false);
            return (null, false);
        }

        // Dispara la transición de estado del botón de la franja. _accionEnCurso bloquea una segunda
        // entrada mientras la primera está en curso (confirmación modal + llamada a BLL) y se libera
        // en el finally SIEMPRE — incluso si el usuario cancela la confirmación — así nunca queda
        // "pegado" sin poder reintentar.
        private void EjecutarProximaAccion(string tagBoton)
        {
            if (_accionEnCurso) return;
            _accionEnCurso = true;
            try
            {
                switch (tagBoton)
                {
                    case "eh.btn.aprobarplan":       AprobarPlan(); break;
                    case "eh.btn.enejecucion":       MarcarEnEjecucion(); break;
                    case "eh.btn.cerrarplan":        CerrarPlan(); break;
                    case "eh.btn.evaluarresultados": EvaluarResultados(); break;
                }
            }
            finally { _accionEnCurso = false; }
        }

        // ── Encabezado — UNA sola línea de confirmación, no un segundo resumen ─────────────────
        // Antes este bloque repetía, en 3 renglones, información que YA está visible en la tarjeta
        // seleccionada de la lista de planes (título, período, estado — la tarjeta activa queda
        // resaltada con borde azul a la izquierda, siempre a la vista) y además mostraba 5 chips de
        // conteo (recomendaciones/ejecutadas/por ejecutar/descartadas/por decidir) que YA comunican la
        // franja de "Próxima acción" (texto contextual) y el badge del stepper — una tercera fuente
        // para el mismo dato. Repetirlo ahí no agregaba información: solo ocupaba ~84px que le hacían
        // falta a la grilla/detalle, que es donde pasa el trabajo real.
        //
        // Lo único que ESTE bloque aporta que no está en ningún otro lado es la confirmación rápida de
        // "qué plan estoy gestionando" al bajar la vista desde el stepper/franja, sin tener que mirar
        // de nuevo a la lista — por eso se mantiene como una ÚNICA línea (título + estado), no se
        // elimina. "Período analizado" y "Creado" se sacaron del todo (están en la tarjeta de la
        // izquierda); el total de recomendaciones pasó a vivir en el título de sección de la grilla
        // ("Recomendaciones (28)" / "Tareas del plan (27)"), que es donde el número es accionable.
        private void ConstruirEncabezado(BE.PlanAccion p, List<BE.RecomendacionPlan> recs)
        {
            var btnPdf = UI.Secundario(I18n.T("eh.btn.exportarpdf", "Exportar PDF"), "eh.btn.exportarpdf", (s, e) => Try(ExportarPdf));
            var acciones = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
            acciones.Controls.Add(btnPdf);
            Estilo.EstilizarBoton(btnPdf);   // se arma post-OnLoad: Estilo.Aplicar ya no lo recorre.
            Compacto(btnPdf);

            var fila = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            fila.Controls.Add(new Label { AutoSize = true, Text = "#" + p.IdPlanAccion + " · " + p.Titulo, Font = Estilo.SemiBold(12.5f), ForeColor = Estilo.AzulOscuro, Margin = new Padding(0, 7, 10, 0) });
            var pillEstado = T.Pill(T.EstadoPlan(p.Estado), T.ColorEstadoPlan(p.Estado));
            pillEstado.Margin = new Padding(0, 9, 0, 0);
            fila.Controls.Add(pillEstado);

            var linea = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Estilo.Gris200 };

            _encabezadoHost.Controls.Add(fila);
            _encabezadoHost.Controls.Add(acciones);
            _encabezadoHost.Controls.Add(linea);
        }

        // ── Vista 1 · Recomendaciones (decidir) ───────────────────────────────────
        // Mismo patrón que CentroDecisionesForm de WardrobeFlow (GUI\CentroDecisionesForm.cs, pestaña
        // "2. Recomendaciones"): grilla maestra a la izquierda + detalle/acciones a la derecha, en vez
        // de tarjetas en flujo. Es más fácil de escanear (una fila por recomendación, ancho fijo) y
        // deja ver el detalle completo de UNA por vez, sin competir por espacio con las demás.
        private void ConstruirVistaAnalizar(List<BE.RecomendacionPlan> recs)
        {
            var tit = UI.TituloSeccion(I18n.T("eh.pln.vista.decidir", "Recomendaciones y decisiones") + $" ({recs.Count})", "eh.pln.vista.decidir");
            tit.Dock = DockStyle.Top;
            var cuerpo = ConstruirMaestroDetalle(recs, ModoRec.Decision);
            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            cont.Controls.Add(cuerpo);
            cont.Controls.Add(tit);
            _contenido.Controls.Add(cont);
        }

        // ── Vista 2 · Tareas (ejecutar) ────────────────────────────────────────────
        private void ConstruirVistaEjecutar(List<BE.RecomendacionPlan> recs)
        {
            // Tareas = recomendaciones APROBADAS (ejecutadas o pendientes de ejecución).
            var tareas = recs.Where(r => r.Decision == BE.DecisionRecomendacion.Aprobada).ToList();
            int pendDec = recs.Count(r => r.Decision == BE.DecisionRecomendacion.Pendiente);
            var tit = UI.TituloSeccion(I18n.T("eh.pln.vista.tareas", "Tareas del plan") + $" ({tareas.Count})", "eh.pln.vista.tareas");
            tit.Dock = DockStyle.Top;

            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            if (pendDec > 0)
            {
                var aviso = new Label { Dock = DockStyle.Top, Height = 22, Text = string.Format(I18n.T("eh.pln.ejec.penddecidir", "Hay {0} recomendación(es) sin decidir en «Recomendaciones»."), pendDec), Font = Estilo.Crear(9f, FontStyle.Italic), ForeColor = Estilo.Naranja };
                cont.Controls.Add(aviso);
            }
            if (tareas.Count == 0)
            {
                var vacio = new Label { Dock = DockStyle.Fill, Text = I18n.T("eh.pln.ejec.sintareas", "No hay tareas aprobadas. Aprobá recomendaciones en «Recomendaciones»."), ForeColor = Estilo.Gris400, Font = Estilo.Normal(9.5f), TextAlign = ContentAlignment.MiddleCenter };
                cont.Controls.Add(vacio);
            }
            else
            {
                cont.Controls.Add(ConstruirMaestroDetalle(tareas, ModoRec.Ejecucion));
            }
            cont.Controls.Add(tit);
            _contenido.Controls.Add(cont);
        }

        // ── Vista 3 · Evaluar resultados ──────────────────────────────────────────
        // Acá YA NO hay nada que decidir (son decisiones firmes, pasadas): una sola grilla de lectura
        // a todo el ancho, igual que la pestaña "3. Historial de decisiones" de WardrobeFlow — sin
        // panel de detalle lateral, porque no hace falta elegir una fila para actuar sobre ella.
        private void ConstruirVistaEvaluar(List<BE.RecomendacionPlan> recs)
        {
            var snaps = _svc.ObtenerComparativo(_sel.IdPlanAccion) ?? new List<BE.KpiSnapshot>();
            bool hayDespues = snaps.Any(s => s.Momento == BE.MomentoKpi.Despues);
            var titL = UI.TituloSeccion(I18n.T("eh.pln.sec.decisiones", "Decisiones y acciones del plan"), "eh.pln.sec.decisiones");
            titL.Dock = DockStyle.Top;

            var lectura = new Panel { Dock = DockStyle.Top, Height = 260, BackColor = Color.White, Padding = new Padding(0, UI.Gap, 0, UI.Gap) };
            if (recs.Count == 0)
                lectura.Controls.Add(new Label { Dock = DockStyle.Fill, Text = I18n.T("eh.pln.vacio.recplan", "Este plan no tiene recomendaciones asociadas."), ForeColor = Estilo.Gris400, Font = Estilo.Normal(9.5f), TextAlign = ContentAlignment.MiddleCenter });
            else
                lectura.Controls.Add(ConstruirGrillaLectura(recs));
            lectura.Controls.Add(titL);

            // Orden visual (de abajo hacia arriba se agrega; el ÚLTIMO Dock.Top agregado queda arriba
            // de todo — mismo criterio ya usado en ConstruirDetalleRec): detalle de decisiones al
            // fondo, comparativo REAL en el medio, y el resumen SIMULADO —si corresponde— arriba de
            // todo, porque es el contenido que más le importa al usuario en esta etapa.
            _contenido.Controls.Add(lectura);
            _contenido.Controls.Add(ConstruirSeguimiento(_sel, snaps));
            // El panel simulado es un ADELANTO antes de que exista la evaluación real — una vez que
            // ÉSTA existe (hayDespues), el dato real manda y no se mezcla con la proyección ficticia.
            if (!hayDespues)
                _contenido.Controls.Add(ConstruirImpactoSimulado(_sel, recs));
        }

        // ── Maestro-detalle de recomendaciones (patrón CentroDecisionesForm/WardrobeFlow) ─────────
        // Grilla ARRIBA (todo el ancho útil) + detalle de la seleccionada ABAJO (también a todo el
        // ancho). Antes la grilla y el detalle iban lado a lado (280px cada uno aprox.) y NINGUNO de
        // los dos tenía ancho real: la grilla truncaba cada columna ("List...", "Program...", "Apr...")
        // y el detalle quedaba angosto con scroll interno. Apilados, la grilla usa TODO el ancho del
        // panel principal (columnas Fill → se reparten ese ancho entero) y el detalle también, así que
        // puede mostrar sus secciones en una franja horizontal (ribbon) en vez de una columna angosta.
        //
        // NO usa SplitContainer: Panel1MinSize/Panel2MinSize + SplitterDistance de SplitContainer
        // exigen que el espacio disponible sea >= esos mínimos en TODO momento del layout — si en algún
        // punto (primer render, ventana angosta) no alcanza, el control tira ArgumentException DESDE SU
        // PROPIO motor de layout (no en una línea que uno pueda envolver en try/catch). Eso fue lo que
        // rompía la pantalla al abrir un plan. En cambio, panel fijo arriba (Dock.Top) + panel de
        // detalle abajo (Dock.Fill), sin esa clase de límite.
        private Control ConstruirMaestroDetalle(List<BE.RecomendacionPlan> recs, ModoRec modo)
        {
            var grid = UI.Grilla();
            Estilo.EstilizarGrid(grid);   // se arma DESPUÉS del OnLoad del form: no pasa por Estilo.Aplicar.
            ConstruirColumnasRecs(grid, incluirMotivo: false);
            grid.DataSource = ArmarTablaRecs(recs, incluirMotivo: false);
            grid.CellFormatting += (s, e) => ColorearFilaPorDecision(grid, recs, e);

            // Alto fijo para ~4 filas visibles (header 40 + 4×32) — el resto lo scrollea la propia
            // grilla (barra vertical nativa). Se achicó de 262 a 208 para dejarle más alto real al
            // detalle de abajo (antes quedaba debajo del borde visible en pantallas de 720px).
            var gridPanel = new TarjetaPanel { Dock = DockStyle.Top, Height = 208, Radio = 8, BackColor = Color.White, BordeColor = Estilo.Gris200, Padding = new Padding(1), Margin = new Padding(0, 0, 0, UI.Gap) };
            gridPanel.Controls.Add(grid);

            var detalleHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0, UI.Gap, 0, 0) };
            void Refrescar()
            {
                int idx = grid.CurrentCell?.RowIndex ?? (grid.Rows.Count > 0 ? 0 : -1);
                var sel = idx >= 0 && idx < recs.Count ? recs[idx] : null;
                detalleHost.Controls.Clear();
                detalleHost.Controls.Add(ConstruirDetalleRec(sel, modo));
            }
            grid.SelectionChanged += (s, e) => Refrescar();

            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            host.Controls.Add(detalleHost);   // Fill se agrega PRIMERO (ocupa lo que sobra, abajo).
            host.Controls.Add(gridPanel);     // Top se agrega DESPUÉS (reserva su franja fija, arriba).

            if (grid.Rows.Count > 0) grid.Rows[0].Selected = true;
            Refrescar();
            return host;
        }

        // Grilla de SOLO LECTURA a todo el ancho (vista Evaluar): sin panel de detalle lateral, por
        // eso acá SÍ incluye la columna Motivo (no hay otro lugar donde mostrarla).
        private DataGridView ConstruirGrillaLectura(List<BE.RecomendacionPlan> recs)
        {
            var grid = UI.Grilla();
            Estilo.EstilizarGrid(grid);
            ConstruirColumnasRecs(grid, incluirMotivo: true);
            grid.DataSource = ArmarTablaRecs(recs, incluirMotivo: true);
            grid.CellFormatting += (s, e) => ColorearFilaPorDecision(grid, recs, e);
            return grid;
        }

        private System.Data.DataTable ArmarTablaRecs(List<BE.RecomendacionPlan> recs, bool incluirMotivo)
        {
            var t = new System.Data.DataTable();
            t.Columns.Add("colTipo", typeof(string));
            t.Columns.Add("colEntidad", typeof(string));
            t.Columns.Add("colAccion", typeof(string));
            t.Columns.Add("colDecision", typeof(string));
            t.Columns.Add("colEjecutada", typeof(string));
            if (incluirMotivo) t.Columns.Add("colMotivo", typeof(string));
            foreach (var r in recs)
            {
                string ejecutada = r.Ejecutada ? I18n.T("eh.pln.rec.si", "Sí") : "—";
                if (incluirMotivo)
                    t.Rows.Add(T.Tipo(r.Tipo), r.NombreEntidad, T.Accion(r.AccionSugerida), T.Decision(r.Decision), ejecutada, r.MotivoDecision ?? "");
                else
                    t.Rows.Add(T.Tipo(r.Tipo), r.NombreEntidad, T.Accion(r.AccionSugerida), T.Decision(r.Decision), ejecutada);
            }
            return t;
        }

        private void ConstruirColumnasRecs(DataGridView grid, bool incluirMotivo)
        {
            grid.AutoGenerateColumns = true;   // sobre el DataTable — los headers se fijan tras bindear.
            grid.DataBindingComplete += (s, e) =>
            {
                if (grid.Columns["colTipo"] == null) return;
                grid.Columns["colTipo"].HeaderText = I18n.T("eh.pln.col.tipo", "Tipo");
                grid.Columns["colEntidad"].HeaderText = I18n.T("eh.pln.col.entidad", "Entidad");
                grid.Columns["colAccion"].HeaderText = I18n.T("eh.pln.rec.accion", "Acción sugerida");
                grid.Columns["colDecision"].HeaderText = I18n.T("eh.pln.col.decision", "Decisión");
                grid.Columns["colEjecutada"].HeaderText = I18n.T("eh.pln.rec.ejecutada", "Ejecutada");
                grid.Columns["colTipo"].FillWeight = 16;
                grid.Columns["colEntidad"].FillWeight = 26;
                grid.Columns["colAccion"].FillWeight = 26;
                grid.Columns["colDecision"].FillWeight = 16;
                grid.Columns["colEjecutada"].FillWeight = 12;
                if (incluirMotivo && grid.Columns["colMotivo"] != null)
                {
                    grid.Columns["colMotivo"].HeaderText = I18n.T("eh.pln.rec.motivo", "Motivo");
                    grid.Columns["colMotivo"].FillWeight = 30;
                }
            };
        }

        // Tiñe la fila completa según la Decisión (mismo criterio que CentroDecisionesForm de
        // WardrobeFlow, que colorea por Prioridad: acá no hay prioridad, así que se usa Decisión —
        // Pendiente en amarillo suave, Aprobada en verde suave, Descartada en gris).
        private void ColorearFilaPorDecision(DataGridView grid, List<BE.RecomendacionPlan> recs, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= recs.Count) return;
            var r = recs[e.RowIndex];
            Color fondo = r.Decision == BE.DecisionRecomendacion.Aprobada ? ColorTranslator.FromHtml("#EAF6EC")
                        : r.Decision == BE.DecisionRecomendacion.Descartada ? Estilo.Gris100
                        : ColorTranslator.FromHtml("#FFF8E6");
            e.CellStyle.BackColor = fondo;
        }

        // Panel de detalle de UNA recomendación: toda la info + la acción disponible, según el modo
        // (Decidir: Aprobar/Descartar · Ejecutar: Ejecutar tarea · Lectura: sin acciones). Ahora tiene
        // TODO el ancho del área principal (la grilla pasó a vivir arriba, no al costado), así que las
        // secciones se arman en RIBBON horizontal (tarjetas una al lado de la otra, con ajuste de línea
        // si no entran) en vez de una columna angosta apilada con scroll interno.
        private Control ConstruirDetalleRec(BE.RecomendacionPlan r, ModoRec modo)
        {
            var host = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 8, BackColor = Color.White, BordeColor = Estilo.Gris200, Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.Gap) };
            if (r == null)
            {
                host.Controls.Add(new Label { Dock = DockStyle.Fill, Text = I18n.T("eh.pln.rec.sinsel", "Seleccioná una recomendación de la lista para ver el detalle."), ForeColor = Estilo.Gris400, Font = Estilo.Normal(9.5f), TextAlign = ContentAlignment.MiddleCenter });
                return host;
            }

            // ── Fila superior: entidad + chips de estado a la izquierda, acción disponible a la
            // derecha — EN LA MISMA fila (no un renglón de acciones aparte al fondo): el usuario ve
            // qué está mirando y qué puede hacer con eso de un solo vistazo, sin bajar la vista.
            var fila1 = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.White, Margin = new Padding(0, 0, 0, UI.Gap) };
            var acciones = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.White };
            if (modo == ModoRec.Decision && r.PuedeDecidir() && Puede(BE.Patentes.PlanAccionAprobar))
            {
                var btnAprobar = UI.Primario(I18n.T("eh.btn.aprobar", "Aprobar"), "eh.btn.aprobar", (s, e) => Try(() => Decidir(r, true)));
                var btnDescartar = UI.Secundario(I18n.T("eh.btn.descartar", "Descartar"), "eh.btn.descartar", (s, e) => Try(() => Decidir(r, false)));
                acciones.Controls.Add(btnAprobar);
                acciones.Controls.Add(btnDescartar);
                // Se arman post-OnLoad (panel de detalle reconstruido en cada selección): Estilo.Aplicar
                // ya no los recorre, hay que estilizarlos a mano.
                Estilo.EstilizarBoton(btnAprobar);
                Estilo.EstilizarBoton(btnDescartar);
                Compacto(btnAprobar);
                Compacto(btnDescartar);
            }
            else if (modo == ModoRec.Ejecucion && r.PuedeEjecutar() && Puede(BE.Patentes.PlanAccionEjecutar))
            {
                bool destr = T.AccionDestructiva(r.AccionSugerida);
                var btnEjecutar = destr
                    ? UI.Peligro(I18n.T("eh.btn.ejecutar", "Ejecutar tarea"), "eh.btn.ejecutar", (s, e) => Try(() => Ejecutar(r)))
                    : UI.Primario(I18n.T("eh.btn.ejecutar", "Ejecutar tarea"), "eh.btn.ejecutar", (s, e) => Try(() => Ejecutar(r)));
                acciones.Controls.Add(btnEjecutar);
                Estilo.EstilizarBoton(btnEjecutar);
                Compacto(btnEjecutar);
            }

            var infoIzq = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.White, AutoScroll = false };
            infoIzq.Controls.Add(new Label { AutoSize = true, Text = r.NombreEntidad, Font = Estilo.SemiBold(12f), ForeColor = Estilo.AzulOscuro, Margin = new Padding(0, 6, 14, 0) });
            var pTipo = T.Pill(T.Tipo(r.Tipo), T.ColorTipo(r.Tipo)); pTipo.Margin = new Padding(0, 6, 6, 0);
            var pDec = T.Pill(T.Decision(r.Decision), T.ColorDecision(r.Decision)); pDec.Margin = new Padding(0, 6, 6, 0);
            infoIzq.Controls.Add(pTipo);
            infoIzq.Controls.Add(pDec);
            if (r.Ejecutada)
            {
                var pEjec = T.Pill(I18n.T("eh.pln.rec.ejecutada", "Ejecutada"), Estilo.Verde); pEjec.Margin = new Padding(0, 6, 6, 0);
                infoIzq.Controls.Add(pEjec);
            }
            fila1.Controls.Add(infoIzq);
            fila1.Controls.Add(acciones);

            var linea = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Estilo.Gris200, Margin = new Padding(0, 0, 0, UI.Gap) };

            // ── Ribbon de secciones: tarjetas lado a lado (se ajustan de línea si no entran), cada
            // una con ancho fijo y alto AUTOMÁTICO según su texto — así el texto largo se AJUSTA
            // (wrap) en vez de cortarse o forzar un scroll interno.
            var ribbon = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.White };

            Control Tarjeta(string titulo, string texto, Color? color = null)
            {
                var panel = new Panel { Width = 300, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White, Margin = new Padding(0, 0, UI.GapL, UI.Gap) };
                var cap = new Label { Dock = DockStyle.Top, Height = 16, Font = Estilo.SemiBold(8.5f), ForeColor = Estilo.AzulOscuro, Text = titulo };
                var lbl = new Label { Dock = DockStyle.Top, AutoSize = true, MaximumSize = new Size(300, 0), Font = Estilo.Normal(9f), ForeColor = color ?? Estilo.Gris700, Text = texto };
                panel.Controls.Add(lbl); panel.Controls.Add(cap);
                return panel;
            }

            ribbon.Controls.Add(Tarjeta(I18n.T("eh.pln.rec.accion", "Acción sugerida"), T.Accion(r.AccionSugerida)));
            ribbon.Controls.Add(Tarjeta(I18n.T("eh.pln.rec.evidencia", "Evidencia"), r.Evidencia));
            if (r.Ejecutada && r.FechaEjecucion.HasValue)
                ribbon.Controls.Add(Tarjeta(I18n.T("eh.pln.rec.fechaejec", "Ejecutada el"), r.FechaEjecucion.Value.ToString("dd/MM/yyyy HH:mm")));
            if (!string.IsNullOrWhiteSpace(r.MotivoDecision))
                ribbon.Controls.Add(Tarjeta(I18n.T("eh.pln.rec.motivo", "Motivo"), r.MotivoDecision));

            host.Controls.Add(ribbon);
            host.Controls.Add(linea);
            host.Controls.Add(fila1);
            return host;
        }

        // ── Comparativo Antes/Después + conclusiones (tabla estructurada, filas alternadas) ──
        // Distingue EXPLÍCITAMENTE 3 situaciones (sin mostrar nunca una caída falsa por falta de datos):
        //   1) Cerrado, ventana de observación NO cumplida  → "evaluable a partir de <fecha>" (sin tabla).
        //   2) Cerrado, ventana cumplida, SIN evaluar todavía → invita a tocar "Evaluar resultados" (sin tabla).
        //   3) Evaluado (existe snapshot Después)             → tabla completa + clasificación por indicador.
        private Control ConstruirSeguimiento(BE.PlanAccion p, List<BE.KpiSnapshot> snaps)
        {
            var cont = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White, Padding = new Padding(0, UI.Gap, 0, UI.GapL) };
            var tit = UI.TituloSeccion(I18n.T("eh.pln.sec.seguimiento", "Seguimiento: Antes / Después"), "eh.pln.sec.seguimiento");
            var antes = snaps.Where(s => s.Momento == BE.MomentoKpi.Antes).ToDictionary(s => s.Clave, s => s.Valor);
            var despues = snaps.Where(s => s.Momento == BE.MomentoKpi.Despues).ToDictionary(s => s.Clave, s => s.Valor);
            bool hayDespues = despues.Count > 0;
            bool periodoCumplido = p.PeriodoObservacionCumplido(DateTime.Now);

            string periodoAntes = p.PeriodoDesde.ToString("dd/MM/yy") + "–" + p.PeriodoHasta.ToString("dd/MM/yy");

            if (!hayDespues)
            {
                // Estados 1 y 2: SIN tabla (no hay "Después" real todavía) — mensaje explícito, nunca ceros/caídas falsas.
                cont.Controls.Add(new Label { Dock = DockStyle.Top, Height = 18, Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris700,
                    Text = I18n.T("eh.pln.seg.antes", "Antes") + ": " + periodoAntes });
                // El período de observación solo arranca a contarse DESDE EL CIERRE (FechaEvaluacionPrevista
                // = FechaCierre + duración): un plan que todavía NO se cerró no tiene ningún período "en
                // curso" — antes acá se mostraba igual "Período de observación en curso... a partir del —"
                // (con un guión en vez de fecha, porque FechaCierre es null), dando a entender que ya había
                // un cronómetro corriendo cuando en realidad ni siquiera se cerró el plan todavía.
                string msj;
                Color colorMsj;
                if (p.Estado != BE.EstadoPlanAccion.Cerrado)
                {
                    msj = I18n.T("eh.pln.seg.sincerrar", "El comparativo Antes/Después se habilita al cerrar el plan: ahí arranca a contarse el período de observación.");
                    colorMsj = Estilo.Gris700;
                }
                else if (!periodoCumplido)
                {
                    msj = string.Format(I18n.T("eh.pln.seg.pendiente.fecha", "Período de observación en curso. Resultados evaluables a partir del {0}."),
                        p.FechaEvaluacionPrevista?.ToString("dd/MM/yyyy") ?? "—");
                    colorMsj = Estilo.Naranja;
                }
                else
                {
                    msj = I18n.T("eh.pln.seg.listo", "El período de observación ya transcurrió: usá «Evaluar resultados» para generar el comparativo.");
                    colorMsj = Estilo.AzulDeSeleccion;
                }
                cont.Controls.Add(new Label { Dock = DockStyle.Top, Height = 34, Text = msj, Font = Estilo.Crear(9f, FontStyle.Italic), ForeColor = colorMsj });
                cont.Controls.Add(tit);
                return cont;
            }

            // Estado 3: evaluado — tabla completa con período Después real (desde el cierre, ya transcurrido).
            string periodoDespues = p.FechaCierre.Value.ToString("dd/MM/yy") + "–" + p.FechaCierre.Value.AddDays(p.DuracionPeriodoDias).ToString("dd/MM/yy");

            var tl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 6, BackColor = Color.White, Margin = new Padding(0) };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            Fila(tl, I18n.T("eh.pln.seg.indicador", "Indicador"), I18n.T("eh.pln.seg.antes", "Antes"), I18n.T("eh.pln.seg.despues", "Después"), "Δ", "%",
                I18n.T("eh.pln.seg.resultado", "Resultado"), true, false, Color.White);

            string[] orden = { "EnEspera", "OfertasVencidas", "CancelacionesATiempo", "CancelacionesTardias", "Inasistencias", "Altas", "Renovaciones", "IngresosPlanes" };
            int mejoraron = 0, empeoraron = 0, sinCambio = 0, fi = 0;
            foreach (var clave in orden)
            {
                if (!antes.ContainsKey(clave) && !despues.ContainsKey(clave)) continue;
                bool money = clave == "IngresosPlanes";
                decimal a = antes.TryGetValue(clave, out var va) ? va : 0m;
                decimal d = despues.TryGetValue(clave, out var vd) ? vd : 0m;
                decimal delta = d - a;
                bool par = (fi++ % 2) == 0;
                string sA = money ? a.ToString("$ #,##0") : a.ToString("0.##");
                string sD = money ? d.ToString("$ #,##0") : d.ToString("0.##");
                string sDelta = money ? delta.ToString("$ +#,##0;$ -#,##0;0") : delta.ToString("+0.##;-0.##;0");
                string sPct = a != 0 ? Math.Round(100m * delta / Math.Abs(a), 0).ToString("+0;-0;0") + "%" : "—";
                string resultado = T.Resultado(clave, delta);
                Color colorResultado = T.ColorResultado(clave, delta);
                if (resultado == I18n.T("eh.pln.res.mejoro", "Mejoró")) mejoraron++;
                else if (resultado == I18n.T("eh.pln.res.empeoro", "Empeoró")) empeoraron++;
                else sinCambio++;
                Fila(tl, T.KpiSnapshot(clave), sA, sD, sDelta, sPct, resultado, false, par, colorResultado);
            }
            cont.Controls.Add(tl);

            cont.Controls.Add(new Label { Dock = DockStyle.Top, Height = 20, Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris700,
                Text = string.Format(I18n.T("eh.pln.seg.periodos", "Antes: {0}   ·   Después: {1}"), periodoAntes, periodoDespues) });
            cont.Controls.Add(new Label { Dock = DockStyle.Top, Height = 20, Font = Estilo.Normal(9f), ForeColor = Estilo.Gris700,
                Text = string.Format(I18n.T("eh.pln.seg.conclusion2", "Mejoraron: {0} · empeoraron: {1} · sin cambios/informativo: {2}."), mejoraron, empeoraron, sinCambio) });
            cont.Controls.Add(new Label { Dock = DockStyle.Top, Height = 20, Text = I18n.T("eh.pln.seg.aviso", "Comparativo informativo: la variación puede deberse también a otros factores."), Font = Estilo.Normal(8f), ForeColor = Estilo.Gris400 });
            cont.Controls.Add(tit);
            return cont;
        }

        private void Fila(TableLayoutPanel t, string ind, string antes, string desp, string delta, string pct, string resultado, bool header, bool alterna, Color colorResultado)
        {
            int r = t.RowCount; t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, header ? 28 : 24));
            Font f = header ? Estilo.Bold(9f) : Estilo.Normal(9f);
            Color fg = header ? Color.White : Estilo.Gris700;
            Color bg = header ? Estilo.AzulOscuro : (alterna ? Estilo.Gris100 : Color.White);
            Action<string, int, ContentAlignment, Color> celda = (txt, col, al, color) =>
                t.Controls.Add(new Label { Text = txt, Dock = DockStyle.Fill, Font = header ? f : (col == 5 ? Estilo.Bold(9f) : f), ForeColor = color, BackColor = bg, TextAlign = al, AutoEllipsis = true, Margin = new Padding(0), Padding = new Padding(6, 0, 6, 0) }, col, r);
            celda(ind, 0, ContentAlignment.MiddleLeft, fg);
            celda(antes, 1, ContentAlignment.MiddleRight, fg);
            celda(desp, 2, ContentAlignment.MiddleRight, fg);
            celda(delta, 3, ContentAlignment.MiddleRight, fg);
            celda(pct, 4, ContentAlignment.MiddleRight, fg);
            celda(resultado, 5, ContentAlignment.MiddleRight, header ? fg : colorResultado);
        }

        // ── Acciones ──────────────────────────────────────────────────────────────
        private void AprobarPlan() { _svc.AprobarPlan(_sel.IdPlanAccion); Ok(I18n.T("eh.plan.aprobado", "Plan aprobado.")); int id = _sel.IdPlanAccion; RenderPlanes(); SeleccionarPorId(id); }
        private void MarcarEnEjecucion() { _svc.MarcarEnEjecucion(_sel.IdPlanAccion); Ok(I18n.T("eh.plan.enejec", "Plan marcado en ejecución.")); int id = _sel.IdPlanAccion; _etapa = ProcesoPlanificacion.Ejecutar; RenderPlanes(); SeleccionarPorId(id); }
        private void CerrarPlan()
        {
            // BLL.PlanAccionService.CerrarPlan solo exige PuedeCerrar() (Estado == EnEjecucion): NO
            // bloquea el cierre por tareas aprobadas sin ejecutar (verificado — no se inventa esa regla
            // acá). Como SÍ está permitido cerrar con tareas pendientes, la confirmación explica la
            // consecuencia real en vez de ocultarla: pueden seguir ejecutándose después de cerrar, pero
            // el período de observación para evaluar resultados arranca a contar desde este cierre.
            int aprobSinEjec = _svc.ObtenerRecomendaciones(_sel.IdPlanAccion)
                .Count(r => r.Decision == BE.DecisionRecomendacion.Aprobada && !r.Ejecutada);
            string msj = aprobSinEjec > 0
                ? string.Format(I18n.T("eh.plan.cerrarconfirm.pend",
                    "Quedan {0} tarea(s) aprobada(s) sin ejecutar: vas a poder seguir ejecutándolas después de cerrar, pero el período de observación para evaluar resultados empieza a contarse desde este cierre. ¿Continuar?"), aprobSinEjec)
                : I18n.T("eh.plan.cerrarconfirm", "El plan deja de ejecutarse. Los resultados se evaluarán más adelante, cuando transcurra el período de observación. ¿Continuar?");
            if (!Estilo.Confirmar(this, I18n.T("eh.btn.cerrarplan", "Cerrar plan"), msj)) return;
            int id = _sel.IdPlanAccion;
            _svc.CerrarPlan(id);
            Ok(I18n.T("eh.plan.cerrado", "Plan cerrado. Los resultados podrán evaluarse cuando transcurra el período de observación."));
            _filtro = 2; PintarChips(_chips, 2);
            RenderPlanes();
            _etapa = ProcesoPlanificacion.Evaluar;
            SeleccionarPorId(id);
        }
        private void EvaluarResultados()
        {
            if (!Estilo.Confirmar(this, I18n.T("eh.btn.evaluarresultados", "Evaluar resultados"),
                I18n.T("eh.plan.evaluarconfirm", "Se registrará el comparativo Antes/Después con los indicadores reales del período de observación. ¿Continuar?"))) return;
            int id = _sel.IdPlanAccion;
            _svc.EvaluarResultados(id);
            Ok(I18n.T("eh.plan.evaluado", "Resultados evaluados. Ya podés exportar el comparativo a PDF."));
            RenderPlanes();
            SeleccionarPorId(id);
        }
        // Aprobar queda en UN clic: MotivoDecision es NULLable en BD y la BLL nunca exigió motivo para
        // aprobar (solo tenía sentido pedirlo siempre por simetría visual con Descartar). Descartar
        // SIGUE pidiendo motivo por diálogo: es la acción que de verdad se beneficia de un porqué.
        private void Decidir(BE.RecomendacionPlan rec, bool aprobar)
        {
            if (_accionEnCurso) return;
            string motivo = null;
            if (!aprobar)
            {
                using (var dlg = new InputDialog(I18n.T("eh.btn.descartar", "Descartar"), I18n.T("eh.plan.motivo", "Motivo de la decisión:"), false))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    motivo = dlg.InputText?.Trim();
                }
            }
            _accionEnCurso = true;
            try
            {
                _svc.Decidir(rec.IdRecomendacion, aprobar, motivo);
                RenderDetalle(); ActualizarPasoBar();
            }
            finally { _accionEnCurso = false; }
        }
        private void Ejecutar(BE.RecomendacionPlan rec)
        {
            if (_accionEnCurso) return;
            if (!Estilo.Confirmar(this, I18n.T("eh.btn.ejecutar", "Ejecutar tarea"), string.Format(I18n.T("eh.plan.ejecconfirm", "Se ejecutará: {0} sobre '{1}'. ¿Continuar?"), T.Accion(rec.AccionSugerida), rec.NombreEntidad))) return;
            _accionEnCurso = true;
            try
            {
                _svc.EjecutarRecomendacion(rec);
                Ok(I18n.T("eh.plan.ejecutada", "Tarea ejecutada."));
                RenderDetalle(); ActualizarPasoBar();
            }
            finally { _accionEnCurso = false; }
        }
        // Un ÚNICO botón "Exportar PDF" (el del encabezado, visible en cualquier etapa) decide SOLO qué
        // informe generar: si se está mirando Resultados y todavía no hay evaluación real (snapshot
        // "Después"), exporta el informe de impacto simulado (mismos números que la pantalla); en
        // cualquier otro caso, el informe general del plan. Antes había DOS botones de PDF visibles al
        // mismo tiempo en Resultados (este + uno propio del bloque simulado) — ahora hay uno solo.
        private void ExportarPdf()
        {
            var recs = _svc.ObtenerRecomendaciones(_sel.IdPlanAccion);
            if (_etapa == ProcesoPlanificacion.Evaluar && !_svc.TieneEvaluacion(_sel.IdPlanAccion))
            {
                var resumen = SimuladorResultados.Calcular(recs);
                ExportarInformeSimulado(_sel, resumen);
                return;
            }
            var snaps = _svc.ObtenerComparativo(_sel.IdPlanAccion);
            // Informe EJECUTIVO estructurado → PDF directo a archivo (PDFsharp), sin impresora virtual.
            var doc = Exportacion.InformePlanificacion.DocumentoPlan(_sel, snaps, recs);
            new Exportacion.ExportadorPdfArchivo().Exportar(doc, this);
        }

        private void Ok(string msg) => Estilo.Exito(this, I18n.T("eh.pln.sec.planes", "Planes de acción"), msg);

        protected override void TraducirExtra() { Try(RenderPlanes); }
    }
}

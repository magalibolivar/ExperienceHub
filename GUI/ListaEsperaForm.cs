using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// PN02 · Gestión de lista de espera — pantalla comercial orientada a tareas.
    /// Encabezado + dos indicadores globales + pestañas (Activas y próximas / Anteriores e historial).
    /// El contenido se adapta al estado real: vacío compacto (A), tarjetas de experiencias con cola (B),
    /// y ofertas vigentes accionables en tarjeta contextual (C). No altera FIFO, vencimientos, estados
    /// ni reglas (todo en BLL/DAL); las consultas globales son de solo lectura.
    /// </summary>
    public class ListaEsperaForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.ListaEspera _bll    = new BLL.ListaEspera();
        private readonly BLL.Experiencia _bllExp = new BLL.Experiencia();
        private const string MODULO = "ListaEspera";

        private enum Pestania { Activas, Historial }
        private Pestania _tab = Pestania.Activas;

        // Datos (solo lectura)
        private List<BE.ResumenListaEspera> _resumen = new List<BE.ResumenListaEspera>();
        private List<BE.ListaEspera> _ofertas = new List<BE.ListaEspera>();
        private Dictionary<int, BE.Experiencia> _expPorId = new Dictionary<int, BE.Experiencia>();
        private int? _expSelId;
        private List<BE.ListaEspera> _todas = new List<BE.ListaEspera>();
        private int _ordenB; // 0 = próximas, 1 = más clientes

        // UI persistente
        private Label _lblNumEspera, _lblNumOfertas;
        private TarjetaPanel _cardEspera, _cardOfertas;
        private Button _btnTabActivas, _btnTabHistorial;
        private Panel _host;

        // Historial
        private ComboBox _cboHistExp, _cboHistEstado;
        private DataGridView _gridHist;

        public ListaEsperaForm()
        {
            Text = "Lista de Espera"; Size = new Size(1100, 680); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(960, 600);
            ConstruirUI();
        }

        // ══════════════════════ ESTRUCTURA GENERAL ═════════════════════════════════
        private void ConstruirUI()
        {
            var card = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                          Margin = new Padding(UI.GapXL, UI.GapL, UI.GapXL, UI.GapL),
                                          Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };

            _host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var tabs   = ConstruirTabs();
            var inds    = ConstruirIndicadores();
            var cabecera = ConstruirCabecera();

            card.Controls.Add(_host);      // Fill
            card.Controls.Add(tabs);       // Top
            card.Controls.Add(inds);       // Top
            card.Controls.Add(cabecera);   // Top

            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.Lienzo };
            cont.Controls.Add(card);
            Controls.Add(cont);
        }

        private Panel ConstruirCabecera()
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.White };
            var txt = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var tit = new Label { Dock = DockStyle.Top, Height = 28, Text = I18n.T("eh.le.gtitulo", "Gestión de lista de espera"),
                                  Tag = "eh.le.gtitulo", Font = Estilo.SemiBold(15f), ForeColor = Estilo.AzulOscuro };
            var sub = new Label { Dock = DockStyle.Top, Height = 20, Text = I18n.T("eh.le.gsub", "Consultá las listas y gestioná las ofertas disponibles."),
                                  Tag = "eh.le.gsub", Font = Estilo.Normal(9.5f), ForeColor = Estilo.Gris700 };
            txt.Controls.Add(sub); txt.Controls.Add(tit);
            var btn = UI.Secundario("↻ Actualizar", "eh.btn.refrescar", (s, e) => Actualizar());
            var der = new Panel { Dock = DockStyle.Right, Width = 150, BackColor = Color.White };
            btn.Location = new Point(0, 8); btn.Width = 140; der.Controls.Add(btn);
            p.Controls.Add(txt); p.Controls.Add(der);
            return p;
        }

        private Panel ConstruirIndicadores()
        {
            var fila = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, ColumnCount = 2, RowCount = 1,
                                              BackColor = Color.White, Padding = new Padding(0, 6, 0, 6) };
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _cardEspera = TarjetaIndicador("👥", "eh.le.ind.espera.lbl", "Clientes en espera (global)",
                "eh.le.ind.espera.desc", "Inscripciones activas en listas de espera", out _lblNumEspera);
            _cardEspera.Margin = new Padding(0, 0, UI.Gap, 0);
            _cardOfertas = TarjetaIndicador("⏰", "eh.le.ind.ofertas.lbl", "Ofertas pendientes (global)",
                "eh.le.ind.ofertas.desc", "Ofertas vigentes que requieren atención", out _lblNumOfertas);
            _cardOfertas.Margin = new Padding(UI.Gap, 0, 0, 0);

            fila.Controls.Add(_cardEspera, 0, 0);
            fila.Controls.Add(_cardOfertas, 1, 0);
            return fila;
        }

        private TarjetaPanel TarjetaIndicador(string icono, string tagLbl, string lbl, string tagDesc, string desc, out Label num)
        {
            var c = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                       Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.Gap) };
            // Labels DIRECTOS sobre la tarjeta (BackColor transparente) para que el fondo amarillo del
            // indicador de ofertas se vea sin un panel blanco tapándolo.
            var ico = new Label { Dock = DockStyle.Left, Width = 44, Text = icono, Font = Estilo.Normal(20f),
                                  TextAlign = ContentAlignment.MiddleCenter, ForeColor = Estilo.AzulDeSeleccion, BackColor = Color.Transparent };
            var d = new Label { Dock = DockStyle.Top, Height = 16, Text = I18n.T(tagDesc, desc), Tag = tagDesc,
                                Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris400, BackColor = Color.Transparent };
            num = new Label { Dock = DockStyle.Top, Height = 30, Text = "0", Font = Estilo.SemiBold(20f), ForeColor = Estilo.AzulOscuro, BackColor = Color.Transparent };
            var l = new Label { Dock = DockStyle.Top, Height = 18, Text = I18n.T(tagLbl, lbl), Tag = tagLbl,
                                Font = Estilo.Normal(9f), ForeColor = Estilo.Gris700, BackColor = Color.Transparent };
            c.Controls.Add(d); c.Controls.Add(num); c.Controls.Add(l); c.Controls.Add(ico);
            return c;
        }

        private Panel ConstruirTabs()
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, BackColor = Color.White, Padding = new Padding(0, 4, 0, 4) };
            _btnTabActivas   = PildoraTab("Activas y próximas", "eh.le.tab.activas", () => CambiarTab(Pestania.Activas));
            _btnTabHistorial = PildoraTab("Anteriores e historial", "eh.le.tab.historial", () => CambiarTab(Pestania.Historial));
            p.Controls.Add(_btnTabActivas); p.Controls.Add(_btnTabHistorial);
            return p;
        }

        private Button PildoraTab(string texto, string tag, Action onClick)
        {
            var b = new Button { Text = I18n.T(tag, texto), Tag = tag, AutoSize = false, Height = 30, Width = 180,
                                 FlatStyle = FlatStyle.Flat, Font = Estilo.SemiBold(9.5f), Margin = new Padding(0, 0, 8, 0) };
            b.FlatAppearance.BorderSize = 1;
            b.Click += (s, e) => onClick();
            return b;
        }

        private void PintarTabs()
        {
            void Pintar(Button b, bool activo)
            {
                b.BackColor = activo ? Estilo.AzulDeSeleccion : Color.White;
                b.ForeColor = activo ? Color.White : Estilo.Gris700;
                b.FlatAppearance.BorderColor = activo ? Estilo.AzulDeSeleccion : Estilo.Gris200;
            }
            Pintar(_btnTabActivas, _tab == Pestania.Activas);
            Pintar(_btnTabHistorial, _tab == Pestania.Historial);
        }

        private void CambiarTab(Pestania t) { _tab = t; PintarTabs(); RenderContenido(); }

        // ══════════════════════ CICLO DE VIDA ══════════════════════════════════════
        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.listaespera", this.Text); I18n.TraducirControles(this); PintarTabs(); RenderContenido(); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            CargarDatos();
            NavegacionInicial();
            PintarTabs();
            RenderContenido();
            Traducir();
        }

        private void Actualizar()
        {
            int? prev = _expSelId;
            CargarDatos();
            if (prev.HasValue && _expPorId.ContainsKey(prev.Value)) _expSelId = prev;
            else NavegacionInicial();
            RenderContenido();
        }

        // ══════════════════════ DATOS (solo lectura) ═══════════════════════════════
        private void CargarDatos()
        {
            try
            {
                _ofertas = _bll.ObtenerOfertasVigentes();
                _resumen = _bll.ObtenerResumenActivo();
                _expPorId.Clear();
                foreach (var ex in _bllExp.ObtenerTodos()) _expPorId[ex.IdExperiencia] = ex;

                int enEspera = 0; foreach (var r in _resumen) enEspera += r.EnEspera;
                if (_lblNumEspera  != null) _lblNumEspera.Text  = enEspera.ToString();
                if (_lblNumOfertas != null) _lblNumOfertas.Text = _ofertas.Count.ToString();

                // Indicador de ofertas destacado en amarillo suave cuando hay ofertas pendientes.
                if (_cardOfertas != null)
                    _cardOfertas.BackColor = _ofertas.Count > 0 ? Estilo.OfertaPendiente : Color.White;
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Experiencias FUTURAS con actividad (clientes esperando u ofertas), para la pestaña Activas.
        private List<BE.ResumenListaEspera> ActividadFutura()
        {
            var lista = new List<BE.ResumenListaEspera>();
            DateTime ahora = DateTime.Now;
            foreach (var r in _resumen)
            {
                if (r.Activas <= 0) continue;
                _expPorId.TryGetValue(r.IdExperiencia, out var e);
                bool futura = r.FechaHoraExperiencia.HasValue ? r.FechaHoraExperiencia.Value > ahora
                            : (e != null && e.EsFutura());
                bool operable = e == null || (e.Estado != BE.EstadoExperiencia.Finalizada && e.Estado != BE.EstadoExperiencia.Cancelada);
                if (futura && operable) lista.Add(r);
            }
            if (_ordenB == 1) lista.Sort((a, b) => b.EnEspera.CompareTo(a.EnEspera));
            else lista.Sort((a, b) => (a.FechaHoraExperiencia ?? DateTime.MaxValue).CompareTo(b.FechaHoraExperiencia ?? DateTime.MaxValue));
            return lista;
        }

        private void NavegacionInicial()
        {
            if (_ofertas.Count > 0) { _expSelId = _ofertas[0].IdExperiencia; return; }
            var act = ActividadFutura();
            _expSelId = act.Count > 0 ? (int?)act[0].IdExperiencia : null;
        }

        // ══════════════════════ RENDER ═════════════════════════════════════════════
        private void RenderContenido()
        {
            if (_host == null) return;
            _host.SuspendLayout();
            _host.Controls.Clear();
            if (_tab == Pestania.Historial) RenderHistorial();
            else
            {
                var act = ActividadFutura();
                if (_ofertas.Count == 0 && act.Count == 0) RenderVacioA();
                else RenderTrabajo(act);
            }
            _host.ResumeLayout();
        }

        // ── ESCENARIO A: sin actividad ───────────────────────────────────────────────
        private void RenderVacioA()
        {
            var card = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                          Margin = new Padding(0, UI.Gap, 0, 0), Padding = new Padding(UI.GapL) };
            var centro = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var ico = new Label { Dock = DockStyle.Top, Height = 64, Text = "🗓", Font = Estilo.Normal(40f),
                                  TextAlign = ContentAlignment.MiddleCenter, ForeColor = Estilo.Gris400 };
            var tit = new Label { Dock = DockStyle.Top, Height = 34, Text = I18n.T("eh.le.vacio.titulo", "No hay experiencias con listas de espera activas"),
                                  Tag = "eh.le.vacio.titulo", Font = Estilo.SemiBold(14f), ForeColor = Estilo.AzulOscuro, TextAlign = ContentAlignment.MiddleCenter };
            var desc = new Label { Dock = DockStyle.Top, Height = 24, Text = I18n.T("eh.le.vacio.desc", "Actualmente no hay experiencias con clientes en espera ni ofertas pendientes."),
                                   Tag = "eh.le.vacio.desc", Font = Estilo.Normal(10f), ForeColor = Estilo.Gris700, TextAlign = ContentAlignment.MiddleCenter };
            var hist = new Label { Dock = DockStyle.Top, Height = 22, Text = I18n.T("eh.le.vacio.hist", "Podés consultar experiencias anteriores desde Historial."),
                                   Tag = "eh.le.vacio.hist", Font = Estilo.Normal(9f), ForeColor = Estilo.Gris400, TextAlign = ContentAlignment.MiddleCenter };
            // Centrado vertical aproximado con un relleno superior.
            var relleno = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.White };
            centro.Controls.Add(hist); centro.Controls.Add(desc); centro.Controls.Add(tit); centro.Controls.Add(ico); centro.Controls.Add(relleno);
            card.Controls.Add(centro);
            _host.Controls.Add(card);
        }

        // ── ESCENARIOS B/C: lista de tarjetas (izq) + detalle (der) ─────────────────
        private void RenderTrabajo(List<BE.ResumenListaEspera> actividad)
        {
            var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White,
                                              Padding = new Padding(0, UI.Gap, 0, 0) };
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            cols.Controls.Add(ConstruirColumnaIzquierda(actividad), 0, 0);
            cols.Controls.Add(ConstruirDetalle(), 1, 0);
            _host.Controls.Add(cols);
        }

        private Panel ConstruirColumnaIzquierda(List<BE.ResumenListaEspera> actividad)
        {
            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0, 0, UI.Gap, 0) };
            var lista = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            // Ítems a apilar (se agregan Dock=Top en orden inverso para respetar el orden visual).
            var items = new List<Control>();

            if (_ofertas.Count > 0)
            {
                items.Add(TituloMini(string.Format(I18n.T("eh.le.atencion.n", "Requieren atención ({0})"), _ofertas.Count), Estilo.Naranja));
                foreach (var o in _ofertas) items.Add(TarjetaOferta(o));
                // Otras experiencias con lista activa (las que no son de ofertas).
                var otras = new List<BE.ResumenListaEspera>();
                foreach (var r in actividad)
                {
                    bool tieneOferta = false;
                    foreach (var o in _ofertas) if (o.IdExperiencia == r.IdExperiencia) { tieneOferta = true; break; }
                    if (!tieneOferta && r.EnEspera > 0) otras.Add(r);
                }
                if (otras.Count > 0)
                {
                    items.Add(TituloMini(I18n.T("eh.le.otras", "Otras experiencias con lista activa"), Estilo.Gris700));
                    foreach (var r in otras) items.Add(TarjetaExp(r));
                }
            }
            else
            {
                var cabe = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.White };
                cabe.Controls.Add(TituloMini(string.Format(I18n.T("eh.le.exp.activas", "Experiencias con lista activa ({0})"), actividad.Count), Estilo.AzulOscuro));
                var orden = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Bottom, Width = 220 };
                orden.Items.AddRange(new object[] { I18n.T("eh.le.ordenar.proximas", "Próximas"), I18n.T("eh.le.ordenar.masclientes", "Más clientes en espera") });
                orden.SelectedIndex = _ordenB;
                orden.SelectedIndexChanged += (s, e) => { _ordenB = orden.SelectedIndex; RenderContenido(); };
                cabe.Controls.Add(orden);
                items.Add(cabe);
                foreach (var r in actividad) items.Add(TarjetaExp(r));
            }

            items.Reverse();
            foreach (var c in items) { c.Dock = DockStyle.Top; lista.Controls.Add(c); }
            cont.Controls.Add(lista);
            return cont;
        }

        private Label TituloMini(string texto, Color color)
            => new Label { Dock = DockStyle.Top, Height = 26, Text = texto, Font = Estilo.SemiBold(10f), ForeColor = color, Padding = new Padding(2, 4, 0, 0) };

        private TarjetaPanel TarjetaOferta(BE.ListaEspera o)
        {
            bool sel = _expSelId == o.IdExperiencia;
            var c = new TarjetaPanel { Dock = DockStyle.Top, Radio = 8, Height = 96, Margin = new Padding(0, 0, 0, UI.Gap),
                                       BackColor = sel ? Estilo.AzulDeFondo : Estilo.OfertaPendiente, Padding = new Padding(UI.Gap, 6, UI.Gap, 6) };
            var nom = new Label { Dock = DockStyle.Top, Height = 20, Text = o.NombreExperiencia ?? "—", Font = Estilo.SemiBold(10f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true };
            string fecha = o.FechaHoraExperiencia?.ToString("dd/MM/yyyy · HH:mm") ?? "—";
            var f = FilaMini("📅 " + fecha);
            var cli = FilaMini("👤 " + (o.NombreCliente ?? "—"));
            var v = Vencimiento(o);
            var ven = new Label { Dock = DockStyle.Top, Height = 18, Font = Estilo.Normal(8.5f), ForeColor = Estilo.BordeAdvertencia,
                                  Text = "⏰ " + I18n.T("eh.le.card.oferta", "Oferta pendiente") + (v.HasValue ? " · " + string.Format(I18n.T("eh.le.card.vence", "Vence {0}"), v.Value.ToString("dd/MM HH:mm")) : "") };
            c.Controls.Add(ven); c.Controls.Add(cli); c.Controls.Add(f); c.Controls.Add(nom);
            Clickable(c, (s, e) => { _expSelId = o.IdExperiencia; RenderContenido(); });
            return c;
        }

        private TarjetaPanel TarjetaExp(BE.ResumenListaEspera r)
        {
            bool sel = _expSelId == r.IdExperiencia;
            _expPorId.TryGetValue(r.IdExperiencia, out var e);
            var c = new TarjetaPanel { Dock = DockStyle.Top, Radio = 8, Height = 104, Margin = new Padding(0, 0, 0, UI.Gap),
                                       BackColor = sel ? Estilo.AzulDeFondo : Color.White, Padding = new Padding(UI.Gap, 6, UI.Gap, 6) };
            var nom = new Label { Dock = DockStyle.Top, Height = 20, Text = r.NombreExperiencia ?? "—", Font = Estilo.SemiBold(10f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true };
            string fecha = r.FechaHoraExperiencia?.ToString("dd/MM/yyyy · HH:mm") ?? "—";
            var f = FilaMini("📅 " + fecha);
            var ciudad = FilaMini("📍 " + (e?.NombreCiudad ?? "—"));
            string cupos = e != null ? string.Format(I18n.T("eh.le.card.cupos", "{0}/{1} cupos"), e.CupoDisponible, e.CupoMaximo)
                                     + (e.EstaCompleta() ? " · " + I18n.T("eh.le.completa", "Completa") : "") : "—";
            var cupo = FilaMini("🎟 " + cupos);
            var esp = new Label { Dock = DockStyle.Top, Height = 18, Font = Estilo.SemiBold(8.5f), ForeColor = Estilo.AzulDeSeleccion,
                                  Text = string.Format(I18n.T("eh.le.card.enespera", "{0} en espera"), r.EnEspera) };
            c.Controls.Add(esp); c.Controls.Add(cupo); c.Controls.Add(ciudad); c.Controls.Add(f); c.Controls.Add(nom);
            Clickable(c, (s, ev) => { _expSelId = r.IdExperiencia; RenderContenido(); });
            return c;
        }

        private Label FilaMini(string texto)
            => new Label { Dock = DockStyle.Top, Height = 16, Text = texto, Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris700, AutoEllipsis = true };

        // ── Panel de detalle (derecha) ───────────────────────────────────────────────
        private Panel ConstruirDetalle()
        {
            var cont = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(UI.Gap, 0, 0, 0) };
            if (!_expSelId.HasValue) return cont;

            _expPorId.TryGetValue(_expSelId.Value, out var e);
            try { _bll.ProcesarVencimientos(_expSelId.Value); } catch { }        // vencimiento perezoso (PN02 existente)
            try { _todas = _bll.ObtenerTodas(_expSelId.Value); } catch { _todas = new List<BE.ListaEspera>(); }

            var carddet = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White, Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.Gap) };

            // Encabezado: nombre + badge estado + fila de datos.
            var cab = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.White };
            var nom = new Label { Dock = DockStyle.Fill, Text = e?.Nombre ?? "—", Font = Estilo.SemiBold(13f), ForeColor = Estilo.AzulOscuro, AutoEllipsis = true };
            var badge = new Label { Dock = DockStyle.Right, Width = 120, Height = 24, TextAlign = ContentAlignment.MiddleCenter,
                                    Text = EstadoExpLabel(e), Font = Estilo.SemiBold(9f), ForeColor = Color.White,
                                    BackColor = e != null && e.EstaCompleta() ? Estilo.Naranja : Estilo.AzulDeSeleccion };
            cab.Controls.Add(nom); cab.Controls.Add(badge);
            var datos = new Label { Dock = DockStyle.Top, Height = 22, Font = Estilo.Normal(9.5f), ForeColor = Estilo.Gris700, AutoEllipsis = true,
                Text = e != null ? string.Format("📅 {0}   📍 {1}   🎟 {2}/{3}", e.FechaHoraInicio.ToString("dd/MM/yyyy HH:mm"), e.NombreCiudad ?? "—", e.CupoDisponible, e.CupoMaximo) : "—" };
            var tcli = TituloMini(string.Format(I18n.T("eh.le.detalle.clientes", "Clientes en espera ({0})"),
                _todas.FindAll(le => le.Estado == BE.EstadoListaEspera.Esperando).Count), Estilo.AzulOscuro);

            // Grilla FIFO compacta (cola activa: Esperando + Ofrecido).
            var grid = GrillaCola();
            var activos = _todas.FindAll(le => le.Estado == BE.EstadoListaEspera.Esperando || le.Estado == BE.EstadoListaEspera.Ofrecido);
            grid.DataSource = activos;
            UI.Columnas(grid, ("Posicion", 12), ("NombreCliente", 44), ("Estado", 22), ("FechaIngreso", 22));
            if (grid.Columns.Contains("FechaIngreso")) grid.Columns["FechaIngreso"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
            HeaderCola(grid);

            // Nota FIFO.
            var nota = new Label { Dock = DockStyle.Bottom, Height = 34, Font = Estilo.Normal(8.5f), ForeColor = Estilo.Gris700,
                                   Text = "ℹ " + I18n.T("eh.le.nota.fifo", "Cuando se libere un cupo, el sistema ofrecerá el lugar al primer cliente de la lista, según las reglas vigentes."),
                                   Padding = new Padding(2, 6, 2, 0) };

            // Tarjeta de oferta vigente accionable (solo si hay una).
            var oferta = _todas.Find(le => le.Estado == BE.EstadoListaEspera.Ofrecido);
            Control accion = oferta != null ? (Control)TarjetaAccionOferta(oferta) : new Panel { Dock = DockStyle.Bottom, Height = 0 };

            carddet.Controls.Add(grid);     // Fill
            carddet.Controls.Add(accion);   // Bottom (tarjeta amarilla) — si hay oferta
            carddet.Controls.Add(nota);     // Bottom
            carddet.Controls.Add(tcli);     // Top
            carddet.Controls.Add(datos);    // Top
            carddet.Controls.Add(cab);      // Top
            cont.Controls.Add(carddet);
            return cont;
        }

        private TarjetaPanel TarjetaAccionOferta(BE.ListaEspera oferta)
        {
            var card = new TarjetaPanel { Dock = DockStyle.Bottom, Radio = 8, Height = 96, Margin = new Padding(0, UI.Gap, 0, 0),
                                          BackColor = Estilo.OfertaPendiente, Padding = new Padding(UI.GapL, 6, UI.GapL, 6) };
            var acc = new Panel { Dock = DockStyle.Right, Width = 180, BackColor = Estilo.OfertaPendiente };
            var btnConf = UI.Exito("Confirmar oferta", "eh.btn.confirmaroferta", (s, e) => Confirmar(oferta));
            var btnRech = UI.Peligro("Rechazar oferta", "eh.btn.rechazaroferta", (s, e) => Rechazar(oferta));
            btnConf.Dock = DockStyle.Top; btnConf.Height = 34; btnConf.Margin = new Padding(0, 0, 0, 6);
            btnRech.Dock = DockStyle.Top; btnRech.Height = 34;
            acc.Controls.Add(btnRech); acc.Controls.Add(btnConf);

            var cuerpo = new Panel { Dock = DockStyle.Fill, BackColor = Estilo.OfertaPendiente };
            var tit = new Label { Dock = DockStyle.Top, Height = 20, Text = I18n.T("eh.le.oferta.card.titulo", "Oferta vigente"), Tag = "eh.le.oferta.card.titulo",
                                  Font = Estilo.SemiBold(10f), ForeColor = Estilo.BordeAdvertencia };
            var v = Vencimiento(oferta);
            var l1 = FilaMini(string.Format(I18n.T("eh.le.oferta.cliente", "Cliente: {0}"), oferta.NombreCliente ?? "—")
                              + "   " + string.Format(I18n.T("eh.le.oferta.posicion", "Posición: {0}"), oferta.Posicion));
            var l2 = FilaMini(oferta.FechaOferta.HasValue ? string.Format(I18n.T("eh.le.oferta.ofrecida", "Ofrecida el: {0}"), oferta.FechaOferta.Value.ToString("dd/MM/yyyy HH:mm")) : "");
            var l3 = FilaMini(v.HasValue ? string.Format(I18n.T("eh.le.oferta.vence", "Vence el: {0}"), v.Value.ToString("dd/MM/yyyy HH:mm")) : "");
            cuerpo.Controls.Add(l3); cuerpo.Controls.Add(l2); cuerpo.Controls.Add(l1); cuerpo.Controls.Add(tit);

            card.Controls.Add(cuerpo); card.Controls.Add(acc);
            return card;
        }

        // ── Pestaña Historial ────────────────────────────────────────────────────────
        private void RenderHistorial()
        {
            var card = new TarjetaPanel { Dock = DockStyle.Fill, Radio = 10, BackColor = Color.White,
                                          Margin = new Padding(0, UI.Gap, 0, 0), Padding = new Padding(UI.GapL, UI.Gap, UI.GapL, UI.GapL) };
            _cboHistExp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            var exps = new List<BE.Experiencia>(_expPorId.Values);
            exps.Sort((a, b) => b.FechaHoraInicio.CompareTo(a.FechaHoraInicio));
            _cboHistExp.DisplayMember = "Nombre"; _cboHistExp.ValueMember = "IdExperiencia"; _cboHistExp.DataSource = exps;
            _cboHistExp.SelectedIndexChanged += (s, e) => CargarHistorial();
            _cboHistEstado = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboHistEstado.Items.AddRange(new object[]
            {
                I18n.T("eh.le.filtro.todos", "Todos"),
                I18n.T("eh.le.estado.esperando", "Esperando"), I18n.T("eh.le.estado.ofrecido", "Ofrecido"),
                I18n.T("eh.le.estado.confirmado", "Confirmado"), I18n.T("eh.le.estado.retirado", "Retirado"),
            });
            _cboHistEstado.SelectedIndex = 0;
            _cboHistEstado.SelectedIndexChanged += (s, e) => CargarHistorial();
            var filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, WrapContents = false, BackColor = Color.White, Padding = new Padding(0, 4, 0, 0) };
            filtros.Controls.Add(UI.FiltroCampo("Experiencia", _cboHistExp, 300, "eh.lbl.experiencia"));
            filtros.Controls.Add(UI.FiltroCampo("Estado", _cboHistEstado, 160, "eh.lbl.filtroestado"));

            _gridHist = GrillaCola();
            card.Controls.Add(_gridHist);
            card.Controls.Add(filtros);
            card.Controls.Add(TituloMini(I18n.T("eh.le.hist.titulo", "Consultar historial"), Estilo.AzulOscuro));
            _host.Controls.Add(card);
            CargarHistorial();
        }

        private void CargarHistorial()
        {
            if (_gridHist == null) return;
            try
            {
                List<BE.ListaEspera> datos = new List<BE.ListaEspera>();
                if (_cboHistExp?.SelectedValue is int idExp) datos = _bll.ObtenerTodas(idExp);
                int idx = _cboHistEstado?.SelectedIndex ?? 0;
                if (idx == 1) datos = datos.FindAll(le => le.Estado == BE.EstadoListaEspera.Esperando);
                else if (idx == 2) datos = datos.FindAll(le => le.Estado == BE.EstadoListaEspera.Ofrecido);
                else if (idx == 3) datos = datos.FindAll(le => le.Estado == BE.EstadoListaEspera.Confirmado);
                else if (idx == 4) datos = datos.FindAll(le => le.Estado == BE.EstadoListaEspera.Retirado);
                _gridHist.DataSource = datos;
                UI.Columnas(_gridHist, ("Posicion", 10), ("NombreCliente", 34), ("Estado", 18), ("FechaIngreso", 19), ("FechaOferta", 19));
                if (_gridHist.Columns.Contains("FechaIngreso")) _gridHist.Columns["FechaIngreso"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
                if (_gridHist.Columns.Contains("FechaOferta"))  _gridHist.Columns["FechaOferta"].DefaultCellStyle.Format  = "dd/MM/yyyy HH:mm";
                HeaderCola(_gridHist);
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // ══════════════════════ HELPERS ════════════════════════════════════════════
        private DataGridView GrillaCola()
        {
            var g = UI.Grilla();
            g.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (g.Columns[e.ColumnIndex].Name == "Estado" && e.Value is BE.EstadoListaEspera st)
                { e.Value = EstadoLabel(st); e.FormattingApplied = true; }
            };
            UI.PintarFilasPorEstado<BE.ListaEspera>(g, le => ColorEstado(le.Estado));
            return g;
        }

        private static void HeaderCola(DataGridView g)
        {
            void H(string col, string clave, string fb) { if (g.Columns.Contains(col)) g.Columns[col].HeaderText = I18n.T(clave, fb); }
            H("Posicion",      "eh.le.col.posicion", "Posición");
            H("NombreCliente", "eh.le.col.cliente",  "Cliente");
            H("Estado",        "eh.le.col.estado",   "Estado");
            H("FechaIngreso",  "eh.le.col.ingreso",  "Ingresó");
            H("FechaOferta",   "eh.le.col.oferta",   "Ofrecida");
        }

        private static void Clickable(Control c, EventHandler onClick)
        {
            c.Cursor = Cursors.Hand;
            c.Click += onClick;
            foreach (Control ch in c.Controls) Clickable(ch, onClick);
        }

        private static DateTime? Vencimiento(BE.ListaEspera e)
        {
            if (e == null || e.Estado != BE.EstadoListaEspera.Ofrecido || !e.FechaOferta.HasValue) return null;
            DateTime v = e.FechaOferta.Value.AddHours(BLL.ListaEspera.HorasVigenciaOferta);
            if (e.FechaHoraExperiencia.HasValue)
            {
                DateTime porMargen = e.FechaHoraExperiencia.Value.AddHours(-BLL.ListaEspera.MargenInicioHoras);
                if (porMargen < v) v = porMargen;
            }
            return v;
        }

        private static string EstadoLabel(BE.EstadoListaEspera e)
        {
            switch (e)
            {
                case BE.EstadoListaEspera.Esperando:  return I18n.T("eh.le.estado.esperando",  "Esperando");
                case BE.EstadoListaEspera.Ofrecido:   return I18n.T("eh.le.estado.ofrecido",   "Ofrecido");
                case BE.EstadoListaEspera.Confirmado: return I18n.T("eh.le.estado.confirmado", "Confirmado");
                case BE.EstadoListaEspera.Retirado:   return I18n.T("eh.le.estado.retirado",   "Retirado");
                default: return e.ToString();
            }
        }

        private static Color ColorEstado(BE.EstadoListaEspera e)
        {
            switch (e)
            {
                case BE.EstadoListaEspera.Ofrecido:   return Estilo.Naranja;
                case BE.EstadoListaEspera.Confirmado: return Estilo.Verde;
                case BE.EstadoListaEspera.Retirado:   return Estilo.Rojo;
                default:                              return Estilo.Gris700;
            }
        }

        private static string EstadoExpLabel(BE.Experiencia e)
        {
            if (e == null) return "—";
            switch (e.Estado)
            {
                case BE.EstadoExperiencia.Programada: return I18n.T("eh.le.expestado.programada", "Programada");
                case BE.EstadoExperiencia.Completa:   return I18n.T("eh.le.expestado.completa",   "Completa");
                case BE.EstadoExperiencia.EnCurso:    return I18n.T("eh.le.expestado.encurso",    "En curso");
                case BE.EstadoExperiencia.Finalizada: return I18n.T("eh.le.expestado.finalizada", "Finalizada");
                case BE.EstadoExperiencia.Cancelada:  return I18n.T("eh.le.expestado.cancelada",  "Cancelada");
                default: return e.Estado.ToString();
            }
        }

        // ── Acciones (dialogo con cliente; reglas y validaciones intactas en BLL) ─────
        private void Confirmar(BE.ListaEspera entrada)
        {
            if (entrada == null || !entrada.PuedeConfirmarse())
            { MostrarError(I18n.T("eh.le.err.seloferta", "Seleccioná la entrada con la oferta vigente.")); return; }
            if (!Estilo.Confirmar(this, I18n.T("eh.le.confirmar.titulo", "Confirmar oferta"),
                    string.Format(I18n.T("eh.le.confirmar.cuerpo", "¿Confirmar la oferta de «{0}» para {1}?\n\nSe creará la reserva (quedará Pendiente de confirmación)."),
                        entrada.NombreExperiencia, entrada.NombreCliente)))
                return;
            try
            {
                int idReserva = _bll.ConfirmarOferta(MODULO, entrada);
                Estilo.Exito(this, I18n.T("eh.le.ok.confirmada.titulo", "¡Cupo confirmado!"),
                    string.Format(I18n.T("eh.le.ok.confirmada.cuerpo", "Se creó la reserva #{0} desde la lista de espera (queda Pendiente de confirmación)."), idReserva));
                Actualizar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Rechazar(BE.ListaEspera entrada)
        {
            if (entrada == null || !entrada.PuedeRechazarse())
            { MostrarError(I18n.T("eh.le.err.seloferta", "Seleccioná la entrada con la oferta vigente.")); return; }
            if (!Estilo.Confirmar(this, I18n.T("eh.le.rechazar.titulo", "Rechazar oferta"),
                    string.Format(I18n.T("eh.le.rechazar.cuerpo", "¿Rechazar la oferta de {0}?\n\nEl cupo se ofrecerá al siguiente de la fila (si hay)."),
                        entrada.NombreCliente)))
                return;
            try
            {
                _bll.RechazarOferta(MODULO, entrada);
                MostrarOk(I18n.T("eh.le.ok.rechazada", "Oferta rechazada. El cupo se ofreció al siguiente de la fila (si había)."));
                Actualizar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

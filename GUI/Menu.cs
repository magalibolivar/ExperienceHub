using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// Capa de Presentación — Formulario Menú Principal (MDI Container).
    ///
    /// Al iniciarse, construye el menú dinámicamente según los permisos EFECTIVOS
    /// del usuario logueado, resueltos en el Login desde el árbol Composite
    /// (tabla PermisoRelacion — la única fuente de verdad de autorización).
    ///
    /// Roles del sistema:
    ///
    ///   Administrador → acceso total (PN01 Comercialización | PN02 Reservas | Administración | Bitácora)
    ///   Supervisor    → Bitácora
    ///   Venta         → PN01 Comercialización (Clientes, Planes, Suscripciones, Contrataciones)
    ///                   y PN02 Reservas (Experiencias, Reservas, Reservas realizadas, Lista de espera)
    ///   Caja          → PN01 Comercialización (cobro de contrataciones)
    ///
    /// Los permisos se leen de BE.Usuario.Permisos via BLL.ObtenerUsuarioActivo().
    /// La GUI nunca accede directamente a Seguridad ni a DAL.
    ///
    /// PATRÓN OBSERVER — T05 Gestión de Múltiples Idiomas:
    ///   Implementa IIdiomaObserver. Se suscribe al GestorIdioma en Load
    ///   y se desuscribe en FormClosing. Al recibir UpdateLanguage() llama
    ///   a Traducir() que reasigna el .Text de todos los ítems del menú.
    ///   La barra de idiomas (ToolStrip con 3 botones) se construye en el constructor.
    /// </summary>
    public partial class Menu : Form, IIdiomaObserver
    {
        // Timer de verificación periódica de integridad (C — background check)
        private System.Windows.Forms.Timer _timerIntegridad;

        // Barra de idioma con un DROPDOWN (reemplaza los botones). Soporta idiomas dinámicos de BD.
        private ToolStrip _tsIdioma;
        private ToolStripComboBox _cmbIdiomaMenu;
        private bool _suprimirIdiomaMenu = false;
        // Label "Idioma:" / "Language:" / "Язык:" — dinámico por Observer
        private ToolStripLabel _lblIdioma;
        // Item "Mi Perfil" (preferencias del usuario) — creado dinámicamente, traducible por Observer
        private ToolStripMenuItem _miPerfilItem;
        // Ítem "Administración de Usuarios" (panel ABM de datos) — se agrega por código bajo Administrar.
        private ToolStripMenuItem _adminUsuariosItem;
        // Submenús de "Administrar" (reorganización): "Usuarios ▸" y "Sistema ▸".
        private ToolStripMenuItem _grpUsuarios, _grpSistema;
        // Helper de traducción con fallback (para ítems creados por código).
        private static string Tx(string key, string fallback)
        {
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            return t.ContainsKey(key) ? t[key].Texto : fallback;
        }
        // Usuario cargado en el constructor; reutilizado en OnLoad para no hacer dos SELECT
        private BE.Usuario _usuarioActivo;

        public Menu()
        {
            InitializeComponent();
            try { string ico = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico"); if (System.IO.File.Exists(ico)) this.Icon = new System.Drawing.Icon(ico); } catch { }
            ConstruirMenuCatalogos();

            // ── Barra de selección de idioma ─────────────────────────────────
            // Se agrega un ToolStrip justo debajo del MenuStrip existente.
            // Los 3 botones llaman a GestorIdioma.CambiarIdioma() → notifica a
            // todos los formularios abiertos de forma automática (patrón Observer).
            _tsIdioma = new ToolStrip
            {
                Dock      = DockStyle.Top,
                BackColor = Color.FromArgb(40, 40, 55),
                GripStyle = ToolStripGripStyle.Hidden,
                Padding   = new Padding(4, 0, 4, 0),
                Height    = 28
            };

            _lblIdioma = new ToolStripLabel
            {
                Text      = "Idioma:",
                ForeColor = Color.FromArgb(200, 200, 210),
                Font      = new System.Drawing.Font("Segoe UI", 8.5f)
            };

            _tsIdioma.Items.Add(_lblIdioma);
            _tsIdioma.Items.Add(new ToolStripSeparator());

            _cmbIdiomaMenu = new ToolStripComboBox
            {
                AutoSize      = false,
                Width         = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new System.Drawing.Font("Segoe UI", 8.5f)
            };
            _cmbIdiomaMenu.ComboBox.SelectedIndexChanged += CmbIdiomaMenu_Changed;
            _tsIdioma.Items.Add(_cmbIdiomaMenu);
            ReconstruirComboIdioma(Traductor.ObtenerIdiomas());

            // Insertar el ToolStrip después del MenuStrip (índice 0 = primero visible abajo del borde)
            this.Controls.Add(_tsIdioma);
            _tsIdioma.BringToFront();

            // Obtener usuario activo via BLL (GUI nunca toca SessionManager directamente)
            _usuarioActivo = new BLL.Usuario().ObtenerUsuarioActivo();

            if (_usuarioActivo != null)
            {
                this.Text = "ExperienceHub  —  " + _usuarioActivo.Username +
                            (_usuarioActivo.Perfil != null ? "  [" + _usuarioActivo.Perfil + "]" : "");

                // Cargar y aplicar las preferencias de UI del usuario (fuente/tamaño/tema).
                PreferenciasUI.Cargar(_usuarioActivo.Id);
                PreferenciasUI.Aplicar(this);
            }

            // "Mi Perfil" — preferencias del usuario (idioma). Disponible para TODOS los usuarios.
            // Se inserta al inicio del menú "Perfil", antes de "Cerrar Sesión".
            _miPerfilItem = new System.Windows.Forms.ToolStripMenuItem(
                Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual).ContainsKey("perfil.menu")
                    ? Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual)["perfil.menu"].Texto
                    : "Mi Perfil");
            _miPerfilItem.Click += MiPerfil_Click;
            usuarioToolStripMenuItem.DropDownItems.Insert(0, _miPerfilItem);

            // El menú de sesión pasa a llamarse "Sesión" (evita confundirlo con "Perfiles y Permisos").
            usuarioToolStripMenuItem.Tag  = "mnu.sesion";
            usuarioToolStripMenuItem.Text = Tx("mnu.sesion", "Sesión");

            // ── Reorganización del menú "Administrar" en submenús ──────────────────
            // El panel ABM de datos de usuario (modificar nombre/apellido/usuario/fecha nac./email,
            // cambiar rol y ver historial). Va dentro del submenú "Usuarios".
            _adminUsuariosItem = new ToolStripMenuItem(
                Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual).ContainsKey("mnu.adminusuarios")
                    ? Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual)["mnu.adminusuarios"].Texto
                    : "Administración de Usuarios") { Tag = "mnu.adminusuarios", Name = "adminUsuariosToolStripMenuItem" };
            _adminUsuariosItem.Click += AdminUsuarios_Click;

            // "Usuarios" (operaciones de cuenta) pasa a llamarse "Cuentas de Usuario" para no
            // confundirse con "Administración de Usuarios".
            usuariosToolStripMenuItem.Tag  = "mnu.cuentas";
            usuariosToolStripMenuItem.Text = Tx("mnu.cuentas", "Cuentas de Usuario");

            // Submenú "Usuarios": ABM de datos + cuentas de usuario + historial de cambios.
            _grpUsuarios = new ToolStripMenuItem(Tx("mnu.grp.usuarios", "Usuarios")) { Tag = "mnu.grp.usuarios", Name = "grpUsuariosToolStripMenuItem" };
            _grpUsuarios.DropDownItems.Add(_adminUsuariosItem);
            _grpUsuarios.DropDownItems.Add(usuariosToolStripMenuItem);
            _grpUsuarios.DropDownItems.Add(historialUsuariosToolStripMenuItem);

            // Submenú "Sistema": herramientas transversales.
            _grpSistema = new ToolStripMenuItem(Tx("mnu.grp.sistema", "Sistema")) { Tag = "mnu.grp.sistema", Name = "grpSistemaToolStripMenuItem" };
            _grpSistema.DropDownItems.Add(idiomasToolStripMenuItem);
            _grpSistema.DropDownItems.Add(backupToolStripMenuItem);
            _grpSistema.DropDownItems.Add(integridadToolStripMenuItem);

            // Reconstruir el dropdown de "Administrar": Usuarios ▸, Perfiles, ──── , Sistema ▸.
            gestionToolStripMenuItem.DropDownItems.Clear();
            gestionToolStripMenuItem.DropDownItems.Add(_grpUsuarios);
            gestionToolStripMenuItem.DropDownItems.Add(perfilesToolStripMenuItem);
            gestionToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            gestionToolStripMenuItem.DropDownItems.Add(_grpSistema);

            // Reorganizar la navegación en bloques de negocio (Fidelización / Operación / Analíticas
            // / Administración / Sesión) reutilizando los ítems y formularios ya existentes.
            ReorganizarNavegacionPorBloques();

            // Construir menú dinámico según permisos del rol
            RegistroControles.Registrar(this);   // Etapa 4 (C1) — registra los ítems del menú para la pantalla de mapeo
            AplicarPermisos(_usuarioActivo?.Permisos);
        }

        // Abre "Mi Perfil" como diálogo modal (preferencias del usuario en sesión).
        private void MiPerfil_Click(object sender, EventArgs e)
        {
            using (var f = new MiPerfilForm(_usuarioActivo))
                f.ShowDialog(this);
        }

        /// <summary>
        /// Genera un tile 160×148 con el monograma WF en patrón de ladrillos
        /// (filas alternadas desplazadas medio tile) sobre fondo rosa claro.
        /// El tile repite perfectamente en ambas direcciones sin cortes visibles.
        /// </summary>
        /// <summary>Renderer de menú moderno y sobrio: fondo blanco, hover índigo muy claro, texto slate.</summary>
        private sealed class MenuRendererModerno : ToolStripProfessionalRenderer
        {
            public MenuRendererModerno() : base(new ColorTableModerno()) { }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Selected ? Color.FromArgb(255, 107, 107) : Color.FromArgb(36, 59, 83);
                base.OnRenderItemText(e);
            }
        }

        private sealed class ColorTableModerno : ProfessionalColorTable
        {
            private static readonly Color Hover = Color.FromArgb(255, 236, 228); // coral/durazno claro
            private static readonly Color Borde = Color.FromArgb(226, 232, 240);
            public override Color MenuItemSelected              => Hover;
            public override Color MenuItemSelectedGradientBegin => Hover;
            public override Color MenuItemSelectedGradientEnd   => Hover;
            public override Color MenuItemBorder                => Hover;
            public override Color MenuItemPressedGradientBegin  => Hover;
            public override Color MenuItemPressedGradientEnd    => Hover;
            public override Color MenuBorder                    => Borde;
            public override Color ToolStripDropDownBackground   => Color.White;
            public override Color ImageMarginGradientBegin      => Color.White;
            public override Color ImageMarginGradientMiddle     => Color.White;
            public override Color ImageMarginGradientEnd        => Color.White;
        }

        /// <summary>
        /// Muestra u oculta los ítems del menú según los permisos del usuario.
        /// La lógica es completamente basada en permisos (NombreMenu), no en roles.
        ///
        /// Mapeo NombreMenu → ToolStripMenuItem:
        ///   mnuExperiencias        → experienciasToolStripMenuItem        (PN02 Reservas)
        ///   mnuReservas            → reservasToolStripMenuItem            (PN02 Reservas)
        ///   mnuReservasRealizadas  → reservasRealizadasToolStripMenuItem  (PN02 Reservas)
        ///   mnuClientes            → clientesToolStripMenuItem            (PN01 Comercialización)
        ///   mnuPlanSuscripciones   → planesToolStripMenuItem              (PN01 Comercialización)
        ///   mnuUsuarios            → gestionToolStripMenuItem             (Administración)
        ///   mnuAuditoria           → bitacoraToolStripMenuItem
        ///
        /// Administrador ve todo el menú (acceso total por perfil).
        /// </summary>
        private void AplicarPermisos(List<BE.Permiso> permisos)
        {
            // Panel de Control visible para todos los usuarios autenticados (transversal, sin color de cuatri).
            panelControlToolStripMenuItem.Visible = true;

            // Permisos efectivos del usuario, indexados por NombreMenu (O(1), case-insensitive).
            var nombresMenu = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (permisos != null)
                foreach (var p in permisos)
                    if (!string.IsNullOrEmpty(p.NombreMenu)) nombresMenu.Add(p.NombreMenu);

            // El Administrador tiene ACCESO TOTAL por definición (consigna §3): ve TODAS las
            // pantallas de todos los roles, sin depender de qué patentes tenga asignadas en el
            // árbol de permisos. Es el mismo bypass por perfil que ya aplican PermisosAccion,
            // BLLHelper y Familia en la capa de negocio; acá lo replicamos para la visibilidad
            // del menú, que era el único punto sin contemplarlo.
            bool esAdmin = _usuarioActivo != null && _usuarioActivo.EsAdministrador;
            bool Permite(string nm) => esAdmin || nombresMenu.Contains(nm);

            // ── Mapa declarativo HOJA → permiso (ÚNICA fuente de verdad) ─────────────────────────
            // En vez de repetir 'item.Visible = nombresMenu.Contains("mnuX")' por cada control, la
            // relación se declara UNA sola vez. Agregar un módulo nuevo = una línea acá. (Enfoque
            // data-driven inspirado en el ManejadorSeguridad de Stach, resuelto en código para
            // conservar la semántica de "grupo visible si ALGÚN hijo lo está", que un mapeo plano
            // en tabla no expresa por sí solo.)
            var hojas = new (ToolStripItem Item, string Permiso)[]
            {
                (experienciasToolStripMenuItem,           "mnuExperiencias"),
                (clientesToolStripMenuItem,          "mnuClientes"),
                (planesToolStripMenuItem,            "mnuPlanSuscripciones"),
                (reservasToolStripMenuItem,      "mnuReservas"),
                (reservasRealizadasToolStripMenuItem, "mnuReservasRealizadas"),
                // Bloque "Administrar" + "Sistema": todo gobernado por la patente de gestión.
                (usuariosToolStripMenuItem,          "mnuUsuarios"),
                (perfilesToolStripMenuItem,          "mnuUsuarios"),
                (idiomasToolStripMenuItem,           "mnuUsuarios"),
                (historialUsuariosToolStripMenuItem, "mnuUsuarios"),
                (backupToolStripMenuItem,            "mnuUsuarios"),
                (integridadToolStripMenuItem,        "mnuUsuarios"),
                (_adminUsuariosItem,                 "mnuUsuarios"),
                // Bitácora.
                (bitSistemaToolStripMenuItem,        "mnuAuditoria"),
                (bitNegocioToolStripMenuItem,        "mnuAuditoria"),
                (reporteJornadaToolStripMenuItem,    "mnuAuditoria"),
            };
            foreach (var h in hojas)
                if (h.Item != null) h.Item.Visible = Permite(h.Permiso);

            // ── Visibilidad de los BLOQUES de negocio (según patentes de sus ítems) ──
            // Se usa .Available (no .Visible): dentro de un dropdown cerrado, .Visible siempre es false.
            SetGrupoVisible(_mnuComercializacion,
                clientesToolStripMenuItem, planesToolStripMenuItem, _miSuscripciones, _miContrataciones);

            SetGrupoVisible(_mnuReservas,
                experienciasToolStripMenuItem, reservasToolStripMenuItem, reservasRealizadasToolStripMenuItem,
                _miListaEspera, _miOrganizadores, _miCategorias, _miCiudades);

            // ── Administración (técnica): submenús + Bitácora ──
            bool tieneUsuarios  = Permite("mnuUsuarios");
            bool tieneAuditoria = Permite("mnuAuditoria");
            if (_grpUsuarios != null) _grpUsuarios.Visible = tieneUsuarios;
            if (_grpSistema  != null) _grpSistema.Visible  = tieneUsuarios;
            SetGrupoVisible(bitacoraToolStripMenuItem, bitSistemaToolStripMenuItem, bitNegocioToolStripMenuItem);
            gestionToolStripMenuItem.Visible = tieneUsuarios || tieneAuditoria;
        }

        // Un grupo (menú contenedor) es visible si alguno de sus ítems hijo está DISPONIBLE.
        // Se usa .Available (no .Visible): dentro de un dropdown cerrado, .Visible siempre es false.
        private static void SetGrupoVisible(ToolStripMenuItem grupo, params ToolStripItem[] hijos)
        {
            if (grupo == null) return;
            bool algunoVisible = false;
            foreach (var h in hijos)
                if (h != null && h.Available) { algunoVisible = true; break; }
            grupo.Visible = algunoVisible;
        }

        // ── Etapa 2 — Re-aplicación de seguridad EN VIVO (patrón ManejadorSeguridad de Stach) ──

        /// <summary>
        /// Re-resuelve los permisos efectivos del usuario en sesión desde la BD y re-aplica la
        /// visibilidad del menú, sin necesidad de cerrar sesión. Útil tras editar roles/permisos
        /// en "Gestión de Permisos": si se modificó el rol del usuario actual, su menú se actualiza
        /// al instante (los cambios sobre OTROS roles impactan a esos usuarios en su próximo login).
        /// </summary>
        public void RefrescarSeguridad()
        {
            if (!Seguridad.SessionManager.IsLoggedIn) return;
            try
            {
                var usuario  = Seguridad.SessionManager.GetInstance().Usuario;
                var permisos = new BLL.Familia().ObtenerPermisosEfectivos(usuario.Rol ?? usuario.Perfil);
                usuario.Permisos = permisos;   // _usuarioActivo es la MISMA referencia que la sesión
                AplicarPermisos(permisos);
                // Etapa 4 — re-aplicar también la seguridad a nivel de control en los forms abiertos.
                ManejadorSeguridad.ActualizarSeguridadFormulariosAbiertos(usuario);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[Menu.RefrescarSeguridad] " + ex.Message);
            }
        }

        /// <summary>
        /// Localiza el Menú abierto (formulario MDI padre) y le re-aplica la seguridad. Seguro de
        /// llamar desde cualquier formulario hijo tras un cambio de permisos.
        /// </summary>
        public static void RefrescarSeguridadAbierta()
        {
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
            {
                if (f is Menu m) { m.RefrescarSeguridad(); return; }
            }
        }

        /// <summary>
        /// Cierra la sesión y reinicia la aplicación para volver al Login con estado limpio.
        /// </summary>
        private void cerrarSesionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ConfirmarCerrarSesion())
            {
                new BLL.Usuario().Logout(this.Text);
                Application.Restart();
            }
        }

        /// <summary>
        /// Diálogo de confirmación de cierre de sesión con botones traducidos al idioma activo.
        /// Reemplaza MessageBox.Show() cuyo "Yes"/"No" es siempre en inglés (Windows).
        /// </summary>
        private bool ConfirmarCerrarSesion()
        {
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            string T(string key, string fallback) => t.ContainsKey(key) ? t[key].Texto : fallback;

            using (var dlg = new Form())
            {
                dlg.Text            = T("dlg.cerrarsesion.titulo", "Cerrar Sesión");
                dlg.ClientSize      = new Size(340, 126);
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition   = FormStartPosition.CenterParent;
                dlg.MaximizeBox     = false;
                dlg.MinimizeBox     = false;

                var lbl = new Label
                {
                    Text      = T("dlg.cerrarsesion.msg", "¿Está seguro que desea cerrar la sesión?"),
                    Left = 16, Top = 20, Width = 308, Height = 44,
                    Font      = new System.Drawing.Font("Segoe UI", 9.5f),
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };

                var btnSi = new Button
                {
                    Text         = T("btn.si", "Sí"),
                    Left = 84, Top = 76, Width = 76, Height = 30,
                    DialogResult = DialogResult.Yes,
                    BackColor    = Color.FromArgb(255, 140, 66),
                    ForeColor    = Color.White,
                    FlatStyle    = FlatStyle.Flat
                };
                btnSi.FlatAppearance.BorderSize = 0;

                var btnNo = new Button
                {
                    Text         = T("btn.no", "No"),
                    Left = 176, Top = 76, Width = 76, Height = 30,
                    DialogResult = DialogResult.No,
                    FlatStyle    = FlatStyle.Flat
                };

                dlg.Controls.AddRange(new Control[] { lbl, btnSi, btnNo });
                dlg.AcceptButton = btnSi;
                dlg.CancelButton = btnNo;

                try { PreferenciasUI.Aplicar(dlg); } catch { }   // Montserrat + preferencias del usuario

                return dlg.ShowDialog(this) == DialogResult.Yes;
            }
        }

        // Ítems de los módulos de catálogo/negocio (construidos por código). Se ubican luego en los
        // bloques de negocio del menú (Fidelización / Operación) en ReorganizarNavegacionPorBloques.
        private ToolStripMenuItem _miOrganizadores, _miCategorias, _miCiudades,
                                  _miSuscripciones, _miListaEspera, _miContrataciones;
        // Grupos del menú principal (uno por proceso de negocio), creados por código.
        private ToolStripMenuItem _mnuComercializacion, _mnuReservas;
        // (compat) referencia usada por código legacy null-guardeado; ya no se crea el menú "Catálogos".
        private ToolStripMenuItem _catalogosMenu;

        // Crea un ítem de menú que abre un formulario como hijo MDI (reutiliza la instancia si existe).
        private ToolStripMenuItem NuevoItemForm(string texto, string tagI18n, Type tipo, Func<Form> crear)
        {
            var it = new ToolStripMenuItem(texto) { Tag = tagI18n };
            it.Click += (s, e) =>
            {
                foreach (Form h in this.MdiChildren)
                    if (h.GetType() == tipo) { h.BringToFront(); return; }
                var f = crear(); f.MdiParent = this; f.Show();
            };
            return it;
        }

        // Construye los ítems de los módulos de catálogo/negocio (todavía no los ubica en el menú).
        private void ConstruirMenuCatalogos()
        {
            _miOrganizadores   = NuevoItemForm("Organizadores",   "mnu.cat.organizadores",   typeof(Organizadores),       () => new Organizadores());
            _miCategorias      = NuevoItemForm("Categorías",      "mnu.cat.categorias",      typeof(Categorias),          () => new Categorias());
            _miCiudades        = NuevoItemForm("Ciudades",        "mnu.cat.ciudades",        typeof(Ciudades),            () => new Ciudades());
            _miSuscripciones   = NuevoItemForm("Suscripciones",   "mnu.cat.suscripciones",   typeof(SuscripcionesForm),   () => new SuscripcionesForm());
            _miContrataciones  = NuevoItemForm("Contrataciones (Caja)", "mnu.cat.contrataciones", typeof(ContratacionesForm), () => new ContratacionesForm());
            _miListaEspera     = NuevoItemForm("Lista de espera", "mnu.cat.listaespera",     typeof(ListaEsperaForm),     () => new ListaEsperaForm());
        }

        // ── Reorganización de la navegación en BLOQUES DE NEGOCIO (reutiliza ítems/forms existentes) ──
        //   Panel de Control · Fidelización · Ventas/Operación · Analíticas · Administración · Sesión
        private void ReorganizarNavegacionPorBloques()
        {
            // 1 · PN01 — COMERCIALIZACIÓN DE LA SUSCRIPCIÓN (relación comercial con el cliente)
            _mnuComercializacion = new ToolStripMenuItem(Tx("mnu.blq.comercializacion", "Comercialización")) { Tag = "mnu.blq.comercializacion" };
            _mnuComercializacion.DropDownItems.Add(clientesToolStripMenuItem);
            _mnuComercializacion.DropDownItems.Add(planesToolStripMenuItem);
            _mnuComercializacion.DropDownItems.Add(_miSuscripciones);
            _mnuComercializacion.DropDownItems.Add(_miContrataciones);

            // 2 · PN02 — RESERVAS DE EXPERIENCIAS (armado del pedido/reserva + catálogos de apoyo)
            _mnuReservas = new ToolStripMenuItem(Tx("mnu.blq.reservas", "Reservas")) { Tag = "mnu.blq.reservas" };
            _mnuReservas.DropDownItems.Add(experienciasToolStripMenuItem);            // Experiencias
            _mnuReservas.DropDownItems.Add(reservasToolStripMenuItem);       // Reservas
            _mnuReservas.DropDownItems.Add(reservasRealizadasToolStripMenuItem);  // Reservas Realizadas
            _mnuReservas.DropDownItems.Add(_miListaEspera);
            _mnuReservas.DropDownItems.Add(new ToolStripSeparator());
            _mnuReservas.DropDownItems.Add(_miOrganizadores);
            _mnuReservas.DropDownItems.Add(_miCategorias);
            _mnuReservas.DropDownItems.Add(_miCiudades);

            // 3 · ADMINISTRACIÓN (técnica) — reusa "Administrar" y le suma la Bitácora.
            //     El Reporte de Jornada queda DENTRO de Bitácora (ya no hay bloque Analíticas).
            gestionToolStripMenuItem.Tag  = "mnu.administracion";
            gestionToolStripMenuItem.Text = Tx("mnu.administracion", "Administración");
            gestionToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            gestionToolStripMenuItem.DropDownItems.Add(bitacoraToolStripMenuItem);

            // ── Reconstruir la barra superior por proceso de negocio ──
            menuStrip1.Items.Clear();
            menuStrip1.Items.Add(_mnuComercializacion);            // PN01
            menuStrip1.Items.Add(_mnuReservas);                    // PN02
            menuStrip1.Items.Add(gestionToolStripMenuItem);        // Administración
            menuStrip1.Items.Add(usuarioToolStripMenuItem);        // Sesión (Mi Perfil / Cerrar sesión)
        }

        // ══════════════════════════════════════════════════════════════════════
        // PATRÓN OBSERVER — T05 Gestión de Múltiples Idiomas
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Suscribe este formulario al GestorIdioma al abrirse.
        /// Equivalente a frmMain_Load → ManejadorDeSesion.SuscribirObservador(this)
        /// del ejemplo de cátedra.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Tipografía Montserrat en el shell (menú, barra de idioma, etc.).
            try { Estilo.AplicarFuente(this); } catch { }

            // 1. Fondo MDI — lienzo lavanda plano (#F8F9FF, superficie del sistema de diseño),
            //    sin foto ni marca de agua: workspace limpio como en los mockups Stitch.
            foreach (Control c in Controls)
            {
                if (c.GetType().Name == "MdiClient")
                {
                    c.BackColor       = Estilo.Lienzo;
                    c.BackgroundImage = null;
                    break;
                }
            }
            // Barra de menú y de idioma claras (nada de bloques oscuros).
            menuStrip1.BackColor = Color.White;
            menuStrip1.ForeColor = Estilo.AzulOscuro;
            menuStrip1.Renderer  = new MenuRendererModerno();
            if (_tsIdioma != null) { _tsIdioma.BackColor = Estilo.Gris100; }
            if (_lblIdioma != null) { _lblIdioma.ForeColor = Estilo.Gris700; }

            // 2. Observer + timer de integridad
            GestorIdioma.SuscribirObservador(this);
            _timerIntegridad = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
            _timerIntegridad.Tick += TimerIntegridad_Tick;
            _timerIntegridad.Start();

            // 4. Cargar traducciones de BD en background (no bloquea la UI)
            // El Menú abre en el idioma ELEGIDO EN EL LOGIN (no se pisa con la preferencia
            // guardada del usuario). GestorIdioma.IdiomaActual ya refleja lo seleccionado en
            // la pantalla de login; si no se eligió nada, cae al idioma por defecto (ES).
            string codigoPref = GestorIdioma.IdiomaActual?.Id ?? "ES";
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var svc      = new BLL.IdiomaService();
                    var dictTrad = svc.CargarTraducciones(codigoPref);
                    var idiomas  = svc.ObtenerIdiomasActivosComoIdioma();

                    this.BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed) return;
                        if (idiomas.Count > 0)
                        {
                            GestorIdioma.SetIdiomasDisponibles(idiomas);
                            ReconstruirComboIdioma(idiomas);
                        }
                        foreach (var idm in Traductor.ObtenerIdiomas())
                        {
                            if (idm.Id != codigoPref) continue;
                            GestorIdioma.CambiarIdioma(idm, dictTrad);
                            SeleccionarIdiomaEnCombo(codigoPref);
                            break;
                        }
                    }));
                }
                catch
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed) Traducir(GestorIdioma.IdiomaActual);
                    }));
                }
            });
        }

        /// <summary>
        /// Desuscribe este formulario del GestorIdioma al cerrarse.
        /// Equivalente a frmMain_FormClosing → ManejadorDeSesion.DesuscribirObservador(this)
        /// del ejemplo de cátedra.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _timerIntegridad?.Stop();
            _timerIntegridad?.Dispose();
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        private void TimerIntegridad_Tick(object sender, EventArgs e)
        {
            try
            {
                var diag = BLL.Configuracion.ObtenerDiagnostico();

                // Loguear resultado via BLL (GUI no accede a DAL directamente)
                BLL.Configuracion.RegistrarVerificacionPeriodica(diag);

                if (!diag.Integro)
                {
                    var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
                    string T(string k, string fb) => t.ContainsKey(k) ? t[k].Texto : fb;

                    MessageBox.Show(
                        string.Format(
                            T("alerta.integridad.msg",
                              "ALERTA: Se detectaron problemas de integridad en la tabla Usuario.\n\n" +
                              "Filas con DVH inválido: {0}\n" +
                              "DVV almacenado: {1}  |  DVV calculado: {2}\n\n" +
                              "Vaya a Administrar → Diagnóstico de Integridad para reparar."),
                            diag.FilasRotas.Count,
                            diag.DVVAlmacenado?.ToString() ?? "—",
                            diag.DVVCalculado),
                        T("alerta.integridad.titulo", "Alerta de Integridad"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[Menu.TimerIntegridad] Error: {ex.Message}");
            }
        }

    }
}

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
    /// Roles del sistema (documento G04 — ExperienceHub_Iteracion1.docx):
    ///
    ///   Administrador     → TODO: Inventario | Ventas | Administrar | Bitácora
    ///   Supervisor        → Bitácora
    ///   OperadorLogistico → Inventario (Prendas, Outfits, Categorias, Pedidos Realizados)
    ///
    /// Roles adicionales (implementación, no están en G04):
    ///   Vendedor             → Ventas (Clientes, Planes, Pedidos de Venta)
    ///   ControladorDeStock   → Inventario (Prendas, Stock)
    ///   OperadorDeInventario → Ventas (Pedidos Realizados)
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
        // Centro de Alertas (ítem top-level con badge de cantidad) — creado por código.
        private ToolStripMenuItem _alertasItem;
        private int _alertasCount = -1;

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

            // ── Centro de Alertas ──────────────────────────────────────────────
            // Ítem top-level visible para todos los usuarios autenticados (igual que
            // "Panel de Control"). El conteo de alertas se calcula en background.
            // Alineado a la DERECHA del MenuStrip (bajo la X de la ventana), en la misma barra.
            _alertasItem = new ToolStripMenuItem
            {
                Tag       = "mnu.alertas",
                Name      = "alertasToolStripMenuItem",
                Alignment = ToolStripItemAlignment.Right
            };
            _alertasItem.Click += AlertasItem_Click;
            menuStrip1.Items.Add(_alertasItem);
            RefrescarTextoAlertas();

            // Reorganizar la navegación en bloques de negocio (Fidelización / Operación / Analíticas
            // / Administración / Sesión) reutilizando los ítems y formularios ya existentes.
            ReorganizarNavegacionPorBloques();

            // Construir menú dinámico según permisos del rol
            RegistroControles.Registrar(this);   // Etapa 4 (C1) — registra los ítems del menú para la pantalla de mapeo
            AplicarPermisos(_usuarioActivo?.Permisos);
        }

        // Abre el Centro de Alertas como hijo MDI (reusa la instancia abierta si existe).
        private void AlertasItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
                if (hijo is AlertasForm) { hijo.BringToFront(); return; }
            new AlertasForm { MdiParent = this }.Show();
            ActualizarBadgeAlertas(); // refrescar el badge tras consultar
        }

        // Compone el texto del ítem: icono + etiqueta traducida + (N) si hay alertas.
        private void RefrescarTextoAlertas()
        {
            if (_alertasItem == null) return;
            string baseTxt = "🔔 " + Tx("mnu.alertas", "Alertas");
            _alertasItem.Text      = _alertasCount > 0 ? $"{baseTxt} ({_alertasCount})" : baseTxt;
            _alertasItem.ForeColor = _alertasCount > 0 ? Color.FromArgb(255, 235, 130) : Color.White;
        }

        // Calcula la cantidad de alertas en background (la lógica vive en BLL.PanelAlertas)
        // y actualiza el badge sin bloquear la UI.
        private void ActualizarBadgeAlertas()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                int n;
                try { n = new BLL.PanelAlertas().Contar(); } catch { n = 0; }
                try
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed) return;
                        _alertasCount = n;
                        RefrescarTextoAlertas();
                    }));
                }
                catch { }
            });
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
        ///   mnuPrendas            → prendasToolStripMenuItem       (bajo Inventario)
        ///   mnuOutfits            → outfitsToolStripMenuItem        (bajo Inventario)
        ///   mnuCategorias         → categoriasToolStripMenuItem     (bajo Inventario)
        ///   mnuStock              → stockToolStripMenuItem          (bajo Inventario — pendiente en Designer)
        ///   mnuClientes           → clientesToolStripMenuItem       (bajo Ventas)
        ///   mnuPlanSuscripciones  → planesToolStripMenuItem         (bajo Ventas)
        ///   mnuPedidosVenta       → pedidosVentaToolStripMenuItem   (bajo Ventas)
        ///   mnuPedidosRealizados  → pedidosRealizadosToolStripMenuItem (bajo Ventas)
        ///   mnuUsuarios           → gestionToolStripMenuItem        (bajo Administrar)
        ///   mnuAuditoria          → bitacoraToolStripMenuItem
        ///
        /// Administrador tiene los 10 permisos → ve todo el menú.
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
                (prendasToolStripMenuItem,           "mnuExperiencias"),
                (clientesToolStripMenuItem,          "mnuClientes"),
                (planesToolStripMenuItem,            "mnuPlanSuscripciones"),
                (pedidosVentaToolStripMenuItem,      "mnuReservas"),
                (pedidosRealizadosToolStripMenuItem, "mnuReservasRealizadas"),
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

            // Ítems retirados de la interfaz (módulos no implementados): siempre ocultos.
            outfitsToolStripMenuItem.Visible    = false;
            categoriasToolStripMenuItem.Visible = false;

            // ── Visibilidad de los BLOQUES de negocio (según patentes de sus ítems) ──
            // Se usa .Available (no .Visible): dentro de un dropdown cerrado, .Visible siempre es false.
            SetGrupoVisible(_mnuFidelizacion,
                clientesToolStripMenuItem, planesToolStripMenuItem, _miSuscripciones);

            SetGrupoVisible(_mnuOperacion,
                prendasToolStripMenuItem, pedidosVentaToolStripMenuItem, pedidosRealizadosToolStripMenuItem,
                _miListaEspera, _miOrganizadores, _miCategorias, _miCiudades);

            SetGrupoVisible(_mnuAnaliticas, reporteJornadaToolStripMenuItem, _miRecomendaciones,
                _miAnalisisExp, _miClientesRiesgo, _miCancelaciones, _miAnalisisSusc, _miReportesComerciales);

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
                                  _miSuscripciones, _miListaEspera, _miRecomendaciones;
        // Ítems analíticos (PdN 3 — CU-ANA-03..07), ubicados en el bloque Analíticas.
        private ToolStripMenuItem _miAnalisisExp, _miClientesRiesgo, _miCancelaciones, _miAnalisisSusc, _miReportesComerciales;
        // Grupos (bloques de negocio) del menú principal, creados por código.
        private ToolStripMenuItem _mnuFidelizacion, _mnuOperacion, _mnuAnaliticas;
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
            _miListaEspera     = NuevoItemForm("Lista de espera", "mnu.cat.listaespera",     typeof(ListaEsperaForm),     () => new ListaEsperaForm());
            _miRecomendaciones = NuevoItemForm("Recomendaciones", "mnu.cat.recomendaciones", typeof(RecomendacionesForm), () => new RecomendacionesForm());

            // PdN 3 — Analítica / Valor Agregado (CU-ANA-03..06)
            _miAnalisisExp    = NuevoItemForm("Análisis de experiencias", "mnu.ana.experiencias",  typeof(AnalisisExperienciasForm),  () => new AnalisisExperienciasForm());
            _miClientesRiesgo = NuevoItemForm("Clientes en riesgo",       "mnu.ana.riesgo",         typeof(ClientesEnRiesgoForm),      () => new ClientesEnRiesgoForm());
            _miCancelaciones  = NuevoItemForm("Análisis de cancelaciones","mnu.ana.cancelaciones",  typeof(AnalisisCancelacionesForm), () => new AnalisisCancelacionesForm());
            _miAnalisisSusc   = NuevoItemForm("Análisis de suscripciones","mnu.ana.suscripciones",  typeof(AnalisisSuscripcionesForm), () => new AnalisisSuscripcionesForm());
            _miReportesComerciales = NuevoItemForm("Reportes comerciales","mnu.ana.comercial",      typeof(ReportesComercialesForm),  () => new ReportesComercialesForm());
        }

        // ── Reorganización de la navegación en BLOQUES DE NEGOCIO (reutiliza ítems/forms existentes) ──
        //   Panel de Control · Fidelización · Ventas/Operación · Analíticas · Administración · Sesión
        private void ReorganizarNavegacionPorBloques()
        {
            // 1 · FIDELIZACIÓN — relación con el cliente
            _mnuFidelizacion = new ToolStripMenuItem(Tx("mnu.blq.fidelizacion", "Fidelización")) { Tag = "mnu.blq.fidelizacion" };
            _mnuFidelizacion.DropDownItems.Add(clientesToolStripMenuItem);
            _mnuFidelizacion.DropDownItems.Add(planesToolStripMenuItem);
            _mnuFidelizacion.DropDownItems.Add(_miSuscripciones);
            // (Recomendaciones se movió a ANALÍTICA: es un CU de valor agregado, no de Fidelización.)

            // 2 · VENTAS / OPERACIÓN — comercialización y catálogos de experiencias
            _mnuOperacion = new ToolStripMenuItem(Tx("mnu.blq.operacion", "Ventas / Operación")) { Tag = "mnu.blq.operacion" };
            _mnuOperacion.DropDownItems.Add(prendasToolStripMenuItem);            // Experiencias
            _mnuOperacion.DropDownItems.Add(pedidosVentaToolStripMenuItem);       // Reservas
            _mnuOperacion.DropDownItems.Add(pedidosRealizadosToolStripMenuItem);  // Reservas Realizadas
            _mnuOperacion.DropDownItems.Add(_miListaEspera);
            _mnuOperacion.DropDownItems.Add(new ToolStripSeparator());
            _mnuOperacion.DropDownItems.Add(_miOrganizadores);
            _mnuOperacion.DropDownItems.Add(_miCategorias);
            _mnuOperacion.DropDownItems.Add(_miCiudades);

            // 3 · ANALÍTICAS — datos operativos → información para decidir
            _mnuAnaliticas = new ToolStripMenuItem(Tx("mnu.blq.analiticas", "Analíticas")) { Tag = "mnu.blq.analiticas" };
            bitacoraToolStripMenuItem.DropDownItems.Remove(reporteJornadaToolStripMenuItem); // sale de Bitácora
            _mnuAnaliticas.DropDownItems.Add(reporteJornadaToolStripMenuItem);  // CU-ANA-01
            _mnuAnaliticas.DropDownItems.Add(_miRecomendaciones);   // CU-ANA-02 (reubicado desde Fidelización)
            _mnuAnaliticas.DropDownItems.Add(new ToolStripSeparator());
            _mnuAnaliticas.DropDownItems.Add(_miAnalisisExp);       // CU-ANA-03
            _mnuAnaliticas.DropDownItems.Add(_miClientesRiesgo);    // CU-ANA-04
            _mnuAnaliticas.DropDownItems.Add(_miCancelaciones);     // CU-ANA-05
            _mnuAnaliticas.DropDownItems.Add(_miAnalisisSusc);      // CU-ANA-06
            _mnuAnaliticas.DropDownItems.Add(_miReportesComerciales); // CU-ANA-07
            // (El Dashboard es el "Panel de Control" top-level: punto de entrada/resumen.)

            // 4 · ADMINISTRACIÓN (técnica) — reusa "Administrar" y le suma la Bitácora
            gestionToolStripMenuItem.Tag  = "mnu.administracion";
            gestionToolStripMenuItem.Text = Tx("mnu.administracion", "Administración");
            // Quitar separadores sueltos que hayan quedado en Bitácora tras mover el Reporte.
            for (int i = bitacoraToolStripMenuItem.DropDownItems.Count - 1; i >= 0; i--)
                if (bitacoraToolStripMenuItem.DropDownItems[i] is ToolStripSeparator)
                    bitacoraToolStripMenuItem.DropDownItems.RemoveAt(i);
            gestionToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            gestionToolStripMenuItem.DropDownItems.Add(bitacoraToolStripMenuItem);

            // ── Reconstruir la barra superior en orden de negocio ──
            menuStrip1.Items.Clear();
            menuStrip1.Items.Add(panelControlToolStripMenuItem);   // Dashboard / inicio
            menuStrip1.Items.Add(_mnuFidelizacion);
            menuStrip1.Items.Add(_mnuOperacion);
            menuStrip1.Items.Add(_mnuAnaliticas);
            menuStrip1.Items.Add(gestionToolStripMenuItem);        // Administración
            menuStrip1.Items.Add(usuarioToolStripMenuItem);        // Sesión (Mi Perfil / Cerrar sesión)
            menuStrip1.Items.Add(_alertasItem);                    // Alertas (alineado a la derecha)
        }

        // Codificación por COLOR de la paleta según el cuatrimestre en que se implementó cada módulo,
        // para que el docente identifique de un vistazo qué pertenece a cada entrega:
        //   VERDE  = 1er cuatrimestre — módulos técnicos / infraestructura (T02, T04, T05, T06, T07/T08).
        //   CORAL  = 2do cuatrimestre — dominio de negocio de ExperienceHub.
        // Todo lo demás (transversal: Panel de Control, Sesión) queda en azul oscuro.
        // Genera un puntito de color (círculo) para marcar el cuatrimestre sin alterar el texto.
        private static Image PuntoCuatri(Color c)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(c)) g.FillEllipse(br, 4, 4, 8, 8);
            }
            return bmp;
        }

        private void CodificarMenuPorCuatrimestre()
        {
            // El TEXTO queda en azul oscuro (estética del programa); solo un puntito discreto a la
            // izquierda marca el cuatrimestre. Los ítems padre también lo llevan.
            //   AZUL    = 1er cuatri (técnico)  ·  NARANJA = 2do cuatri (negocio)
            Image dotV = PuntoCuatri(Color.FromArgb(59, 130, 246));  // azul técnico (paleta de categorías)
            Image dotC = PuntoCuatri(Estilo.Naranja);                // naranja negocio
            void D(ToolStripItem it, Image dot) { if (it != null) it.Image = dot; }

            // 2do cuatrimestre — NEGOCIO (bloques Fidelización / Operación / Analíticas y sus ítems)
            D(_mnuFidelizacion, dotC); D(_mnuOperacion, dotC); D(_mnuAnaliticas, dotC);
            D(clientesToolStripMenuItem,          dotC);
            D(planesToolStripMenuItem,            dotC);
            D(_miSuscripciones,                   dotC);
            D(_miRecomendaciones,                 dotC);
            D(prendasToolStripMenuItem,           dotC);   // Experiencias
            D(pedidosVentaToolStripMenuItem,      dotC);   // Reservas
            D(pedidosRealizadosToolStripMenuItem, dotC);   // Reservas Realizadas
            D(_miListaEspera,                     dotC);
            D(_miOrganizadores,                   dotC);
            D(_miCategorias,                      dotC);
            D(_miCiudades,                        dotC);
            D(reporteJornadaToolStripMenuItem,    dotC);   // Reporte (analítica)
            D(_miAnalisisExp,                     dotC);   // CU-ANA-03
            D(_miClientesRiesgo,                  dotC);   // CU-ANA-04
            D(_miCancelaciones,                   dotC);   // CU-ANA-05
            D(_miAnalisisSusc,                    dotC);   // CU-ANA-06
            D(_miReportesComerciales,             dotC);   // CU-ANA-07

            // 1er cuatrimestre — TÉCNICO (bloque Administración y sus ítems)
            D(gestionToolStripMenuItem,           dotV);
            D(_grpUsuarios,                       dotV);
            D(usuariosToolStripMenuItem,          dotV);
            D(_adminUsuariosItem,                 dotV);
            D(perfilesToolStripMenuItem,          dotV);
            D(_grpSistema,                        dotV);
            D(idiomasToolStripMenuItem,           dotV);
            D(historialUsuariosToolStripMenuItem, dotV);
            D(backupToolStripMenuItem,            dotV);
            D(integridadToolStripMenuItem,        dotV);
            D(bitacoraToolStripMenuItem,          dotV);
            D(bitSistemaToolStripMenuItem,        dotV);
            D(bitNegocioToolStripMenuItem,        dotV);

            // Leyenda a la derecha del menú (texto azul oscuro + puntito), para el docente.
            menuStrip1.Items.Add(new ToolStripLabel("2° cuatri") { Image = dotC, ForeColor = Estilo.AzulOscuro, Alignment = ToolStripItemAlignment.Right });
            menuStrip1.Items.Add(new ToolStripLabel("1° cuatri") { Image = dotV, ForeColor = Estilo.AzulOscuro, Alignment = ToolStripItemAlignment.Right });
        }

        private void panelControlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is DashboardForm)
                { hijo.BringToFront(); return; }
            }
            CrearDashboardDelRol().Show();
        }

        // Panel de control único: el DashboardForm genérico se adapta por permisos.
        private Form CrearDashboardDelRol()
        {
            var permisos = _usuarioActivo?.Permisos;
            Form dash = new DashboardForm(permisos) { MdiParent = this };
            return dash;
        }

        private void bitSistemaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is Bitacora b) { b.SeleccionarTab("sistema"); b.BringToFront(); return; }
            }
            new Bitacora("sistema") { MdiParent = this }.Show();
        }

        private void bitNegocioToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is Bitacora b) { b.SeleccionarTab("negocio"); b.BringToFront(); return; }
            }
            new Bitacora("negocio") { MdiParent = this }.Show();
        }

        private void reporteJornadaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is ReporteJornadaForm) { hijo.BringToFront(); return; }
            }
            new ReporteJornadaForm(_usuarioActivo?.Permisos) { MdiParent = this }.Show();
        }

        /// <summary>
        /// Abre Gestión de Usuarios como hijo MDI. Accesible solo para Administrador.
        /// </summary>
        private void usuariosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is Usuarios) { hijo.BringToFront(); return; }
            }
            new Usuarios { MdiParent = this }.Show();
        }

        // Abre el panel de Administración de Usuarios (ABM de datos no sensibles + cambiar rol + historial).
        private void AdminUsuarios_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is AdministracionUsuariosForm) { hijo.BringToFront(); return; }
            }
            new AdministracionUsuariosForm { MdiParent = this }.Show();
        }

        /// <summary>
        /// Abre el Gestor de Perfiles y Permisos como hijo MDI — T04 Composite Pattern.
        /// Accesible solo para Administrador.
        /// </summary>
        private void perfilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is GestorPermisos) { hijo.BringToFront(); return; }
            }
            new GestorPermisos { MdiParent = this }.Show();
        }

        private void idiomasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is FormIdiomas) { hijo.BringToFront(); return; }
            }
            new FormIdiomas { MdiParent = this }.Show();
        }

        private void historialUsuariosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is VersionHistorialForm) { hijo.BringToFront(); return; }
            }
            new VersionHistorialForm { MdiParent = this }.Show();
        }

        private void backupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new BackupForm())
                form.ShowDialog(this);
        }

        /// <summary>
        /// Abre el módulo de Prendas como hijo MDI.
        /// </summary>
        private void prendasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is Experiencias) { hijo.BringToFront(); return; }
            }
            new Experiencias { MdiParent = this }.Show();
        }

        /// <summary>
        /// Abre el módulo de Outfits como hijo MDI.
        /// TODO: implementar cuando se cree el formulario Outfits.
        /// </summary>
        private void outfitsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tM = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            string T_m(string k, string fb) => tM.ContainsKey(k) ? tM[k].Texto : fb;
            MessageBox.Show(
                T_m("msg.modulo.outfits",    "El módulo de Outfits aún no está disponible."),
                T_m("lbl.proximamente",       "Próximamente"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Abre el módulo de Categorías como hijo MDI.
        /// TODO: implementar cuando se cree el formulario Categorias.
        /// </summary>
        private void categoriasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tM = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            string T_m(string k, string fb) => tM.ContainsKey(k) ? tM[k].Texto : fb;
            MessageBox.Show(
                T_m("msg.modulo.categorias", "El módulo de Categorías aún no está disponible."),
                T_m("lbl.proximamente",       "Próximamente"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Abre el módulo de Clientes como hijo MDI. Accesible para Vendedor.
        /// </summary>
        private void clientesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is Clientes) { hijo.BringToFront(); return; }
            }
            new Clientes { MdiParent = this }.Show();
        }

        /// <summary>
        /// Abre el módulo de Planes de Suscripción como hijo MDI.
        /// </summary>
        private void planesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is Planes) { hijo.BringToFront(); return; }
            }
            new Planes { MdiParent = this }.Show();
        }

        /// <summary>
        /// Abre el módulo de Pedidos de Venta como hijo MDI. Accesible para Vendedor.
        /// </summary>
        private void pedidosVentaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is Reservas) { hijo.BringToFront(); return; }
            }
            new Reservas { MdiParent = this }.Show();
        }

        /// <summary>
        /// Abre el módulo de Pedidos Realizados como hijo MDI.
        /// Accesible para OperadorDeInventario (mnuPedidosRealizados).
        /// </summary>
        private void pedidosRealizadosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is ReservasRealizadas) { hijo.BringToFront(); return; }
            }
            new ReservasRealizadas { MdiParent = this }.Show();
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
            CodificarMenuPorCuatrimestre();
            if (_tsIdioma != null) { _tsIdioma.BackColor = Estilo.Gris100; }
            if (_lblIdioma != null) { _lblIdioma.ForeColor = Estilo.Gris700; }

            // 2. Observer + timer de integridad
            GestorIdioma.SuscribirObservador(this);
            _timerIntegridad = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
            _timerIntegridad.Tick += TimerIntegridad_Tick;
            _timerIntegridad.Start();

            // 3. Abrir dashboard inmediatamente — sus datos cargan en background
            var dash = CrearDashboardDelRol();
            dash.Show();
            try { PreferenciasUI.Aplicar(dash); } catch { }

            // Calcular el badge del Centro de Alertas (en background, vía BLL).
            ActualizarBadgeAlertas();

            // 4. Cargar traducciones de BD en background (no bloquea la UI)
            // RF-22/23 — Al ingresar se aplica el idioma PREFERIDO del usuario (persistido en BD).
            // El login queda en el idioma elegido en esa pantalla; el Menú ya abre en el del usuario.
            string codigoPref = _usuarioActivo?.IdIdioma ?? GestorIdioma.IdiomaActual?.Id ?? "ES";
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

        private void integridadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is DiagnosticoIntegridadForm) { hijo.BringToFront(); return; }
            }
            new DiagnosticoIntegridadForm { MdiParent = this }.Show();
        }

        /// <summary>
        /// Recibe la notificación del GestorIdioma cuando el idioma cambia.
        /// Equivalente a UpdateLanguage(IIdioma idioma) del ejemplo de cátedra.
        /// </summary>
        public void UpdateLanguage(Idioma idioma)
        {
            // Reconstruir el combo por si cambió el CONJUNTO de idiomas disponibles
            // (p. ej. el admin activó/creó un idioma en Gestión de Idiomas): así el nuevo
            // idioma aparece al instante en el dropdown, sin reiniciar la aplicación.
            if (GestorIdioma.IdiomasDisponibles != null && GestorIdioma.IdiomasDisponibles.Count > 0)
                ReconstruirComboIdioma(GestorIdioma.IdiomasDisponibles);
            Traducir(idioma);
            SeleccionarIdiomaEnCombo(idioma.Id);
        }

        /// <summary>
        /// Reasigna el .Text de cada ítem del menú leyendo su propiedad Tag como
        /// clave de traducción — exactamente igual que en el ejemplo de cátedra (frmMain).
        /// Los Tags se asignan en el Designer; el código no hardcodea ninguna clave.
        /// </summary>
        private void Traducir(Idioma idioma)
        {
            var t = Traductor.ObtenerTraducciones(idioma);

            // Label dinámico "Idioma:" / "Language:" / "Язык:"
            if (t.ContainsKey("lbl.idioma"))
                _lblIdioma.Text = t["lbl.idioma"].Texto;

            // Item "Mi Perfil" (creado en código, sin Tag del Designer)
            if (_miPerfilItem != null)
                _miPerfilItem.Text = t.ContainsKey("perfil.menu") ? t["perfil.menu"].Texto : "Mi Perfil";

            Aplicar(usuarioToolStripMenuItem,           t);
            Aplicar(panelControlToolStripMenuItem,      t);
            Aplicar(inventarioToolStripMenuItem,        t);
            Aplicar(prendasToolStripMenuItem,           t);
            Aplicar(ventasToolStripMenuItem,            t);
            Aplicar(clientesToolStripMenuItem,          t);
            Aplicar(planesToolStripMenuItem,            t);
            Aplicar(pedidosVentaToolStripMenuItem,      t);
            Aplicar(pedidosRealizadosToolStripMenuItem, t);
            Aplicar(gestionToolStripMenuItem,           t);
            Aplicar(_grpUsuarios,                       t);
            Aplicar(_grpSistema,                        t);
            Aplicar(usuariosToolStripMenuItem,          t);
            Aplicar(_adminUsuariosItem,                 t);
            Aplicar(perfilesToolStripMenuItem,          t);
            Aplicar(idiomasToolStripMenuItem,           t);
            Aplicar(historialUsuariosToolStripMenuItem, t);
            Aplicar(backupToolStripMenuItem,            t);
            Aplicar(integridadToolStripMenuItem,        t);
            Aplicar(bitacoraToolStripMenuItem,          t);
            Aplicar(bitSistemaToolStripMenuItem,        t);
            Aplicar(bitNegocioToolStripMenuItem,        t);
            Aplicar(reporteJornadaToolStripMenuItem,    t);
            Aplicar(cerrarSesionToolStripMenuItem,      t);

            // Bloques de negocio (creados por código) + ítems de catálogo redistribuidos.
            Aplicar(_mnuFidelizacion,  t);
            Aplicar(_mnuOperacion,     t);
            Aplicar(_mnuAnaliticas,    t);
            Aplicar(_miOrganizadores,  t);
            Aplicar(_miCategorias,     t);
            Aplicar(_miCiudades,       t);
            Aplicar(_miSuscripciones,  t);
            Aplicar(_miListaEspera,    t);
            Aplicar(_miRecomendaciones,t);
            Aplicar(_miAnalisisExp,    t);
            Aplicar(_miClientesRiesgo, t);
            Aplicar(_miCancelaciones,  t);
            Aplicar(_miAnalisisSusc,   t);
            Aplicar(_miReportesComerciales, t);

            // Menú "Catálogos" (construido por código): traducir el grupo y sus ítems por Tag.
            if (_catalogosMenu != null)
            {
                Aplicar(_catalogosMenu, t);
                foreach (ToolStripItem sub in _catalogosMenu.DropDownItems)
                    if (sub is ToolStripMenuItem mi) Aplicar(mi, t);
            }

            // Ítem de Alertas: tiene icono + badge, se compone aparte (no por Tag directo).
            RefrescarTextoAlertas();
        }

        /// <summary>
        /// Lee el Tag del ítem para obtener la clave y aplica la traducción.
        /// Equivalente al patrón if (item.Tag != null && traducciones.ContainsKey(...))
        /// del ejemplo de cátedra — el Tag actúa como clave del diccionario.
        /// </summary>
        private static void Aplicar(ToolStripMenuItem item,
            IDictionary<string, Traduccion> t)
        {
            if (item != null && item.Tag != null && t.ContainsKey(item.Tag.ToString()))
                item.Text = t[item.Tag.ToString()].Texto;
        }

        // ── Helpers de la barra de idioma (dropdown) ──────────────────────────

        /// <summary>
        /// Llena el combo del ToolStrip con los idiomas (de BD o fallback). Un idioma nuevo
        /// aparece solo, sin tocar este código.
        /// </summary>
        private void ReconstruirComboIdioma(IList<Idioma> idiomas)
        {
            if (_cmbIdiomaMenu == null) return;
            _suprimirIdiomaMenu = true;
            _cmbIdiomaMenu.ComboBox.DataSource    = null;
            _cmbIdiomaMenu.ComboBox.DisplayMember = "Nombre";
            _cmbIdiomaMenu.ComboBox.ValueMember   = "Id";
            _cmbIdiomaMenu.ComboBox.DataSource    = new List<Idioma>(idiomas);
            _suprimirIdiomaMenu = false;
            SeleccionarIdiomaEnCombo(GestorIdioma.IdiomaActual?.Id ?? "ES");
        }

        /// <summary>Selecciona en el combo el idioma indicado, SIN disparar el cambio (uso interno).</summary>
        private void SeleccionarIdiomaEnCombo(string codigo)
        {
            if (_cmbIdiomaMenu?.ComboBox?.Items == null) return;
            for (int i = 0; i < _cmbIdiomaMenu.ComboBox.Items.Count; i++)
                if (_cmbIdiomaMenu.ComboBox.Items[i] is Idioma idm &&
                    string.Equals(idm.Id, codigo, StringComparison.OrdinalIgnoreCase))
                {
                    if (_cmbIdiomaMenu.SelectedIndex != i)
                    {
                        bool prev = _suprimirIdiomaMenu;
                        _suprimirIdiomaMenu = true;
                        _cmbIdiomaMenu.SelectedIndex = i;
                        _suprimirIdiomaMenu = prev;
                    }
                    break;
                }
        }

        /// <summary>
        /// Cambio de idioma desde el combo: aplica vía Observer (todos los forms se traducen)
        /// y persiste la preferencia del usuario en BD.
        /// </summary>
        private void CmbIdiomaMenu_Changed(object sender, EventArgs e)
        {
            if (_suprimirIdiomaMenu) return;
            var idioma = _cmbIdiomaMenu.SelectedItem as Idioma;
            if (idioma == null) return;

            try
            {
                var dictTrad = new BLL.IdiomaService().CargarTraducciones(idioma.Id);
                GestorIdioma.CambiarIdioma(idioma, dictTrad);
            }
            catch { GestorIdioma.CambiarIdioma(idioma); }

            try
            {
                if (_usuarioActivo != null)
                    new BLL.Usuario().GuardarPreferenciaIdioma(_usuarioActivo.Id, idioma.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[Menu] Error al guardar preferencia de idioma: {ex.Message}");
            }
        }

    }
}

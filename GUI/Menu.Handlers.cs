using System;
using System.Windows.Forms;

namespace GUI
{
    /// <summary>
    /// GUI.Menu — parcial de HANDLERS de navegación: cada ítem del menú abre (o trae al frente)
    /// su formulario como hijo MDI, reutilizando la instancia si ya está abierta. Sin lógica de
    /// negocio: la construcción del menú y la seguridad viven en Menu.cs; las traducciones en
    /// Menu.Idiomas.cs.
    /// </summary>
    public partial class Menu
    {
        // El Panel de Control (Dashboard) se retiró: el landing post-login es el menú sin hijo MDI.
        private void panelControlToolStripMenuItem_Click(object sender, EventArgs e) { }

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
        private void experienciasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is Experiencias) { hijo.BringToFront(); return; }
            }
            new Experiencias { MdiParent = this }.Show();
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
        private void reservasToolStripMenuItem_Click(object sender, EventArgs e)
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
        private void reservasRealizadasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is ReservasRealizadas) { hijo.BringToFront(); return; }
            }
            new ReservasRealizadas { MdiParent = this }.Show();
        }

        private void integridadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form hijo in this.MdiChildren)
            {
                if (hijo is DiagnosticoIntegridadForm) { hijo.BringToFront(); return; }
            }
            new DiagnosticoIntegridadForm { MdiParent = this }.Show();
        }
    }
}

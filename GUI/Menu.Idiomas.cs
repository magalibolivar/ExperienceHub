using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// GUI.Menu — parcial del PATRÓN OBSERVER (T05, Gestión de Múltiples Idiomas).
    ///
    /// Recibe las notificaciones del GestorIdioma (UpdateLanguage) y retraduce todos los ítems
    /// del menú leyendo su Tag como clave de traducción. También gestiona el combo de idioma de
    /// la barra superior. La suscripción/desuscripción al GestorIdioma se hace en el ciclo de
    /// vida del formulario (OnLoad/OnFormClosing, en Menu.cs).
    /// </summary>
    public partial class Menu
    {
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
            Aplicar(experienciasToolStripMenuItem,           t);
            Aplicar(clientesToolStripMenuItem,          t);
            Aplicar(planesToolStripMenuItem,            t);
            Aplicar(reservasToolStripMenuItem,      t);
            Aplicar(reservasRealizadasToolStripMenuItem, t);
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
            Aplicar(_mnuComercializacion, t);
            Aplicar(_mnuReservas,         t);
            Aplicar(_miOrganizadores,  t);
            Aplicar(_miCategorias,     t);
            Aplicar(_miCiudades,       t);
            Aplicar(_miMembresia,      t);
            Aplicar(_miListaEspera,    t);

            // Menú "Catálogos" (construido por código): traducir el grupo y sus ítems por Tag.
            if (_catalogosMenu != null)
            {
                Aplicar(_catalogosMenu, t);
                foreach (ToolStripItem sub in _catalogosMenu.DropDownItems)
                    if (sub is ToolStripMenuItem mi) Aplicar(mi, t);
            }

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

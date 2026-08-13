using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// Helper de traducción para los formularios code-only de ExperienceHub.
    /// Reutiliza el motor Observer/Traductor existente: cada control cuyo Tag sea una
    /// clave "eh.*" se traduce solo. Así los forms nuevos participan del cambio de idioma
    /// en caliente igual que los formularios clásicos.
    /// </summary>
    internal static class I18n
    {
        public static string T(string clave, string fallback)
        {
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            return t != null && t.ContainsKey(clave) ? t[clave].Texto : fallback;
        }

        /// <summary>Recorre el árbol de controles y traduce los que tengan un Tag "eh.*".</summary>
        public static void TraducirControles(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c.Tag is string clave && clave.StartsWith("eh."))
                    c.Text = T(clave, c.Text);
                if (c is DataGridView dgv) Estilo.TraducirEncabezados(dgv);
                if (c.HasChildren) TraducirControles(c);
            }
        }
    }
}

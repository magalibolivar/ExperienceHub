using System;
using System.Drawing;
using System.Windows.Forms;

namespace GUI.Ayuda
{
    /// <summary>
    /// Diálogo de ayuda CONTEXTUAL: compacto, modal, se cierra con un clic. Responde las tres preguntas
    /// que pide el sistema de ayuda (sección 8): dónde estoy, qué puedo hacer acá, cuál es el próximo
    /// paso. Nunca se muestra solo — el usuario lo abre a propósito con el botón "?" del encabezado
    /// (<see cref="UI.Encabezado"/>), nunca aparece automáticamente al cargar una pantalla. Misma
    /// identidad visual que el resto de ExperienceHub (Montserrat, AzulOscuro/Coral, TarjetaPanel).
    /// </summary>
    internal sealed class AyudaDialog : Form
    {
        public AyudaDialog(string titulo, string cuerpo, string[] pasos)
        {
            Text = I18n.T("eh.ayuda.titulo", "Ayuda");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Estilo.BlancoCalido;
            Size = new Size(460, pasos != null && pasos.Length > 0 ? 400 : 300);
            ShowInTaskbar = false;
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var barra = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Estilo.Coral };

            var lblIco = new Label
            {
                Text = "?", Font = Estilo.SemiBold(22f), ForeColor = Estilo.Coral,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(44, 44), Location = new Point(22, 26)
            };
            var lblTit = new Label
            {
                Text = titulo, Font = Estilo.SemiBold(13f), ForeColor = Estilo.AzulOscuro,
                AutoSize = false, Location = new Point(78, 30), Size = new Size(360, 36)
            };
            var lblCuerpo = new Label
            {
                Text = cuerpo, Font = Estilo.Normal(9.5f), ForeColor = Estilo.Gris700,
                AutoSize = false, Location = new Point(22, 78), Size = new Size(416, 80)
            };

            Controls.Add(barra);
            Controls.Add(lblIco);
            Controls.Add(lblTit);
            Controls.Add(lblCuerpo);

            int y = 166;
            if (pasos != null && pasos.Length > 0)
            {
                var lblPasosTit = new Label
                {
                    Text = I18n.T("eh.ayuda.quepodeshacer", "Qué podés hacer acá"),
                    Font = Estilo.Bold(8.5f), ForeColor = Estilo.Gris700,
                    AutoSize = false, Location = new Point(22, y), Size = new Size(300, 18)
                };
                Controls.Add(lblPasosTit);
                y += 24;
                foreach (var paso in pasos)
                {
                    var lblPaso = new Label
                    {
                        Text = "•  " + paso, Font = Estilo.Normal(9f), ForeColor = Estilo.Gris700,
                        AutoSize = false, Location = new Point(22, y), Size = new Size(416, 32)
                    };
                    Controls.Add(lblPaso);
                    y += 30;
                }
            }

            var btn = UI.Primario(I18n.T("eh.ayuda.entendido", "Entendido"), "eh.ayuda.entendido", (s, e) => Close());
            btn.DialogResult = DialogResult.OK;
            btn.Location = new Point(Width - 150, Height - 56);
            Estilo.EstilizarBoton(btn);
            Controls.Add(btn);
            AcceptButton = btn;
            CancelButton = btn;
        }

        /// <summary>Abre el diálogo de ayuda para el contexto indicado (ver <see cref="AyudaContenido"/>).
        /// No hace nada si el contexto no tiene contenido registrado (evita una ventana vacía).</summary>
        public static void Mostrar(IWin32Window propietario, string contexto)
        {
            var c = AyudaContenido.Obtener(contexto);
            if (c == null) return;
            using (var dlg = new AyudaDialog(c.Value.Titulo, c.Value.Cuerpo, c.Value.Pasos))
                dlg.ShowDialog(propietario);
        }
    }
}

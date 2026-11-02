using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>
    /// RF-10 — Diálogo de autodesbloqueo de un Administrador mediante una clave de emergencia
    /// de un solo uso (tipo códigos de respaldo de Steam / 2FA). Se abre desde el Login cuando
    /// la cuenta quedó bloqueada por intentos fallidos, sin depender de otro Administrador.
    /// </summary>
    public class DesbloqueoEmergenciaForm : FormBase
    {
        private const string PLACEHOLDER_CLAVE = "XXXX-XXXX-XXXX";
        private readonly BLL.RecuperacionAdmin _recBLL = new BLL.RecuperacionAdmin();

        private TextBox _txtUsuario;
        private TextBox _txtClave;
        private Button  _btnDesbloquear;
        private Button  _btnCancelar;
        private Label   _lblTitulo;
        private Label   _lblInfo;
        private Label   _lblUsuario;
        private Label   _lblClave;
        private Label   _lblError;

        public DesbloqueoEmergenciaForm(string usuarioSugerido = null)
        {
            BuildUI();
            if (!string.IsNullOrWhiteSpace(usuarioSugerido))
            {
                _txtUsuario.Text = usuarioSugerido;
                _txtClave.Select();
            }
        }

        private string T(string k, string fb)
        {
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            return t.ContainsKey(k) ? t[k].Texto : fb;
        }

        private void BuildUI()
        {
            this.Text            = T("emg.titulo", "Desbloqueo con clave de emergencia");
            this.ClientSize      = new Size(430, 286);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition   = FormStartPosition.CenterParent;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.Font            = new Font("Segoe UI", 9f);
            this.BackColor       = Color.White;

            _lblTitulo = new Label
            {
                Text      = T("emg.encabezado", "Cuenta de Administrador bloqueada"),
                Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 107, 107),
                Location  = new Point(20, 18),
                AutoSize  = true
            };

            _lblInfo = new Label
            {
                Text      = T("emg.info", "Ingresá tu usuario y una de tus claves de emergencia de un solo uso para desbloquear la cuenta."),
                Location  = new Point(22, 50),
                Size      = new Size(386, 40),
                ForeColor = Color.FromArgb(90, 90, 100)
            };

            _lblUsuario = new Label { Text = T("emg.usuario", "Usuario:"), Location = new Point(22, 100), AutoSize = true };
            _txtUsuario = new TextBox { Location = new Point(24, 120), Width = 382 };

            _lblClave = new Label { Text = T("emg.clave", "Clave de emergencia:"), Location = new Point(22, 150), AutoSize = true };
            _txtClave = new TextBox
            {
                Location     = new Point(24, 170),
                Width        = 382,
                CharacterCasing = CharacterCasing.Upper,
                Font         = new Font("Consolas", 11f)
            };
            _txtClave.SetPlaceholder(PLACEHOLDER_CLAVE);

            _lblError = new Label
            {
                Location  = new Point(22, 200),
                Size      = new Size(386, 32),
                ForeColor = Color.FromArgb(229, 72, 77),
                Text      = string.Empty
            };

            _btnDesbloquear = new Button
            {
                Text      = T("emg.btn.desbloquear", "Desbloquear"),
                Location  = new Point(214, 240),
                Size      = new Size(120, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 107, 107),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            _btnDesbloquear.FlatAppearance.BorderSize = 0;
            _btnDesbloquear.Click += (s, e) => Desbloquear();

            _btnCancelar = new Button
            {
                Text      = T("btn.cancelar", "Cancelar"),
                Location  = new Point(340, 240),
                Size      = new Size(66, 34),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                _lblTitulo, _lblInfo, _lblUsuario, _txtUsuario,
                _lblClave, _txtClave, _lblError, _btnDesbloquear, _btnCancelar
            });

            this.AcceptButton = _btnDesbloquear;
            this.CancelButton = _btnCancelar;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);   // ícono + identidad visual SUNSET + preferencias, vía FormBase
            // Estilo.Aplicar repinta todos los TextBox en AzulOscuro; si el campo de clave todavía
            // muestra el placeholder, hay que devolverle el gris para que siga leyéndose como guía.
            if (_txtClave.Text == PLACEHOLDER_CLAVE) _txtClave.ForeColor = Color.Gray;
        }

        private void Desbloquear()
        {
            _lblError.Text = string.Empty;
            _btnDesbloquear.Enabled = false;
            try
            {
                bool ok = _recBLL.DesbloquearConClave(this.Text, _txtUsuario.Text, _txtClave.Text);
                if (ok)
                {
                    Estilo.Exito(this, T("emg.exito.titulo", "Cuenta desbloqueada"),
                        T("emg.exito", "Cuenta desbloqueada con éxito.\nYa podés iniciar sesión normalmente."));
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (BE.AppException ax)
            {
                var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
                _lblError.Text = t.ContainsKey(ax.Clave) ? t[ax.Clave].Texto : ax.Message;
            }
            catch (Exception ex)
            {
                _lblError.Text = ex.Message;
            }
            finally
            {
                _btnDesbloquear.Enabled = true;
            }
        }
    }

    // Pequeño helper para mostrar un placeholder (texto guía) en un TextBox.
    internal static class TextBoxPlaceholderExtensions
    {
        public static void SetPlaceholder(this TextBox txt, string placeholder)
        {
            void Apply()
            {
                if (string.IsNullOrEmpty(txt.Text))
                {
                    txt.ForeColor = Color.Gray;
                    txt.Text = placeholder;
                }
            }
            txt.GotFocus += (s, e) =>
            {
                if (txt.Text == placeholder) { txt.Text = string.Empty; txt.ForeColor = Color.Black; }
            };
            txt.LostFocus += (s, e) => Apply();
            Apply();
        }
    }
}

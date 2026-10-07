using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Módulo de Reservas Realizadas (code-only): registrar asistencia y calificar.</summary>
    public class ReservasRealizadas : FormBase, IIdiomaObserver
    {
        private readonly BLL.Reserva      _bll     = new BLL.Reserva();
        private readonly BLL.Calificacion _bllCal  = new BLL.Calificacion();
        private const string MODULO = "ReservasRealizadas";

        private DataGridView _grid;
        private Button _btnAsistio, _btnNoAsistio, _btnCalificar;

        public ReservasRealizadas()
        {
            Text = "Reservas Realizadas"; Size = new Size(1000, 580); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(820, 480);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            _grid.SelectionChanged += (s, e) => ActualizarBotones();
            _grid.CellFormatting += FormatearPuntaje;
            UI.ConectarEstadoVacio(_grid,
                "No hay reservas para gestionar.\nAcá aparecen las confirmadas (para asistencia) y las ya realizadas (para calificar).",
                "eh.empty.reservasreal");

            _btnAsistio   = UI.Exito   ("Marcó asistencia", "eh.btn.asistio",   (s, e) => Asistencia(true));
            _btnNoAsistio = UI.Peligro ("No asistió",       "eh.btn.noasistio", (s, e) => Asistencia(false));
            _btnCalificar = UI.Primario("Calificar",        "eh.btn.calificar", (s, e) => Calificar());

            var barra = UI.BarraAccionesLR(
                new[] { UI.Secundario("Refrescar", "eh.btn.refrescar", (s, e) => Cargar()) },
                new[] { _btnCalificar, _btnNoAsistio, _btnAsistio });

            var header = UI.Encabezado("Reservas Realizadas",
                "Registrá la asistencia del cliente y su calificación de la experiencia.",
                "eh.frm.reservasreal", "eh.rr.header.desc");

            Controls.Add(_grid);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.reservasreal", this.Text); I18n.TraducirControles(this); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            Cargar();
            Traducir();
        }

        private void Cargar()
        {
            try
            {
                var reservas = _bll.ObtenerRealizadas();        // solo Confirmadas / Asistió / No Asistió
                var puntajes = _bllCal.ObtenerPuntajesPorReserva();
                foreach (var r in reservas)
                    if (puntajes.TryGetValue(r.IdReserva, out int p)) r.Puntaje = p;

                _grid.DataSource = reservas;
                UI.Columnas(_grid,
                    ("NombreCliente", 22), ("NombreExperiencia", 28),
                    ("FechaHoraExperiencia", 18), ("Estado", 16), ("Puntaje", 12));
                if (_grid.Columns.Contains("FechaHoraExperiencia"))
                    _grid.Columns["FechaHoraExperiencia"].HeaderText = "Fecha de la experiencia";
                if (_grid.Columns.Contains("Puntaje"))
                    _grid.Columns["Puntaje"].HeaderText = "Calificación";
                ResaltarPendientesDeCalificar();
                ActualizarBotones();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Muestra la calificación como "★ N/5" (o "—" si todavía no se calificó).
        private void FormatearPuntaje(object s, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Puntaje") return;
            e.Value = (e.Value == null || e.Value == DBNull.Value) ? "—" : "★ " + e.Value + "/5";
            e.FormattingApplied = true;
            e.CellStyle.ForeColor = (e.Value as string) == "—" ? Estilo.Gris400 : Estilo.Naranja;
        }

        // Seguimiento post-experiencia: resalta las reservas con asistencia registrada que todavía
        // NO fueron calificadas (ámbar), para que el operador cierre el círculo pidiendo la opinión.
        private void ResaltarPendientesDeCalificar()
        {
            var ambar = Color.FromArgb(255, 249, 231);
            int pendientes = 0;
            foreach (DataGridViewRow fila in _grid.Rows)
            {
                if (fila.DataBoundItem is BE.Reserva r && r.Estado == BE.EstadoReserva.Asistio && r.Puntaje == null)
                {
                    fila.DefaultCellStyle.BackColor = ambar;
                    pendientes++;
                }
            }
            if (pendientes > 0)
                MostrarOk($"{pendientes} experiencia(s) con asistencia pendientes de calificar (resaltadas).");
        }

        private BE.Reserva Sel() => _grid.CurrentRow?.DataBoundItem as BE.Reserva;

        // Botones CONTEXTUALES: se habilitan según el estado de la reserva seleccionada, para no
        // dejar intentar acciones inválidas (que terminaban en excepción).
        private void ActualizarBotones()
        {
            var r = Sel();
            bool puedeAsistencia = r != null && r.PuedeRegistrarAsistencia();       // Confirmada
            bool puedeCalificar  = r != null && r.PuedeCalificarse() && r.Puntaje == null; // Asistió y sin calificar aún
            if (_btnAsistio   != null) _btnAsistio.Enabled   = puedeAsistencia;
            if (_btnNoAsistio != null) _btnNoAsistio.Enabled = puedeAsistencia;
            if (_btnCalificar != null) _btnCalificar.Enabled = puedeCalificar;
        }

        private void Asistencia(bool asistio)
        {
            var r = Sel(); if (r == null) { MostrarError("Seleccioná una reserva del listado."); return; }
            if (!r.PuedeRegistrarAsistencia())
            { MostrarError("Solo se registra la asistencia de reservas Confirmadas."); return; }
            try
            {
                _bll.RegistrarAsistencia(MODULO, r, asistio);
                MostrarOk(asistio ? "Asistencia registrada." : "Inasistencia registrada.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Calificar()
        {
            var r = Sel(); if (r == null) { MostrarError("Seleccioná una reserva del listado."); return; }
            if (!r.PuedeCalificarse())
            { MostrarError("Solo se puede calificar una reserva con asistencia registrada (estado «Asistió»)."); return; }
            if (!PedirCalificacion(out int puntaje, out string comentario)) return;
            try
            {
                _bllCal.Calificar(MODULO, r, puntaje, comentario);
                Estilo.Exito(this, "¡Gracias por calificar!", "Tu opinión ayuda a mejorar las recomendaciones.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Diálogo ÚNICO de calificación: nota (1–5) + comentario opcional en una sola pantalla.
        private bool PedirCalificacion(out int puntaje, out string comentario)
        {
            puntaje = 5; comentario = "";
            using (var dlg = new Form
            {
                Text = "Calificar experiencia", FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, 210),
                MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Color.White
            })
            {
                var lblP = new Label { Text = "Puntaje (1 a 5) *", Location = new Point(18, 18), AutoSize = true, ForeColor = Estilo.AzulOscuro, Font = Estilo.Bold(9.5f) };
                var num  = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 5, Location = new Point(18, 42), Width = 90 };
                var lblC = new Label { Text = "Comentario (opcional)", Location = new Point(18, 82), AutoSize = true, ForeColor = Estilo.AzulOscuro, Font = Estilo.Bold(9.5f) };
                var txt  = new TextBox { Location = new Point(18, 106), Width = 344, Height = 44, Multiline = true };
                var btnOk     = UI.Primario("Guardar",  "eh.btn.guardar",  null);
                var btnCancel = UI.Secundario("Cancelar","eh.btn.cancelar", null);
                btnOk.DialogResult = DialogResult.OK;      btnOk.Location = new Point(178, 162);
                btnCancel.DialogResult = DialogResult.Cancel; btnCancel.Location = new Point(288, 162);
                dlg.Controls.AddRange(new Control[] { lblP, num, lblC, txt, btnOk, btnCancel });
                dlg.AcceptButton = btnOk; dlg.CancelButton = btnCancel;
                Estilo.Aplicar(dlg);

                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                puntaje = (int)num.Value; comentario = txt.Text.Trim();
                return true;
            }
        }
    }
}

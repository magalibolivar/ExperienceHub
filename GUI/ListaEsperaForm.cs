using System;
using System.Drawing;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI
{
    /// <summary>Lista de espera por experiencia (code-only): confirmar la oferta de cupo (reglas 8 y 9).</summary>
    public class ListaEsperaForm : FormBase, IIdiomaObserver
    {
        private readonly BLL.ListaEspera _bll    = new BLL.ListaEspera();
        private readonly BLL.Experiencia _bllExp = new BLL.Experiencia();
        private const string MODULO = "ListaEspera";

        private ComboBox _cboExperiencia;
        private DataGridView _grid;

        public ListaEsperaForm()
        {
            Text = "Lista de Espera"; Size = new Size(900, 540); BackColor = Estilo.Lienzo;
            MinimumSize = new Size(760, 460);
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            _grid = UI.Grilla();
            UI.ConectarEstadoVacio(_grid, "Elegí una experiencia para ver su lista de espera.\nSi está completa, acá aparece la fila FIFO.", "eh.empty.listaespera");

            _cboExperiencia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboExperiencia.SelectedIndexChanged += (s, e) => Cargar();
            var filtros = UI.BarraFiltros(UI.FiltroCampo("Experiencia", _cboExperiencia, 340, "eh.lbl.experiencia"));

            var barra = UI.BarraAccionesLR(
                new[] { UI.Secundario("Refrescar", "eh.btn.refrescar", (s, e) => Cargar()) },
                new[]
                {
                    UI.Peligro("Rechazar oferta → siguiente", "eh.btn.rechazaroferta", (s, e) => Rechazar()),
                    UI.Primario("Confirmar oferta → reserva",  "eh.btn.confirmaroferta", (s, e) => Confirmar())
                });

            var header = UI.Encabezado("Lista de Espera",
                "Cuando se libera un cupo, se ofrece al primero de la fila. Confirmá su oferta para generar la reserva.",
                "eh.frm.listaespera", "eh.le.header.desc");

            Controls.Add(_grid);
            Controls.Add(filtros);
            Controls.Add(barra);
            Controls.Add(header);
        }

        public void UpdateLanguage(Idioma idioma) => Traducir();
        private void Traducir() { this.Text = I18n.T("eh.frm.listaespera", this.Text); I18n.TraducirControles(this); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GestorIdioma.DesuscribirObservador(this);
            base.OnFormClosing(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GestorIdioma.SuscribirObservador(this);
            try { _cboExperiencia.DataSource = _bllExp.ObtenerTodos(); _cboExperiencia.DisplayMember = "Nombre"; _cboExperiencia.ValueMember = "IdExperiencia"; }
            catch (Exception ex) { MostrarError(ex); }
            Cargar();
            Traducir();
        }

        private void Cargar()
        {
            try
            {
                if (_cboExperiencia.SelectedValue is int idExp)
                {
                    // Avanza la cola si la oferta vigente venció antes de mostrar la lista.
                    _bll.ProcesarVencimientos(idExp);
                    _grid.DataSource = _bll.ObtenerTodas(idExp);
                    UI.Columnas(_grid, ("Posicion", 10), ("NombreCliente", 38), ("Estado", 18),
                                       ("FechaIngreso", 17), ("FechaOferta", 17));
                }
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Confirmar()
        {
            var entrada = _grid.CurrentRow?.DataBoundItem as BE.ListaEspera;
            if (entrada == null) { MostrarError("Seleccioná una entrada de la lista."); return; }
            try
            {
                int idReserva = _bll.ConfirmarOferta(MODULO, entrada);
                Estilo.Exito(this, "¡Cupo confirmado!", $"Se creó la reserva #{idReserva} desde la lista de espera.");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void Rechazar()
        {
            var entrada = _grid.CurrentRow?.DataBoundItem as BE.ListaEspera;
            if (entrada == null) { MostrarError("Seleccioná la entrada con la oferta vigente."); return; }
            try
            {
                _bll.RechazarOferta(MODULO, entrada);
                MostrarOk("Oferta rechazada. El cupo se ofreció al siguiente de la fila (si había).");
                Cargar();
            }
            catch (Exception ex) { MostrarError(ex); }
        }
    }
}

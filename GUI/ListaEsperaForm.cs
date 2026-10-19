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
        private ComboBox _cboFiltroEstado;
        private Label _lblContador;
        private Button _btnConfirmar, _btnRechazar;
        private DataGridView _grid;
        private System.Collections.Generic.List<BE.ListaEspera> _todas = new System.Collections.Generic.List<BE.ListaEspera>();

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
            _cboFiltroEstado = UI.ComboFiltroEstados<BE.EstadoListaEspera>(EstadoLabel, "Todos");
            _cboFiltroEstado.SelectedIndexChanged += (s, e) => AplicarFiltro();
            _lblContador = UI.Contador();
            var filtros = UI.BarraFiltros(
                UI.FiltroCampo("Experiencia", _cboExperiencia, 300, "eh.lbl.experiencia"),
                UI.FiltroCampo("Estado", _cboFiltroEstado, 170, "eh.lbl.filtroestado"),
                UI.FiltroCampo(" ", _lblContador, 170));

            _btnRechazar  = UI.Peligro("Rechazar oferta → siguiente", "eh.btn.rechazaroferta", (s, e) => Rechazar());
            _btnConfirmar = UI.Primario("Confirmar oferta → reserva",  "eh.btn.confirmaroferta", (s, e) => Confirmar());
            var barra = UI.BarraAccionesLR(
                new[] { UI.Secundario("Refrescar", "eh.btn.refrescar", (s, e) => Cargar()) },
                new[] { _btnRechazar, _btnConfirmar });

            // Lectura de un vistazo: color por estado + botones habilitados según la fila elegida.
            UI.PintarFilasPorEstado<BE.ListaEspera>(_grid, le => ColorEstado(le.Estado));
            _grid.SelectionChanged += (s, e) => ActualizarBotones();

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
                    _todas = _bll.ObtenerTodas(idExp);
                }
                else _todas = new System.Collections.Generic.List<BE.ListaEspera>();
                AplicarFiltro();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        private void AplicarFiltro()
        {
            try
            {
                var estado = UI.EstadoSeleccionado<BE.EstadoListaEspera>(_cboFiltroEstado);
                var lista = estado.HasValue ? _todas.FindAll(le => le.Estado == estado.Value) : _todas;

                _grid.DataSource = lista;
                UI.Columnas(_grid, ("Posicion", 10), ("NombreCliente", 38), ("Estado", 18),
                                   ("FechaIngreso", 17), ("FechaOferta", 17));

                if (_lblContador != null)
                    _lblContador.Text = string.Format(I18n.T("eh.lbl.mostrando", "Mostrando {0} de {1}"), lista.Count, _todas.Count);
                ActualizarBotones();
            }
            catch (Exception ex) { MostrarError(ex); }
        }

        // Etiqueta legible de cada estado de la cola.
        private static string EstadoLabel(BE.EstadoListaEspera e)
        {
            switch (e)
            {
                case BE.EstadoListaEspera.Esperando:  return "Esperando";
                case BE.EstadoListaEspera.Ofrecido:   return "Ofrecido";
                case BE.EstadoListaEspera.Confirmado: return "Confirmado";
                case BE.EstadoListaEspera.Retirado:   return "Retirado";
                default: return e.ToString();
            }
        }

        // Color por estado: ofrecido resaltado (ámbar, es el accionable), confirmado verde, retirado rojo.
        private static Color ColorEstado(BE.EstadoListaEspera e)
        {
            switch (e)
            {
                case BE.EstadoListaEspera.Ofrecido:   return Estilo.Naranja;
                case BE.EstadoListaEspera.Confirmado: return Estilo.Verde;
                case BE.EstadoListaEspera.Retirado:   return Estilo.Rojo;
                default:                              return Estilo.Gris700;  // Esperando
            }
        }

        // Confirmar/Rechazar solo tienen sentido sobre una entrada con oferta vigente (Ofrecido).
        private void ActualizarBotones()
        {
            var entrada = _grid.CurrentRow?.DataBoundItem as BE.ListaEspera;
            bool ofrecida = entrada != null && entrada.Estado == BE.EstadoListaEspera.Ofrecido;
            if (_btnConfirmar != null) _btnConfirmar.Enabled = ofrecida;
            if (_btnRechazar  != null) _btnRechazar.Enabled  = ofrecida;
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

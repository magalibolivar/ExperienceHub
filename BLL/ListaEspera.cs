using System.Collections.Generic;

namespace BLL
{
    /// <summary>
    /// Lógica de negocio para la lista de espera de experiencias completas (reglas 8 y 9).
    /// </summary>
    public class ListaEspera
    {
        private readonly DAL.ListaEspera     dal      = new DAL.ListaEspera();
        private readonly DAL.Experiencia     dalExp   = new DAL.Experiencia();
        private readonly Servicios.Bitacora        bitacora    = new Servicios.Bitacora();
        private readonly Servicios.BitacoraNegocio bitacoraNeg = new Servicios.BitacoraNegocio();

        public List<BE.ListaEspera> ObtenerCola(int idExperiencia) => dal.ObtenerColaPorExperiencia(idExperiencia);

        /// <summary>Todas las entradas de la experiencia (para ver ofrecidos/confirmados).</summary>
        public List<BE.ListaEspera> ObtenerTodas(int idExperiencia) => dal.ObtenerPorExperiencia(idExperiencia);

        /// <summary>
        /// Regla 9 (cierre) — el cliente al que se le ofreció el cupo lo confirma: se crea la
        /// reserva real (consume el cupo liberado) y se marca la entrada como Confirmada.
        /// </summary>
        public int ConfirmarOferta(string modulo, BE.ListaEspera entrada)
        {
            if (entrada == null) throw new System.ArgumentNullException(nameof(entrada));
            if (entrada.Estado != BE.EstadoListaEspera.Ofrecido)
                throw new BE.AppException("err.bll.espera.no_ofrecido",
                    "Solo se puede confirmar una entrada a la que se le ofreció el cupo.");

            int idReserva = new Reserva().CrearReserva(modulo, entrada.IdCliente, entrada.IdExperiencia, 0);
            dal.CambiarEstado(entrada.IdListaEspera, BE.EstadoListaEspera.Confirmado);
            return idReserva;
        }

        /// <summary>
        /// Regla 8 — un cliente ingresa a la lista de espera de una experiencia completa.
        /// Solo se permite si la experiencia efectivamente no tiene cupos y no está ya en la cola.
        /// </summary>
        public int Ingresar(string modulo, int idExperiencia, int idCliente)
        {
            PermisosAccion.Exigir(BE.Patentes.ListaEspera, BE.Patentes.ListaEspera);

            var exp = dalExp.ObtenerPorId(idExperiencia)
                ?? throw new BE.AppException("err.bll.espera.experiencia_inexistente", "La experiencia no existe.");
            if (!exp.EstaCompleta())
                throw new BE.AppException("err.bll.espera.hay_cupo",
                    "La experiencia todavía tiene cupos; no corresponde lista de espera.");
            if (dal.YaEstaEnCola(idExperiencia, idCliente))
                throw new BE.AppException("err.bll.espera.ya_en_cola", "El cliente ya está en la lista de espera.");

            int pos = dal.Agregar(idExperiencia, idCliente);
            bitacora.Registrar(modulo, $"Ingreso a lista de espera — experiencia #{idExperiencia}, cliente #{idCliente} (pos. {pos})", BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.IngresoListaEspera,
                $"Cliente #{idCliente} ingresó a la lista de espera de '{exp.Nombre}' (posición {pos})",
                idExperiencia: idExperiencia, idCliente: idCliente);
            return pos;
        }

        /// <summary>El cliente al que se le ofreció el cupo lo confirma → se retira de la cola.</summary>
        public void MarcarConfirmado(int idListaEspera) => dal.CambiarEstado(idListaEspera, BE.EstadoListaEspera.Confirmado);

        /// <summary>El cliente se retira de la lista de espera.</summary>
        public void Retirar(int idListaEspera) => dal.CambiarEstado(idListaEspera, BE.EstadoListaEspera.Retirado);
    }
}

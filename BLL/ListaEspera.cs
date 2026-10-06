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

        // ── Parámetros de negocio configurables ────────────────────────────────
        /// <summary>
        /// Horas de vigencia de una oferta de cupo. Mientras no venza, el cupo liberado queda
        /// RETENIDO para el cliente ofrecido (CrearReserva rechaza que lo tome otro). Pasado el
        /// plazo sin confirmar, la oferta vence y el cupo se ofrece al siguiente de la fila.
        /// </summary>
        public const int HORAS_VIGENCIA_OFERTA = 48;

        public List<BE.ListaEspera> ObtenerCola(int idExperiencia) => dal.ObtenerColaPorExperiencia(idExperiencia);

        /// <summary>Todas las entradas de la experiencia (para ver ofrecidos/confirmados).</summary>
        public List<BE.ListaEspera> ObtenerTodas(int idExperiencia) => dal.ObtenerPorExperiencia(idExperiencia);

        /// <summary>
        /// Regla 9 (cierre) — el cliente al que se le ofreció el cupo lo confirma: se crea la
        /// reserva real por el MISMO camino atómico que una reserva normal (BLL.Reserva.CrearReserva,
        /// con todas sus validaciones y el descuento de cupo), y se marca la entrada como Confirmada.
        /// </summary>
        public int ConfirmarOferta(string modulo, BE.ListaEspera entrada)
        {
            if (entrada == null) throw new System.ArgumentNullException(nameof(entrada));
            if (!entrada.PuedeConfirmarse())
                throw new BE.AppException("err.bll.espera.no_ofrecido",
                    "Solo se puede confirmar una entrada a la que se le ofreció el cupo.");
            if (entrada.OfertaVencida(HORAS_VIGENCIA_OFERTA, System.DateTime.Now))
            {
                // La oferta ya venció: se avanza al siguiente en vez de confirmar a destiempo.
                CambiarYAvanzar(modulo, entrada, "La oferta venció; el cupo se ofrece al siguiente de la fila.");
                throw new BE.AppException("err.bll.espera.oferta_vencida",
                    "La oferta venció. El cupo se ofreció al siguiente cliente de la lista.");
            }

            int idReserva = new Reserva().CrearReserva(modulo, entrada.IdCliente, entrada.IdExperiencia, 0);
            dal.CambiarEstado(entrada.IdListaEspera, BE.EstadoListaEspera.Confirmado);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.PromocionListaEspera,
                $"Cupo de '{entrada.NombreExperiencia}' confirmado por {entrada.NombreCliente} → reserva #{idReserva}",
                idExperiencia: entrada.IdExperiencia, idCliente: entrada.IdCliente);
            return idReserva;
        }

        /// <summary>
        /// El cliente ofrecido RECHAZA el cupo: se retira de la cola y el cupo (que seguía retenido)
        /// se ofrece automáticamente al siguiente en espera (avance FIFO).
        /// </summary>
        public void RechazarOferta(string modulo, BE.ListaEspera entrada)
        {
            if (entrada == null) throw new System.ArgumentNullException(nameof(entrada));
            if (!entrada.PuedeRechazarse())
                throw new BE.AppException("err.bll.espera.no_ofrecido",
                    "Solo se puede rechazar una entrada a la que se le ofreció el cupo.");
            CambiarYAvanzar(modulo, entrada, $"{entrada.NombreCliente} rechazó el cupo ofrecido.");
        }

        /// <summary>
        /// Procesa el vencimiento de la oferta vigente de una experiencia: si pasó el plazo sin
        /// confirmarse, la entrada ofrecida se retira y el cupo se ofrece al siguiente de la fila.
        /// Se invoca de forma perezosa al abrir/refrescar la lista de espera.
        /// </summary>
        public void ProcesarVencimientos(int idExperiencia)
        {
            var oferta = dal.ObtenerOfertaVigente(idExperiencia);
            if (oferta != null && oferta.OfertaVencida(HORAS_VIGENCIA_OFERTA, System.DateTime.Now))
                CambiarYAvanzar("ListaEspera", oferta, "La oferta venció sin confirmarse.");
        }

        // Retira la entrada ofrecida y ofrece el cupo (aún retenido) al siguiente en espera.
        private void CambiarYAvanzar(string modulo, BE.ListaEspera entrada, string motivo)
        {
            dal.CambiarEstado(entrada.IdListaEspera, BE.EstadoListaEspera.Retirado);
            bitacora.Registrar(modulo,
                $"Lista de espera — experiencia #{entrada.IdExperiencia}: {motivo}", BE.Criticidad.Baja);
            Avanzar(modulo, entrada.IdExperiencia);
        }

        // Ofrece el cupo retenido al primero en espera (si lo hay). Si la cola quedó vacía, el cupo
        // simplemente permanece disponible en la experiencia (ya no hay a quién ofrecérselo).
        private void Avanzar(string modulo, int idExperiencia)
        {
            var siguiente = dal.ObtenerPrimeroEnEspera(idExperiencia);
            if (siguiente == null) return;
            dal.Ofrecer(siguiente.IdListaEspera, System.DateTime.Now);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.PromocionListaEspera,
                $"Cupo de '{siguiente.NombreExperiencia}' ofrecido a {siguiente.NombreCliente} (siguiente en la fila)",
                idExperiencia: idExperiencia, idCliente: siguiente.IdCliente);
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

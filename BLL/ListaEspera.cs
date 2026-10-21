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

        // ── Parámetros de negocio configurables (regla 5 · PN03) ────────────────
        /// <summary>
        /// Horas de vigencia de una oferta de cupo (configurable en [Parametro], default 24).
        /// Mientras no venza, el cupo liberado queda RETENIDO para el cliente ofrecido
        /// (CrearReserva rechaza que lo tome otro). Pasado el plazo sin confirmar, la oferta vence
        /// y el cupo se ofrece al siguiente de la fila.
        /// </summary>
        public static int HorasVigenciaOferta => Configuracion.HorasVencimientoOferta();

        /// <summary>
        /// Margen mínimo antes del inicio de la experiencia: una oferta vence (aunque no se haya
        /// cumplido el plazo) y no se ofrece a nadie más si falta menos de este margen, dejando el
        /// cupo libre para reserva directa (configurable en [Parametro], default 24).
        /// </summary>
        public static int MargenInicioHoras => Configuracion.MargenInicioOfertaHoras();

        public List<BE.ListaEspera> ObtenerCola(int idExperiencia) => dal.ObtenerColaPorExperiencia(idExperiencia);

        /// <summary>Vista orientada a tareas (PN02): todas las ofertas vigentes (Ofrecido), de cualquier
        /// experiencia. Solo lectura; la BLL media entre GUI y DAL.</summary>
        public List<BE.ListaEspera> ObtenerOfertasVigentes() => dal.ObtenerOfertasVigentes();

        /// <summary>Resumen de actividad por experiencia (Esperando/Ofrecido). Solo lectura.</summary>
        public List<BE.ResumenListaEspera> ObtenerResumenActivo() => dal.ObtenerResumenActivo();

        /// <summary>Todas las entradas de la experiencia (para ver ofrecidos/confirmados).</summary>
        public List<BE.ListaEspera> ObtenerTodas(int idExperiencia) => dal.ObtenerPorExperiencia(idExperiencia);

        /// <summary>
        /// Regla 9 (cierre) — el cliente al que se le ofreció el cupo lo confirma: se crea la
        /// reserva real por el MISMO camino atómico que una reserva normal (BLL.Reserva.CrearReserva,
        /// con todas sus validaciones y el descuento de cupo), y se marca la entrada como Confirmada.
        /// </summary>
        public int ConfirmarOferta(string modulo, BE.ListaEspera entrada)
        {
            PermisosAccion.Exigir(BE.Patentes.ListaEspera);
            if (entrada == null) throw new System.ArgumentNullException(nameof(entrada));
            if (!entrada.PuedeConfirmarse())
                throw new BE.AppException("err.bll.espera.no_ofrecido",
                    "Solo se puede confirmar una entrada a la que se le ofreció el cupo.");
            if (entrada.OfertaVencida(HorasVigenciaOferta, MargenInicioHoras, System.DateTime.Now))
            {
                // La oferta ya venció: se avanza al siguiente en vez de confirmar a destiempo.
                CambiarYAvanzar(modulo, entrada, "La oferta venció; el cupo se ofrece al siguiente de la fila.");
                throw new BE.AppException("err.bll.espera.oferta_vencida",
                    "La oferta venció. El cupo se ofreció al siguiente cliente de la lista.");
            }

            // Regla 4 — al confirmar se REVALIDA al cliente con todas las reglas de PN02 (suscripción
            // activa y vigente, cupo del plan, premium, edad, ciudad y solapamiento) por el mismo
            // camino atómico que una reserva normal. Si no pasa, la entrada queda Rechazada (Retirado)
            // y el cupo —aún retenido— se ofrece automáticamente al siguiente de la fila.
            // Punto 1 — la reserva y el paso de la entrada a Confirmado ocurren en UNA sola
            // transacción (CrearReserva → CrearConCupo recibe el IdListaEspera y la marca Confirmado
            // ahí mismo). Así no queda la ventana "reserva creada + entrada aún Ofrecido" que permitía
            // confirmar dos veces.
            int idReserva;
            try
            {
                idReserva = new Reserva().CrearReserva(modulo, entrada.IdCliente, entrada.IdExperiencia, 0,
                    idListaEsperaAConfirmar: entrada.IdListaEspera);
            }
            catch (BE.AppException ex)
            {
                // Un fallo de PERMISO o de SESIÓN no significa que el cliente no cumpla las reglas:
                // en ese caso se propaga el error sin tocar la cola (no se penaliza al cliente).
                if (ex.Clave == "err.bll.sin_permiso" || ex.Clave == "err.bll.sesion_expirada")
                    throw;

                // Regla 4 — el cliente no revalidó (suscripción, cupo del plan, premium, edad, ciudad
                // o solapamiento): la entrada queda Rechazada (Retirado) y el cupo —aún retenido— se
                // ofrece automáticamente al siguiente de la fila.
                CambiarYAvanzar(modulo, entrada,
                    $"{entrada.NombreCliente} no cumple las condiciones de reserva; el cupo se ofrece al siguiente.");
                throw;
            }

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
            PermisosAccion.Exigir(BE.Patentes.ListaEspera);
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
            if (oferta != null && oferta.OfertaVencida(HorasVigenciaOferta, MargenInicioHoras, System.DateTime.Now))
                CambiarYAvanzar("ListaEspera", oferta, "La oferta venció sin confirmarse.");
        }

        /// <summary>
        /// Procesa el vencimiento de ofertas de TODAS las experiencias con una oferta vigente.
        /// Pensado para correr al iniciar sesión (además del vencimiento perezoso al abrir el
        /// formulario), para que las ofertas no queden "colgadas" entre sesiones. Tolerante a fallos:
        /// un error puntual no debe impedir el login.
        /// </summary>
        public int ProcesarVencimientosGlobal()
        {
            int procesadas = 0;
            try
            {
                foreach (int idExp in dal.ObtenerExperienciasConOfertaVigente())
                {
                    var oferta = dal.ObtenerOfertaVigente(idExp);
                    if (oferta != null && oferta.OfertaVencida(HorasVigenciaOferta, MargenInicioHoras, System.DateTime.Now))
                    {
                        CambiarYAvanzar("ListaEspera", oferta, "La oferta venció sin confirmarse (barrido de inicio de sesión).");
                        procesadas++;
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[BLL.ListaEspera.ProcesarVencimientosGlobal] " + ex.Message);
            }
            return procesadas;
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

            // Regla 5 — si ya falta menos del margen para el inicio, no se ofrece a nadie: el cupo
            // queda libre para reserva directa (no hay tiempo para que el siguiente responda).
            if (!siguiente.HayMargenParaOferta(MargenInicioHoras, System.DateTime.Now))
            {
                bitacora.Registrar(modulo,
                    $"Lista de espera — experiencia #{idExperiencia}: cupo liberado sin ofrecer (a menos de {MargenInicioHoras} h del inicio); queda para reserva directa.",
                    BE.Criticidad.Baja);
                return;
            }

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
            PermisosAccion.Exigir(BE.Patentes.ListaEspera);

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

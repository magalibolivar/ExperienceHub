using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL
{
    /// <summary>
    /// Lógica de negocio para el ciclo de vida de reservas de experiencias.
    /// Concentra las reglas de negocio 1-7 y 9-10 (la 8 y la 11 viven en
    /// BLL.ListaEspera y BLL.Recomendacion respectivamente).
    /// </summary>
    public class Reserva : Interfaces.IReservaService
    {
        private readonly DAL.Reserva          dalReserva  = new DAL.Reserva();
        private readonly DAL.Experiencia      dalExp      = new DAL.Experiencia();
        private readonly DAL.Cliente          dalCliente  = new DAL.Cliente();
        private readonly DAL.Empleado         dalEmpleado = new DAL.Empleado();
        private readonly DAL.PlanSuscripcion  dalPlan     = new DAL.PlanSuscripcion();
        private readonly DAL.Suscripcion      dalSus      = new DAL.Suscripcion();
        private readonly DAL.ReservaHistorial dalHistorial = new DAL.ReservaHistorial();
        private readonly DAL.ListaEspera      dalEspera   = new DAL.ListaEspera();
        private readonly Servicios.Bitacora        bitacora    = new Servicios.Bitacora();
        private readonly Servicios.BitacoraNegocio bitacoraNeg = new Servicios.BitacoraNegocio();

        // ── Parámetros de negocio configurables ───────────────────────────────
        /// <summary>Regla 4: permitir reservar fuera de la ciudad del cliente.</summary>
        private const bool PERMITIR_FUERA_DE_CIUDAD = false;
        /// <summary>Regla 5: bloquear (true) o solo advertir (false) si el cliente ya asistió antes.</summary>
        private const bool BLOQUEAR_SI_YA_REALIZADA = true;
        /// <summary>Reglas 6-7: horas de anticipación que separan una cancelación "a tiempo" de una "tardía".</summary>
        private const int  HORAS_ANTICIPACION_CANCELACION = 48;
        /// <summary>
        /// PN01: si un cliente ya tiene un "pedido activo" no puede armar uno nuevo. En WardrobeFlow
        /// el pedido es físico (uno por vez); en ExperienceHub se reinterpreta como "reserva Pendiente
        /// sin resolver". Las Confirmadas NO bloquean (un cliente reserva varias experiencias).
        /// En true exige confirmar o cancelar la pendiente antes de armar otra.
        /// </summary>
        private const bool BLOQUEAR_SI_PEDIDO_PENDIENTE = true;

        // ── Consultas ─────────────────────────────────────────────────────────
        public List<BE.Reserva> ObtenerTodos()               => dalReserva.ObtenerTodos();

        /// <summary>
        /// Reservas que corresponden al módulo "Reservas Realizadas": las Confirmadas (listas para
        /// registrar asistencia) y las que ya tienen asistencia registrada (Asistió/No Asistió, para
        /// calificar). Excluye Pendientes y Canceladas, que no son gestionables acá.
        /// </summary>
        public List<BE.Reserva> ObtenerRealizadas()
            => dalReserva.ObtenerTodos().FindAll(r =>
                   r.Estado == BE.EstadoReserva.Confirmada ||
                   r.Estado == BE.EstadoReserva.Asistio ||
                   r.Estado == BE.EstadoReserva.NoAsistio);
        public List<BE.Reserva> ObtenerPorCliente(int id)    => dalReserva.ObtenerPorCliente(id);
        public BE.Reserva       ObtenerPorId(int id)         => dalReserva.ObtenerPorId(id);

        // ── PN01 · CU03-VEN — Consultar Situación del Cliente (solo lectura) ───
        /// <summary>
        /// Devuelve el estado comercial del cliente (plan, cupo disponible, vigencia y reserva
        /// pendiente si corresponde). No modifica nada. Permite al Vendedor decidir si avanzar a
        /// armar el pedido. Mapea los códigos de la spec a <see cref="BE.EstadoSituacionCliente"/>.
        /// </summary>
        public BE.SituacionCliente ConsultarSituacionCliente(int idCliente)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasEditar, BE.Patentes.Reservas);

            var situacion = BE.SituacionCliente.Resolver(dalCliente.ObtenerPorId(idCliente));

            // Enriquecer con el "pedido activo" (reserva Pendiente) — requiere consultar las reservas.
            if (situacion.Estado == BE.EstadoSituacionCliente.Ok)
            {
                var pendiente = dalReserva.ObtenerActivasConHorario(idCliente)
                                          .Find(r => r.Estado == BE.EstadoReserva.Pendiente);
                if (pendiente != null)
                {
                    situacion.TieneReservaActiva      = true;
                    situacion.IdReservaActiva         = pendiente.IdReserva;
                    situacion.NombreExperienciaActiva = pendiente.NombreExperiencia;
                }
            }
            return situacion;
        }

        // ── PN01 · CU01-DEP — Verificar Disponibilidad (solo lectura) ──────────
        /// <summary>
        /// Verifica, sin comprometer cupo, que la experiencia siga Programada y tenga lugares
        /// suficientes para los solicitados. La reserva efectiva (comprometer el cupo de forma
        /// atómica, resolviendo el conflicto de concurrencia de CU02-DEP) ocurre en
        /// <see cref="CrearReserva"/> → DAL.Reserva.CrearConCupo.
        /// </summary>
        public BE.ResultadoDisponibilidad VerificarDisponibilidad(int idExperiencia, int lugares)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasEditar, BE.Patentes.Reservas);

            var exp = dalExp.ObtenerPorId(idExperiencia);
            if (exp == null)
                throw new BE.AppException("err.bll.reserva.experiencia_inexistente",
                    "La experiencia seleccionada no existe.");

            return BE.ResultadoDisponibilidad.Evaluar(idExperiencia, exp.Estado, exp.CupoDisponible, lugares);
        }

        /// <summary>
        /// Decisión PURA (sin BD ni sesión): ¿el cliente tiene un "pedido activo" que impide armar
        /// uno nuevo? Con el bloqueo activo, una reserva en estado Pendiente bloquea; las Confirmadas
        /// no. Separada así para poder testearla sin BD (patrón PermisosAccion.PermiteAccion).
        /// </summary>
        public static bool TienePedidoActivoBloqueante(
            IEnumerable<BE.Reserva> reservasActivas, bool bloquearSiPendiente)
        {
            if (!bloquearSiPendiente || reservasActivas == null) return false;
            return reservasActivas.Any(r => r.Estado == BE.EstadoReserva.Pendiente);
        }

        // ── Crear Reserva (reglas 1-5 + premium + edad + invitados) ────────────
        public int CrearReserva(string modulo, int idCliente, int idExperiencia, int cantidadInvitados)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasEditar, BE.Patentes.Reservas);

            var cliente = ObtenerClienteValidado(idCliente);

            // PN01 — un cliente con un "pedido activo" (reserva Pendiente sin resolver) no puede armar
            // otro. Este chequeo ocurre al principio del proceso, no recién al formalizar.
            if (TienePedidoActivoBloqueante(dalReserva.ObtenerActivasConHorario(idCliente), BLOQUEAR_SI_PEDIDO_PENDIENTE))
                throw new BE.AppException("err.bll.reserva.pedido_activo",
                    "El cliente ya tiene una reserva pendiente sin confirmar. Confirmala o cancelala antes de armar una nueva.");

            var experiencia = ObtenerExperienciaValidada(idExperiencia);

            // Cupo RETENIDO por lista de espera: si hay una oferta vigente para OTRO cliente, su cupo
            // liberado está reservado para él mientras la oferta no venza. Solo el cliente ofrecido
            // (vía ConfirmarOferta → este mismo método) puede tomarlo.
            var ofertaEspera = dalEspera.ObtenerOfertaVigente(idExperiencia);
            if (ofertaEspera != null
                && !ofertaEspera.OfertaVencida(ListaEspera.HORAS_VIGENCIA_OFERTA, DateTime.Now)
                && ofertaEspera.IdCliente != idCliente)
                throw new BE.AppException("err.bll.reserva.cupo_reservado_espera",
                    "El cupo liberado de '{0}' está reservado para la lista de espera.", experiencia.Nombre);

            var plan = dalPlan.ObtenerPorId(cliente.Suscripcion.IdPlan);

            // Regla 3 — no superar la cantidad de reservas del plan
            if (!cliente.PuedeReservar())
                throw new BE.AppException("err.bll.reserva.limite_plan",
                    "El plan '{0}' permite {1} reserva(s) por mes y ya no quedan disponibles este período.",
                    cliente.Suscripcion.NombrePlan, cliente.Suscripcion.ReservasDelPlan);

            // Experiencia premium: requiere plan con acceso premium
            if (experiencia.Premium && !cliente.Suscripcion.AccesoPremiumDelPlan)
                throw new BE.AppException("err.bll.reserva.premium",
                    "'{0}' es una experiencia premium; el plan del cliente no incluye acceso premium.",
                    experiencia.Nombre);

            // Edad mínima
            if (!experiencia.CumpleEdadMinima(cliente.Edad()))
                throw new BE.AppException("err.bll.reserva.edad_minima",
                    "La experiencia requiere una edad mínima de {0} años.", experiencia.EdadMinima);

            // Invitados dentro del beneficio del plan
            if (plan != null && !plan.AdmiteInvitados(cantidadInvitados))
                throw new BE.AppException("err.bll.reserva.invitados",
                    "El plan permite hasta {0} invitado(s) por reserva.", plan?.CantidadInvitados ?? 0);

            // Regla 4 — respetar la ciudad del cliente
            if (!PERMITIR_FUERA_DE_CIUDAD && cliente.IdCiudad.HasValue
                && experiencia.IdCiudad != cliente.IdCiudad.Value)
                throw new BE.AppException("err.bll.reserva.fuera_ciudad",
                    "La experiencia es en {0} y el cliente está en {1}.",
                    experiencia.NombreCiudad, cliente.NombreCiudad);

            // Regla 5 — no repetir una experiencia ya realizada
            if (dalReserva.ExisteAsistenciaPrevia(idCliente, idExperiencia))
            {
                if (BLOQUEAR_SI_YA_REALIZADA)
                    throw new BE.AppException("err.bll.reserva.ya_realizada",
                        "El cliente ya asistió a '{0}' anteriormente.", experiencia.Nombre);
                // (modo advertencia: se registraría un aviso; aquí se continúa)
            }

            // Regla 2 — no solapar horarios con otras reservas activas
            ValidarSinSolapamiento(idCliente, experiencia);

            int lugares = 1 + Math.Max(0, cantidadInvitados);

            var reserva = new BE.Reserva
            {
                IdCliente         = idCliente,
                IdExperiencia     = idExperiencia,
                IdEmpleado        = ResolverEmpleadoActivo(),
                Estado            = BE.EstadoReserva.Pendiente,
                FechaReserva      = DateTime.Now,
                CantidadInvitados = Math.Max(0, cantidadInvitados)
            };

            // Regla 1 — descuento de cupo + consumo del beneficio mensual + historial de CREAR,
            // TODO en una única transacción atómica (lanza sin_cupo si el cupo ya no alcanza).
            // Antes el consumo y el historial se escribían después de CrearConCupo: si alguno
            // fallaba, la reserva quedaba creada sin descontar el cupo o sin su registro de cambios.
            var registrosCrear = ConstruirRegistros(0, 1, "CREAR", new List<(string, string, string)>
            {
                ("Estado",       null, BE.EstadoReserva.Pendiente.ToString()),
                ("FechaReserva", null, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            });
            int idNuevo = dalReserva.CrearConCupo(reserva, lugares,
                cliente.Suscripcion.IdSuscripcion, registrosCrear);

            bitacora.Registrar(modulo,
                $"Crear Reserva #{idNuevo} — {cliente.NombreCompleto} — {experiencia.Nombre} — {lugares} lugar(es)",
                BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.Reserva,
                $"Reserva #{idNuevo} — {cliente.NombreCompleto} — {experiencia.Nombre} — {DateTime.Now:dd/MM/yyyy HH:mm}",
                idReserva: idNuevo, idExperiencia: idExperiencia, idCliente: idCliente);

            return idNuevo;
        }

        // ── Confirmar ──────────────────────────────────────────────────────────
        public void Confirmar(string modulo, BE.Reserva reserva)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasEditar, BE.Patentes.Reservas);
            if (!reserva.PuedeConfirmarse())
                throw new BE.AppException("err.bll.reserva.confirmar_estado",
                    "Solo se pueden confirmar reservas Pendientes. Esta reserva está '{0}'.", reserva.Estado);

            dalReserva.Confirmar(reserva.IdReserva);

            RegistrarHistorial(reserva.IdReserva, "CONFIRMAR", new List<(string, string, string)>
            {
                ("Estado", reserva.Estado.ToString(), BE.EstadoReserva.Confirmada.ToString())
            });
            bitacora.Registrar(modulo, $"Confirmar Reserva #{reserva.IdReserva} — {reserva.NombreCliente}", BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.Confirmacion,
                $"Reserva #{reserva.IdReserva} confirmada — {reserva.NombreCliente} — {reserva.NombreExperiencia}",
                idReserva: reserva.IdReserva, idCliente: reserva.IdCliente);
        }

        // ── Registrar asistencia (Asistió / No Asistió) ────────────────────────
        public void RegistrarAsistencia(string modulo, BE.Reserva reserva, bool asistio)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasRealizadasEditar, BE.Patentes.ReservasRealizadas);
            if (!reserva.PuedeRegistrarAsistencia())
                throw new BE.AppException("err.bll.reserva.asistencia_estado",
                    "Solo se registra la asistencia de reservas Confirmadas. Esta reserva está '{0}'.", reserva.Estado);

            var nuevo = asistio ? BE.EstadoReserva.Asistio : BE.EstadoReserva.NoAsistio;
            dalReserva.RegistrarAsistencia(reserva.IdReserva, nuevo);

            RegistrarHistorial(reserva.IdReserva, "ASISTENCIA", new List<(string, string, string)>
            {
                ("Estado", reserva.Estado.ToString(), nuevo.ToString())
            });
            bitacora.Registrar(modulo,
                $"Asistencia Reserva #{reserva.IdReserva} — {reserva.NombreCliente}: {nuevo}", BE.Criticidad.Baja);
            bitacoraNeg.Registrar(asistio ? BE.TipoEventoNegocio.Asistencia : BE.TipoEventoNegocio.Inasistencia,
                $"Reserva #{reserva.IdReserva} — {reserva.NombreCliente} — {nuevo}",
                idReserva: reserva.IdReserva, idCliente: reserva.IdCliente);
        }

        // ── Cancelar (reglas 6-7 + 9) ──────────────────────────────────────────
        public void Cancelar(string modulo, BE.Reserva reserva, string motivo)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasEditar, BE.Patentes.Reservas);
            if (!reserva.PuedeCancelarse())
                throw new BE.AppException("err.bll.reserva.cancelar_estado",
                    "Solo se pueden cancelar reservas Pendientes o Confirmadas. Esta reserva está '{0}'.", reserva.Estado);
            if (string.IsNullOrWhiteSpace(motivo))
                throw new BE.AppException("err.bll.reserva.cancelar_sin_motivo", "Es obligatorio ingresar un motivo de cancelación.");

            var experiencia = dalExp.ObtenerPorId(reserva.IdExperiencia);
            double horas = experiencia != null ? (experiencia.FechaHoraInicio - DateTime.Now).TotalHours : 0;
            bool aTiempo = horas >= HORAS_ANTICIPACION_CANCELACION;
            int  lugares = reserva.LugaresOcupados;

            // Regla 6/7: a tiempo → libera cupo y devuelve el beneficio; tardía → consume el beneficio
            dalReserva.Cancelar(reserva.IdReserva, reserva.IdExperiencia, lugares, motivo.Trim(), liberarCupo: aTiempo);

            var cliente = dalCliente.ObtenerPorId(reserva.IdCliente);
            if (aTiempo && cliente?.Suscripcion != null)
                dalSus.DecrementarConsumo(cliente.Suscripcion.IdSuscripcion);

            RegistrarHistorial(reserva.IdReserva, "CANCELAR", new List<(string, string, string)>
            {
                ("Estado",            reserva.Estado.ToString(), BE.EstadoReserva.Cancelada.ToString()),
                ("MotivoCancelacion", reserva.MotivoCancelacion, motivo.Trim())
            });

            string detalle = aTiempo ? "a tiempo (libera cupo, devuelve beneficio)" : "tardía (consume beneficio)";
            bitacora.Registrar(modulo,
                $"Cancelar Reserva #{reserva.IdReserva} — {reserva.NombreCliente} — {detalle} — Motivo: {motivo}",
                BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.Cancelacion,
                $"Reserva #{reserva.IdReserva} cancelada ({detalle}) — {reserva.NombreCliente} — Motivo: {motivo}",
                idReserva: reserva.IdReserva, idCliente: reserva.IdCliente);

            // Regla 9: al liberarse un cupo, ofrecer al primero de la lista de espera
            if (aTiempo)
                OfrecerCupoAlPrimeroEnEspera(modulo, reserva.IdExperiencia);
        }

        // ── Historial y restauración (Memento a nivel de reserva) ──────────────
        public System.Data.DataTable ObtenerHistorial(int idReserva, string accion = null,
                                                      DateTime? desde = null, DateTime? hasta = null)
            => dalHistorial.ObtenerPorReserva(idReserva, accion, desde, hasta);

        public void RestaurarOperacion(string modulo, int idReserva, int idOperacion)
        {
            var cambios = dalHistorial.ObtenerPorOperacion(idReserva, idOperacion);
            if (cambios == null || cambios.Count == 0)
                throw new BE.AppException("err.bll.reserva.historial_vacio",
                    "No se encontraron cambios para la operación #{0} de la Reserva #{1}.", idOperacion, idReserva);

            string accionOriginal = cambios[0].Accion;
            dalReserva.RestaurarOperacionAtomica(idReserva,
                cambios.Select(c => (c.Campo, c.ValorAnterior)).ToList());

            RegistrarHistorial(idReserva, "RESTAURAR",
                cambios.Select(c => (c.Campo, c.ValorNuevo, c.ValorAnterior)).ToList());

            bitacora.Registrar(modulo,
                $"Restaurar Reserva #{idReserva} — Revertida operación '{accionOriginal}' (op. #{idOperacion})",
                BE.Criticidad.Alta);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.Reactivacion,
                $"Reserva #{idReserva} restaurada — operación '{accionOriginal}' #{idOperacion} revertida",
                idReserva: idReserva);
        }

        /// <summary>
        /// Urgencia de una reserva pendiente/confirmada según cuán próxima está la experiencia.
        /// </summary>
        public BE.NivelUrgencia CalcularNivelUrgencia(BE.Reserva r)
        {
            if (r.Estado != BE.EstadoReserva.Pendiente && r.Estado != BE.EstadoReserva.Confirmada)
                return BE.NivelUrgencia.NoAplica;
            if (!r.FechaHoraExperiencia.HasValue) return BE.NivelUrgencia.NoAplica;

            double diasFalta = (r.FechaHoraExperiencia.Value - DateTime.Now).TotalDays;
            if (diasFalta < 1) return BE.NivelUrgencia.Urgente;
            if (diasFalta < 3) return BE.NivelUrgencia.Normal;
            return BE.NivelUrgencia.Reciente;
        }

        // ── Helpers de validación ──────────────────────────────────────────────
        private BE.Cliente ObtenerClienteValidado(int idCliente)
        {
            var cliente = dalCliente.ObtenerPorId(idCliente);
            if (cliente == null)
                throw new BE.AppException("err.bll.reserva.cliente_inexistente", "El cliente seleccionado no existe.");
            if (!cliente.TieneSuscripcion())
                throw new BE.AppException("err.bll.reserva.sin_suscripcion",
                    "El cliente {0} no tiene una suscripción. Asignale un plan antes de reservar.", cliente.NombreCompleto);
            if (!cliente.SuscripcionVigente())
                throw new BE.AppException("err.bll.reserva.suscripcion_vencida",
                    "La suscripción de {0} no está vigente. Renovala antes de reservar.", cliente.NombreCompleto);
            return cliente;
        }

        private BE.Experiencia ObtenerExperienciaValidada(int idExperiencia)
        {
            var exp = dalExp.ObtenerPorId(idExperiencia);
            if (exp == null)
                throw new BE.AppException("err.bll.reserva.experiencia_inexistente", "La experiencia seleccionada no existe.");
            if (exp.Estado == BE.EstadoExperiencia.Cancelada)
                throw new BE.AppException("err.bll.reserva.experiencia_cancelada", "La experiencia fue cancelada por el organizador.");
            if (!exp.EsFutura())
                throw new BE.AppException("err.bll.reserva.experiencia_pasada", "La experiencia ya ocurrió; no admite nuevas reservas.");
            return exp;
        }

        // Regla 2 — solapamiento de horarios con reservas activas del cliente.
        private void ValidarSinSolapamiento(int idCliente, BE.Experiencia nueva)
        {
            foreach (var r in dalReserva.ObtenerActivasConHorario(idCliente))
            {
                if (!r.FechaHoraExperiencia.HasValue) continue;
                DateTime existeIni = r.FechaHoraExperiencia.Value;
                DateTime existeFin = existeIni.AddMinutes(r.DuracionExperienciaMinutos);
                if (nueva.FechaHoraInicio < existeFin && existeIni < nueva.FechaHoraFin)
                    throw new BE.AppException("err.bll.reserva.solapamiento",
                        "El cliente ya tiene la reserva '{0}' que se superpone en horario con '{1}'.",
                        r.NombreExperiencia, nueva.Nombre);
            }
        }

        // Regla 9 — ofrecer el cupo liberado al primero de la cola.
        private void OfrecerCupoAlPrimeroEnEspera(string modulo, int idExperiencia)
        {
            var primero = dalEspera.ObtenerPrimeroEnEspera(idExperiencia);
            if (primero == null) return;

            // Se ofrece y se sella la fecha: desde acá corre el plazo de vigencia y el cupo queda
            // RETENIDO para este cliente (CrearReserva rechaza que lo tome otro mientras siga vigente).
            dalEspera.Ofrecer(primero.IdListaEspera, DateTime.Now);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.PromocionListaEspera,
                $"Cupo liberado en '{primero.NombreExperiencia}' ofrecido a {primero.NombreCliente} (1° en espera)",
                idExperiencia: idExperiencia, idCliente: primero.IdCliente);
            bitacora.Registrar(modulo,
                $"Lista de espera: cupo ofrecido a {primero.NombreCliente} en experiencia #{idExperiencia}",
                BE.Criticidad.Baja);
        }

        private int ResolverEmpleadoActivo()
        {
            if (!Seguridad.SessionManager.IsLoggedIn)
                throw new BE.AppException("err.bll.sesion_expirada", "La sesión expiró. Volvé a iniciar sesión.");
            var usuario = Seguridad.SessionManager.GetInstance().Usuario;
            var empleado = dalEmpleado.ObtenerPorUsuario(usuario.Id);
            if (empleado == null)
                throw new BE.AppException("err.bll.reserva.empleado_sin_vinculo",
                    "El usuario '{0}' no tiene un Empleado vinculado. Pedíle al Administrador que configure el vínculo.",
                    usuario.Username);
            return empleado.IdEmpleado;
        }

        private void RegistrarHistorial(int idReserva, string accion,
                                        List<(string Campo, string Anterior, string Nuevo)> campos)
        {
            int idOp = dalHistorial.ObtenerSiguienteIdOperacion(idReserva);
            dalHistorial.RegistrarCambios(ConstruirRegistros(idReserva, idOp, accion, campos));
        }

        /// <summary>
        /// Arma los registros de historial de una operación (sellando el usuario en sesión) SIN
        /// persistirlos. Separar la construcción de la escritura permite que la operación CREAR
        /// grabe su historial dentro de la misma transacción de DAL.Reserva.CrearConCupo.
        /// Para una reserva nueva se pasa idReserva=0 (lo estampa el DAL con el Id generado) e
        /// idOperacion=1 (es su primera operación).
        /// </summary>
        private List<BE.ReservaHistorial> ConstruirRegistros(int idReserva, int idOperacion, string accion,
                                        List<(string Campo, string Anterior, string Nuevo)> campos)
        {
            int?   idUsuario     = null;
            string nombreUsuario = null;
            if (Seguridad.SessionManager.IsLoggedIn)
            {
                var u = Seguridad.SessionManager.GetInstance().Usuario;
                idUsuario = u.Id; nombreUsuario = u.Username;
            }

            return campos.Select(c => new BE.ReservaHistorial
            {
                IdReserva     = idReserva,
                IdOperacion   = idOperacion,
                Fecha         = DateTime.Now,
                IdUsuario     = idUsuario,
                NombreUsuario = nombreUsuario,
                Accion        = accion,
                Campo         = c.Campo,
                ValorAnterior = c.Anterior,
                ValorNuevo    = c.Nuevo
            }).ToList();
        }
    }
}

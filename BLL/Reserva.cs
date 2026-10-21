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
        // NOTA: la antigua regla "reserva Pendiente bloqueante" se eliminó cuando crear+confirmar pasó a
        // ser una única operación atómica (la reserva nace Confirmada; ya no quedan Pendientes nuevas).

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

        // ── CU03-AGR — Consultar Situación del Cliente (solo lectura) ──────────
        /// <summary>
        /// Devuelve el estado comercial del cliente (plan, reservas restantes, vigencia y reserva
        /// pendiente si corresponde). No modifica nada. Permite al Agente de Reservas decidir si
        /// avanzar a armar la reserva. Mapea los estados a <see cref="BE.EstadoSituacionCliente"/>.
        /// </summary>
        public BE.SituacionCliente ConsultarSituacionCliente(int idCliente)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasEditar, BE.Patentes.Reservas);

            // Solo situación comercial (plan, reservas restantes, vigencia). La antigua "reserva Pendiente
            // bloqueante" se eliminó: crear+confirmar es atómico, no hay Pendientes nuevas que bloqueen.
            return BE.SituacionCliente.Resolver(dalCliente.ObtenerPorId(idCliente));
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
        /// PN02 (preview comercial) — evalúa de SOLO LECTURA la compatibilidad de un lote de experiencias
        /// para un cliente, reutilizando las MISMAS reglas que <see cref="CrearReserva"/> a través de
        /// <see cref="BE.CompatibilidadReserva.Evaluar"/>. Carga el contexto POR LOTES (cliente, plan,
        /// reservas activas, asistidas y cola) para no consultar por cada experiencia. No persiste nada:
        /// la validación definitiva y el cupo atómico siguen en CrearReserva/CrearConCupo.
        /// </summary>
        public Dictionary<int, BE.CompatibilidadReserva> EvaluarCompatibilidades(
            int idCliente, List<BE.Experiencia> experiencias, int invitados)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasEditar, BE.Patentes.Reservas);
            var res = new Dictionary<int, BE.CompatibilidadReserva>();
            if (experiencias == null || experiencias.Count == 0) return res;

            var cliente = dalCliente.ObtenerPorId(idCliente);
            if (cliente?.Suscripcion == null) return res;   // el gate del paso ① ya exige suscripción vigente
            var sus  = cliente.Suscripcion;
            var plan = dalPlan.ObtenerPorId(sus.IdPlan);
            int edad = cliente.Edad();

            var activas   = dalReserva.ObtenerActivasConHorario(idCliente);
            var asistidas = dalReserva.ObtenerIdExperienciasAsistidas(idCliente);
            var enCola    = dalEspera.ObtenerIdExperienciasEnCola(idCliente);
            int margenHoras = ListaEspera.MargenInicioHoras;
            DateTime ahora  = DateTime.Now;

            foreach (var exp in experiencias)
            {
                bool hayMargen = (exp.FechaHoraInicio - ahora).TotalHours >= margenHoras;
                res[exp.IdExperiencia] = BE.CompatibilidadReserva.Evaluar(
                    exp, sus, plan, cliente.IdCiudad, cliente.NombreCiudad, edad, invitados,
                    yaAsistio:             asistidas.Contains(exp.IdExperiencia),
                    nombreSolapada:        NombreSolapada(activas, exp),
                    yaEnCola:              enCola.Contains(exp.IdExperiencia),
                    hayMargenOferta:       hayMargen,
                    permitirFueraDeCiudad: PERMITIR_FUERA_DE_CIUDAD,
                    bloquearSiYaRealizada: BLOQUEAR_SI_YA_REALIZADA);
            }
            return res;
        }

        // ── Crear Reserva (reglas 1-5 + premium + edad + invitados) ────────────
        // idListaEsperaAConfirmar > 0 → esta reserva nace de confirmar una oferta de lista de espera;
        // la entrada se marca Confirmado en la MISMA transacción que la reserva (atomicidad, punto 1).
        public int CrearReserva(string modulo, int idCliente, int idExperiencia, int cantidadInvitados,
                                int idListaEsperaAConfirmar = 0)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasEditar, BE.Patentes.Reservas);

            var cliente = ObtenerClienteValidado(idCliente);

            var experiencia = ObtenerExperienciaValidada(idExperiencia);

            // Cupo RETENIDO por lista de espera: si hay una oferta vigente para OTRO cliente, su cupo
            // liberado está reservado para él mientras la oferta no venza. Solo el cliente ofrecido
            // (vía ConfirmarOferta → este mismo método) puede tomarlo.
            var ofertaEspera = dalEspera.ObtenerOfertaVigente(idExperiencia);
            if (ofertaEspera != null
                && !ofertaEspera.OfertaVencida(ListaEspera.HorasVigenciaOferta, ListaEspera.MargenInicioHoras, DateTime.Now)
                && ofertaEspera.IdCliente != idCliente)
                throw new BE.AppException("err.bll.reserva.cupo_reservado_espera",
                    "El cupo liberado de '{0}' está reservado para la lista de espera.", experiencia.Nombre);

            var plan = dalPlan.ObtenerPorId(cliente.Suscripcion.IdPlan);

            // Regla 3 — no superar la cantidad de reservas del plan (nivel cliente).
            if (!cliente.PuedeReservar())
                throw new BE.AppException("err.bll.reserva.limite_plan",
                    "El plan '{0}' permite {1} reserva(s) por mes y ya no quedan disponibles este período.",
                    cliente.Suscripcion.NombrePlan, cliente.Suscripcion.ReservasDelPlan);

            // Reglas cliente×experiencia (premium, edad, invitados, ciudad, ya realizada, solapamiento):
            // se evalúan con el MISMO evaluador de solo lectura que usa el preview del formulario, para NO
            // duplicar reglas. La clave/mensaje/args del motivo coinciden con las excepciones anteriores.
            // El cupo se sigue resolviendo de forma ATÓMICA en CrearConCupo (concurrencia intacta).
            string nombreSolapada = NombreSolapada(dalReserva.ObtenerActivasConHorario(idCliente), experiencia);
            bool yaAsistio = dalReserva.ExisteAsistenciaPrevia(idCliente, idExperiencia);
            var comp = BE.CompatibilidadReserva.Evaluar(
                experiencia, cliente.Suscripcion, plan, cliente.IdCiudad, cliente.NombreCiudad, cliente.Edad(),
                cantidadInvitados, yaAsistio, nombreSolapada, yaEnCola: false, hayMargenOferta: true,
                PERMITIR_FUERA_DE_CIUDAD, BLOQUEAR_SI_YA_REALIZADA);
            if (comp.EsIncompatible)
                throw new BE.AppException(comp.MotivoClave, comp.MotivoFallback, comp.MotivoArgs);

            int lugares = 1 + Math.Max(0, cantidadInvitados);

            // Crear+confirmar es UNA sola operación atómica: la reserva nace directamente CONFIRMADA
            // dentro de la transacción de CrearConCupo (cupo + consumo del plan + historial + cierre de
            // la entrada de lista de espera). Idéntico para "Nueva reserva" y para ConfirmarOferta.
            var reserva = new BE.Reserva
            {
                IdCliente         = idCliente,
                IdExperiencia     = idExperiencia,
                IdEmpleado        = ResolverEmpleadoActivo(),
                Estado            = BE.EstadoReserva.Confirmada,
                FechaReserva      = DateTime.Now,
                CantidadInvitados = Math.Max(0, cantidadInvitados)
            };

            // Historial único "CREAR_CONFIRMAR": registra la creación y la confirmación en un solo evento
            // (op. #1), dentro de la MISMA transacción atómica. Mismo criterio en los dos caminos.
            var registrosCrear = ConstruirRegistros(0, 1, "CREAR_CONFIRMAR", new List<(string, string, string)>
            {
                ("Estado",       null, BE.EstadoReserva.Confirmada.ToString()),
                ("FechaReserva", null, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            });
            int idNuevo = dalReserva.CrearConCupo(reserva, lugares,
                cliente.Suscripcion.IdSuscripcion, registrosCrear, idListaEsperaAConfirmar);

            bitacora.Registrar(modulo,
                $"Crear y confirmar Reserva #{idNuevo} — {cliente.NombreCompleto} — {experiencia.Nombre} — {lugares} lugar(es)",
                BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.Reserva,
                $"Reserva #{idNuevo} creada y confirmada — {cliente.NombreCompleto} — {experiencia.Nombre} — {DateTime.Now:dd/MM/yyyy HH:mm}",
                idReserva: idNuevo, idExperiencia: idExperiencia, idCliente: idCliente);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.Confirmacion,
                $"Reserva #{idNuevo} confirmada — {cliente.NombreCompleto} — {experiencia.Nombre}",
                idReserva: idNuevo, idExperiencia: idExperiencia, idCliente: idCliente);

            return idNuevo;
        }

        // ── Confirmar ──────────────────────────────────────────────────────────
        // COMPATIBILIDAD: crear+confirmar ahora es atómico (la reserva nace Confirmada), así que este
        // método ya no se usa en el alta. Se conserva para resolver reservas PENDIENTE heredadas de
        // bases anteriores (botón "Confirmar" de la grilla, habilitado solo si la reserva está Pendiente).
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
            // Regla 9 — patente propia de "Cancelar reserva". Fallback a ReservasEditar: en una base
            // sin migrar (sin la patente nueva) exige lo mismo que antes, así nadie pierde acceso.
            PermisosAccion.Exigir(BE.Patentes.ReservasCancelar, BE.Patentes.ReservasEditar);
            if (!reserva.PuedeCancelarse())
                throw new BE.AppException("err.bll.reserva.cancelar_estado",
                    "Solo se pueden cancelar reservas Pendientes o Confirmadas. Esta reserva está '{0}'.", reserva.Estado);
            if (string.IsNullOrWhiteSpace(motivo))
                throw new BE.AppException("err.bll.reserva.cancelar_sin_motivo", "Es obligatorio ingresar un motivo de cancelación.");

            var experiencia = dalExp.ObtenerPorId(reserva.IdExperiencia);

            // Regla 1 — solo se cancela una reserva de una experiencia que NO esté EnCurso ni
            // Finalizada (una vez que la experiencia arrancó, la reserva ya no es cancelable).
            if (experiencia != null &&
                (experiencia.Estado == BE.EstadoExperiencia.EnCurso ||
                 experiencia.Estado == BE.EstadoExperiencia.Finalizada))
                throw new BE.AppException("err.bll.reserva.cancelar_experiencia_iniciada",
                    "No se puede cancelar la reserva: la experiencia '{0}' ya está {1}.",
                    experiencia.Nombre, experiencia.Estado);

            double horas = experiencia != null ? (experiencia.FechaHoraInicio - DateTime.Now).TotalHours : 0;
            bool aTiempo = horas >= HORAS_ANTICIPACION_CANCELACION;
            int  lugares = reserva.LugaresOcupados;

            // Regla 2 — el cupo se libera SIEMPRE (a tiempo o tardía) para poder reasignarlo por lista
            // de espera; el beneficio del plan SOLO se devuelve en la cancelación a tiempo (la tardía
            // es penalizada: pierde la reserva del plan). La devolución va a la suscripción CON LA QUE
            // se hizo la reserva y solo si sigue vigente (ver ResolverSuscripcionADevolver).
            int idSuscripcionADevolver = aTiempo ? ResolverSuscripcionADevolver(reserva, dalSus) : 0;

            // Regla 2 — advertencia: cancelación a tiempo de una reserva SIN suscripción registrada
            // (previa a PN03): no se puede devolver el beneficio a ciegas, queda constancia.
            if (aTiempo && !reserva.IdSuscripcion.HasValue)
                bitacoraNeg.Registrar(BE.TipoEventoNegocio.Cancelacion,
                    $"ADVERTENCIA: no se devolvió el beneficio del plan de la Reserva #{reserva.IdReserva} — no tiene suscripción registrada (reserva previa a PN03).",
                    idReserva: reserva.IdReserva, idCliente: reserva.IdCliente);

            // Regla 7 — cancelación + liberación de cupo + devolución del plan + historial, TODO en una
            // única transacción atómica (si algo falla, rollback total: nada de devoluciones a medias).
            int idOp = dalHistorial.ObtenerSiguienteIdOperacion(reserva.IdReserva);
            var historial = ConstruirRegistros(reserva.IdReserva, idOp, "CANCELAR", new List<(string, string, string)>
            {
                ("Estado",            reserva.Estado.ToString(), BE.EstadoReserva.Cancelada.ToString()),
                ("MotivoCancelacion", reserva.MotivoCancelacion, motivo.Trim())
            });
            dalReserva.Cancelar(reserva.IdReserva, reserva.IdExperiencia, lugares, motivo.Trim(),
                liberarCupo: true, idSuscripcionADevolver: idSuscripcionADevolver, historial: historial);

            string detalle = aTiempo ? "a tiempo (libera cupo, devuelve beneficio)" : "tardía (libera cupo, NO devuelve beneficio)";
            bitacora.Registrar(modulo,
                $"Cancelar Reserva #{reserva.IdReserva} — {reserva.NombreCliente} — {detalle} — Motivo: {motivo}",
                BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.Cancelacion,
                $"Reserva #{reserva.IdReserva} cancelada ({detalle}) — {reserva.NombreCliente} — Motivo: {motivo}",
                idReserva: reserva.IdReserva, idCliente: reserva.IdCliente);

            // Regla 3 — al liberarse un cupo, ofrecer al primero de la lista de espera (a tiempo o tardía).
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

        /// <summary>
        /// PN03 (regla 2) — Resuelve a QUÉ suscripción se le devuelve el beneficio del plan al
        /// cancelar: la suscripción CON LA QUE se hizo la reserva (<see cref="BE.Reserva.IdSuscripcion"/>)
        /// y SOLO si sigue vigente. Si la reserva NO tiene suscripción registrada (previa a PN03) NO
        /// se devuelve a ninguna —NO se usa la vigente del cliente, que sería el mismo error que se
        /// corrigió— y se deja que el llamador registre la advertencia. Devuelve 0 cuando no hay a
        /// qué devolver (sub ausente o no vigente); nunca aborta. Compartida por CU06 y CU01.
        /// </summary>
        public static int ResolverSuscripcionADevolver(BE.Reserva reserva, DAL.Suscripcion dalSuscripcion)
        {
            if (!reserva.IdSuscripcion.HasValue) return 0;   // sin origen conocido → no se devuelve
            var sus = dalSuscripcion.ObtenerPorId(reserva.IdSuscripcion.Value);
            return (sus != null && sus.EstaVigente()) ? sus.IdSuscripcion : 0;
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

        // Regla 2 — nombre de la primera reserva activa del cliente que se solapa en horario con la
        // experiencia (o null si no hay solapamiento). Insumo del evaluador de compatibilidad; lo usan
        // tanto el preview como CrearReserva (una sola fuente de verdad del solapamiento).
        private static string NombreSolapada(List<BE.Reserva> activas, BE.Experiencia nueva)
        {
            if (activas == null) return null;
            foreach (var r in activas)
            {
                if (!r.FechaHoraExperiencia.HasValue) continue;
                DateTime ini = r.FechaHoraExperiencia.Value;
                DateTime fin = ini.AddMinutes(r.DuracionExperienciaMinutos);
                if (nueva.FechaHoraInicio < fin && ini < nueva.FechaHoraFin) return r.NombreExperiencia;
            }
            return null;
        }

        // Regla 9 — ofrecer el cupo liberado al primero de la cola.
        private void OfrecerCupoAlPrimeroEnEspera(string modulo, int idExperiencia)
        {
            var primero = dalEspera.ObtenerPrimeroEnEspera(idExperiencia);
            if (primero == null) return;

            // Regla 5 — si falta menos del margen para el inicio, no se ofrece: el cupo queda libre
            // para reserva directa (no hay tiempo para que el primero de la fila responda).
            if (!primero.HayMargenParaOferta(ListaEspera.MargenInicioHoras, DateTime.Now))
            {
                bitacora.Registrar(modulo,
                    $"Lista de espera: cupo liberado en experiencia #{idExperiencia} sin ofrecer (a menos de {ListaEspera.MargenInicioHoras} h del inicio); queda para reserva directa.",
                    BE.Criticidad.Baja);
                return;
            }

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

        // Atribución (opcional) del agente que registra la reserva. IdEmpleado es un FK NULLABLE
        // (el DAL lo graba como NULL cuando es 0) y NO entra al DV. Si el usuario en sesión no tiene
        // un Empleado vinculado —caso típico del Administrador, que no es agente y puede operar por
        // bypass, igual que en PN01, que ni siquiera atribuye empleado— la reserva se crea SIN agente
        // en vez de bloquear. Cuando el usuario SÍ es un agente con Empleado, se atribuye a él.
        private int ResolverEmpleadoActivo()
        {
            if (!Seguridad.SessionManager.IsLoggedIn)
                throw new BE.AppException("err.bll.sesion_expirada", "La sesión expiró. Volvé a iniciar sesión.");
            var usuario = Seguridad.SessionManager.GetInstance().Usuario;
            var empleado = dalEmpleado.ObtenerPorUsuario(usuario.Id);
            if (empleado == null)
            {
                System.Diagnostics.Trace.TraceInformation(
                    $"[BLL.Reserva] Usuario '{usuario.Username}' sin Empleado vinculado: la reserva se registra sin agente (IdEmpleado NULL).");
                return 0;   // → IdEmpleado NULL en DAL.Reserva.CrearConCupo
            }
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

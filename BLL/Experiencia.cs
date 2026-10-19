using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>Impacto previsto de cancelar una experiencia (para la confirmación en la GUI).</summary>
    public class ImpactoCancelacion
    {
        public int ReservasActivas { get; set; }
        public int EnListaEspera   { get; set; }
    }

    /// <summary>Lógica de negocio para la gestión de experiencias.</summary>
    public class Experiencia : Interfaces.IExperienciaService
    {
        private readonly DAL.Experiencia            dalExp       = new DAL.Experiencia();
        private readonly DAL.Reserva                dalReserva   = new DAL.Reserva();
        private readonly DAL.Suscripcion            dalSus       = new DAL.Suscripcion();
        private readonly DAL.ReservaHistorial       dalHistorial = new DAL.ReservaHistorial();
        private readonly DAL.ListaEspera            dalEspera    = new DAL.ListaEspera();
        private readonly Servicios.Bitacora         bitacora    = new Servicios.Bitacora();
        private readonly Servicios.BitacoraNegocio  bitacoraNeg = new Servicios.BitacoraNegocio();

        /// <summary>
        /// Mantenimiento de estados por fecha (Finalizada / EnCurso). Sin guarda de permiso: es una
        /// tarea de sistema (como el recálculo de DV), pensada para correr al arranque o por timer.
        /// </summary>
        public int ActualizarEstadosPorFecha() => dalExp.ActualizarEstadosPorFecha();

        public List<BE.Experiencia> ObtenerTodos()              => dalExp.ObtenerTodos();
        public List<BE.Experiencia> ObtenerDisponibles()        => dalExp.ObtenerDisponibles();
        /// <summary>Programadas (con cupo) + Completas (candidatas a lista de espera), para el módulo de Reservas.</summary>
        public List<BE.Experiencia> ObtenerParaReserva()        => dalExp.ObtenerParaReserva();
        public List<BE.Experiencia> ObtenerPorCiudad(int id)    => dalExp.ObtenerPorCiudad(id);
        public BE.Experiencia       ObtenerPorId(int id)        => dalExp.ObtenerPorId(id);

        // Alta de una experiencia. El cupo disponible arranca igual al cupo máximo.
        public void Alta(string modulo, BE.Experiencia experiencia)
        {
            PermisosAccion.Exigir(BE.Patentes.ExperienciasEditar, BE.Patentes.Experiencias);
            Validar(experiencia);
            experiencia.Estado         = BE.EstadoExperiencia.Programada;
            experiencia.CupoDisponible = experiencia.CupoMaximo;

            int idNuevo = dalExp.Alta(experiencia);
            experiencia.IdExperiencia = idNuevo;

            bitacora.Registrar(modulo,
                $"Alta Experiencia: {experiencia.Nombre} ({experiencia.Fecha:dd/MM/yyyy} {experiencia.HoraInicio:hh\\:mm})",
                BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.AltaExperiencia,
                $"Nueva experiencia: {experiencia.Nombre} — {experiencia.NombreCategoria} — {experiencia.NombreCiudad} — cupo {experiencia.CupoMaximo}",
                idExperiencia: idNuevo);
        }

        // Modifica datos descriptivos (no toca el cupo disponible, lo gestionan las reservas).
        public void Modificar(string modulo, BE.Experiencia experiencia)
        {
            PermisosAccion.Exigir(BE.Patentes.ExperienciasEditar, BE.Patentes.Experiencias);
            Validar(experiencia);
            dalExp.Modificar(experiencia);

            bitacora.Registrar(modulo, $"Modificar Experiencia ID {experiencia.IdExperiencia}: {experiencia.Nombre}", BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.ModificacionExperiencia,
                $"Modificación experiencia: '{experiencia.Nombre}' (ID {experiencia.IdExperiencia})",
                idExperiencia: experiencia.IdExperiencia);
        }

        public void CambiarEstado(string modulo, BE.Experiencia experiencia, BE.EstadoExperiencia nuevoEstado)
        {
            PermisosAccion.Exigir(BE.Patentes.ExperienciasEditar, BE.Patentes.Experiencias);
            dalExp.CambiarEstado(experiencia.IdExperiencia, nuevoEstado);

            bitacora.Registrar(modulo,
                $"Estado Experiencia ID {experiencia.IdExperiencia} '{experiencia.Nombre}': {experiencia.Estado} → {nuevoEstado}",
                BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.CambioEstadoExperiencia,
                $"Experiencia '{experiencia.Nombre}' (ID {experiencia.IdExperiencia}): {experiencia.Estado} → {nuevoEstado}",
                idExperiencia: experiencia.IdExperiencia);
        }

        // ── CU01-COO — Cancelación de experiencia por el Coordinador ─────────────

        /// <summary>
        /// Preview del impacto de cancelar una experiencia: cuántas reservas activas y cuántas
        /// entradas de lista de espera se verían afectadas. Para la confirmación en la GUI.
        /// </summary>
        public ImpactoCancelacion ObtenerImpactoCancelacion(int idExperiencia)
            => new ImpactoCancelacion
            {
                ReservasActivas = dalReserva.ObtenerActivasDeExperiencia(idExperiencia).Count,
                EnListaEspera   = dalEspera.ContarActivas(idExperiencia)
            };

        /// <summary>
        /// CU01-COO — El Coordinador cancela una experiencia. En cascada (una transacción atómica):
        /// se cancelan todas sus reservas activas CON devolución del beneficio del plan (no es culpa
        /// del cliente), se cierra su lista de espera y la experiencia queda Cancelada. Reglas 6-8.
        /// </summary>
        public void CancelarExperiencia(string modulo, BE.Experiencia experiencia, string motivo)
        {
            // Regla 9 — patente propia de "Cancelar experiencia" (fallback a ExperienciasEditar para
            // no sacarle acceso a nadie en una base sin migrar).
            PermisosAccion.Exigir(BE.Patentes.ExperienciasCancelar, BE.Patentes.ExperienciasEditar);
            if (experiencia == null) throw new ArgumentNullException(nameof(experiencia));
            if (string.IsNullOrWhiteSpace(motivo))
                throw new BE.AppException("err.bll.experiencia.cancelar_sin_motivo",
                    "Es obligatorio ingresar un motivo de cancelación de la experiencia.");

            var exp = dalExp.ObtenerPorId(experiencia.IdExperiencia)
                ?? throw new BE.AppException("err.bll.experiencia.inexistente", "La experiencia no existe.");
            if (exp.Estado == BE.EstadoExperiencia.Cancelada)
                throw new BE.AppException("err.bll.experiencia.ya_cancelada", "La experiencia ya estaba cancelada.");
            if (exp.Estado == BE.EstadoExperiencia.Finalizada)
                throw new BE.AppException("err.bll.experiencia.cancelar_finalizada",
                    "No se puede cancelar una experiencia que ya finalizó.");

            // Reunir las reservas activas y resolver, por cliente, la suscripción a la que se le
            // devuelve el beneficio. Se arma el historial de CANCELAR de cada reserva.
            var activas = dalReserva.ObtenerActivasDeExperiencia(exp.IdExperiencia);
            var payload = new List<(int, int, List<BE.ReservaHistorial>)>();
            foreach (var r in activas)
            {
                // Devolución a la suscripción con la que se hizo la reserva, solo si sigue vigente
                // (regla 2; mismo criterio que la cancelación simple). 0 = no se devuelve.
                int idSus = Reserva.ResolverSuscripcionADevolver(r, dalSus);
                // Advertencia: reserva sin suscripción registrada (previa a PN03) → no se devuelve a
                // ciegas (no se usa la vigente del cliente); queda constancia en la bitácora de negocio.
                if (!r.IdSuscripcion.HasValue)
                    bitacoraNeg.Registrar(BE.TipoEventoNegocio.Cancelacion,
                        $"ADVERTENCIA: al cancelar '{exp.Nombre}' no se devolvió el beneficio de la Reserva #{r.IdReserva} — sin suscripción registrada (previa a PN03).",
                        idReserva: r.IdReserva, idCliente: r.IdCliente);
                int idOp  = dalHistorial.ObtenerSiguienteIdOperacion(r.IdReserva);
                payload.Add((r.IdReserva, idSus, ConstruirHistorialCancelacion(r.IdReserva, idOp, r.Estado, motivo.Trim())));
            }

            dalExp.CancelarEnCascada(exp.IdExperiencia, motivo.Trim(), payload);

            // Regla 8 — bitácora de seguridad + eventos de negocio (experiencia + cada reserva).
            bitacora.Registrar(modulo,
                $"Cancelar Experiencia ID {exp.IdExperiencia} '{exp.Nombre}' — {activas.Count} reserva(s) cancelada(s) con devolución — Motivo: {motivo}",
                BE.Criticidad.Alta);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.CancelacionExperiencia,
                $"Experiencia '{exp.Nombre}' (ID {exp.IdExperiencia}) cancelada por el organizador — {activas.Count} reserva(s) devuelta(s) — Motivo: {motivo}",
                idExperiencia: exp.IdExperiencia);
            foreach (var r in activas)
                bitacoraNeg.Registrar(BE.TipoEventoNegocio.Cancelacion,
                    $"Reserva #{r.IdReserva} cancelada por cancelación de la experiencia '{exp.Nombre}' (devuelve beneficio) — {r.NombreCliente}",
                    idReserva: r.IdReserva, idCliente: r.IdCliente);
        }

        // Arma el historial de una cancelación (sellando el usuario en sesión) para grabarlo dentro
        // de la transacción de la cascada.
        private List<BE.ReservaHistorial> ConstruirHistorialCancelacion(int idReserva, int idOperacion,
            BE.EstadoReserva estadoAnterior, string motivo)
        {
            int? idUsuario = null; string nombreUsuario = null;
            if (Seguridad.SessionManager.IsLoggedIn)
            {
                var u = Seguridad.SessionManager.GetInstance().Usuario;
                idUsuario = u.Id; nombreUsuario = u.Username;
            }
            DateTime ahora = DateTime.Now;
            return new List<BE.ReservaHistorial>
            {
                new BE.ReservaHistorial { IdReserva=idReserva, IdOperacion=idOperacion, Fecha=ahora,
                    IdUsuario=idUsuario, NombreUsuario=nombreUsuario, Accion="CANCELAR",
                    Campo="Estado", ValorAnterior=estadoAnterior.ToString(), ValorNuevo=BE.EstadoReserva.Cancelada.ToString() },
                new BE.ReservaHistorial { IdReserva=idReserva, IdOperacion=idOperacion, Fecha=ahora,
                    IdUsuario=idUsuario, NombreUsuario=nombreUsuario, Accion="CANCELAR",
                    Campo="MotivoCancelacion", ValorAnterior=null, ValorNuevo=motivo }
            };
        }

        private void Validar(BE.Experiencia e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            if (string.IsNullOrWhiteSpace(e.Nombre))
                throw new BE.AppException("err.bll.experiencia.nombre_requerido", "El nombre de la experiencia es obligatorio.");
            if (e.IdCategoria <= 0)
                throw new BE.AppException("err.bll.experiencia.categoria_requerida", "Debe seleccionar una categoría.");
            if (e.IdCiudad <= 0)
                throw new BE.AppException("err.bll.experiencia.ciudad_requerida", "Debe seleccionar una ciudad.");
            if (e.IdOrganizador <= 0)
                throw new BE.AppException("err.bll.experiencia.organizador_requerido", "Debe seleccionar un organizador.");
            if (e.CupoMaximo <= 0)
                throw new BE.AppException("err.bll.experiencia.cupo_invalido", "El cupo máximo debe ser mayor que cero.");
            if (e.DuracionMinutos <= 0)
                throw new BE.AppException("err.bll.experiencia.duracion_invalida", "La duración debe ser mayor que cero.");
            if (e.EdadMinima < 0)
                throw new BE.AppException("err.bll.experiencia.edad_invalida", "La edad mínima no puede ser negativa.");
        }
    }
}

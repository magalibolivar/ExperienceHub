using System;

namespace BE
{
    /// <summary>
    /// Entidad — Registro de un cambio de campo sobre una Reserva.
    /// Mapea la tabla [ReservaHistorial].
    ///
    /// Estrategia por campo: cada fila representa un único campo modificado.
    /// IdOperacion agrupa todos los campos cambiados en el mismo evento de negocio
    /// (por ejemplo, al Confirmar se generan filas con el mismo IdOperacion).
    ///
    /// Acciones posibles (campo Accion):
    ///   CREAR       — creación de la reserva
    ///   CONFIRMAR   — reserva pasó a Confirmada
    ///   ASISTENCIA  — se registró Asistio / NoAsistio
    ///   CANCELAR    — reserva cancelada
    ///   RESTAURAR   — estado restaurado desde historial
    ///
    /// Campos rastreados (campo Campo):
    ///   Estado | FechaCancelacion | MotivoCancelacion | CantidadInvitados
    /// </summary>
    public class ReservaHistorial
    {
        public int      IdHistorial   { get; set; }
        public int      IdReserva     { get; set; }

        /// <summary>
        /// Agrupa todos los campos cambiados en un mismo evento de negocio.
        /// Permite restaurar de forma atómica todos los campos de una operación.
        /// </summary>
        public int      IdOperacion   { get; set; }

        public DateTime Fecha         { get; set; }
        public int?     IdUsuario     { get; set; }
        public string   NombreUsuario { get; set; }

        /// <summary>Tipo de operación: CREAR, CONFIRMAR, ASISTENCIA, CANCELAR, RESTAURAR.</summary>
        public string   Accion        { get; set; }

        /// <summary>Nombre del campo modificado: Estado, FechaCancelacion, MotivoCancelacion, CantidadInvitados.</summary>
        public string   Campo         { get; set; }

        public string   ValorAnterior { get; set; }
        public string   ValorNuevo    { get; set; }
    }
}

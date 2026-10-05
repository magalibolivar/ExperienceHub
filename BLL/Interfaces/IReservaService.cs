using System.Collections.Generic;

namespace BLL.Interfaces
{
    /// <summary>
    /// Gestión del ciclo de vida de Reservas.
    ///   CrearReserva()        — Agente reserva una experiencia para un cliente (reglas 1-5)
    ///   Confirmar()           — la reserva pasa a Confirmada
    ///   RegistrarAsistencia() — Asistió / No Asistió
    ///   Cancelar()            — cancela; libera cupo si es anticipada (reglas 6-7, 9)
    ///   ObtenerHistorial()    — historial de cambios de la reserva
    ///   RestaurarOperacion()  — restaura la reserva al estado previo a una operación
    /// </summary>
    public interface IReservaService
    {
        List<BE.Reserva> ObtenerTodos();
        List<BE.Reserva> ObtenerPorCliente(int idCliente);
        BE.Reserva       ObtenerPorId(int id);

        // PN01 — pasos previos a armar el pedido (solo lectura)
        BE.SituacionCliente       ConsultarSituacionCliente(int idCliente);          // CU03-VEN
        BE.ResultadoDisponibilidad VerificarDisponibilidad(int idExperiencia, int lugares); // CU01-DEP

        int  CrearReserva(string modulo, int idCliente, int idExperiencia, int cantidadInvitados);
        void Confirmar(string modulo, BE.Reserva reserva);
        void RegistrarAsistencia(string modulo, BE.Reserva reserva, bool asistio);
        void Cancelar(string modulo, BE.Reserva reserva, string motivo);

        System.Data.DataTable ObtenerHistorial(int idReserva, string accion = null,
                                               System.DateTime? desde = null, System.DateTime? hasta = null);
        void RestaurarOperacion(string modulo, int idReserva, int idOperacion);

        BE.NivelUrgencia CalcularNivelUrgencia(BE.Reserva reserva);
    }
}

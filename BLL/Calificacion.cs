using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>Lógica de negocio para las calificaciones de experiencias (regla 10).</summary>
    public class Calificacion
    {
        private readonly DAL.Calificacion    dal      = new DAL.Calificacion();
        private readonly Servicios.Bitacora        bitacora    = new Servicios.Bitacora();
        private readonly Servicios.BitacoraNegocio bitacoraNeg = new Servicios.BitacoraNegocio();

        public List<BE.Calificacion> ObtenerPorExperiencia(int id) => dal.ObtenerPorExperiencia(id);
        public System.Collections.Generic.Dictionary<int, int> ObtenerPuntajesPorReserva() => dal.ObtenerPuntajesPorReserva();
        public double PromedioPorExperiencia(int id) => dal.PromedioPorExperiencia(id);

        /// <summary>
        /// Regla 10 — el cliente califica una experiencia a la que asistió.
        /// Solo válido si la reserva está en estado Asistió y no fue calificada aún.
        /// </summary>
        public void Calificar(string modulo, BE.Reserva reserva, int puntaje, string comentario)
        {
            PermisosAccion.Exigir(BE.Patentes.ReservasRealizadasEditar, BE.Patentes.ReservasRealizadas);

            if (reserva == null) throw new ArgumentNullException(nameof(reserva));
            if (!reserva.PuedeCalificarse())
                throw new BE.AppException("err.bll.calificacion.no_asistio",
                    "Solo se pueden calificar experiencias a las que el cliente asistió.");
            if (dal.ExistePorReserva(reserva.IdReserva))
                throw new BE.AppException("err.bll.calificacion.ya_calificada",
                    "Esta reserva ya fue calificada.");

            var c = new BE.Calificacion
            {
                IdReserva     = reserva.IdReserva,
                IdCliente     = reserva.IdCliente,
                IdExperiencia = reserva.IdExperiencia,
                Puntaje       = puntaje,
                Comentario    = comentario,
                Fecha         = DateTime.Now
            };
            if (!c.PuntajeValido())
                throw new BE.AppException("err.bll.calificacion.puntaje_invalido",
                    "El puntaje debe estar entre {0} y {1}.", BE.Calificacion.PuntajeMinimo, BE.Calificacion.PuntajeMaximo);

            dal.Alta(c);

            bitacora.Registrar(modulo, $"Calificación reserva #{reserva.IdReserva}: {puntaje}★", BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.Calificacion,
                $"Calificación {puntaje}★ — {reserva.NombreCliente} — {reserva.NombreExperiencia}",
                idReserva: reserva.IdReserva, idCliente: reserva.IdCliente);
        }
    }
}

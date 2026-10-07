using System.Collections.Generic;

namespace BLL
{
    /// <summary>
    /// Lógica de FIDELIZACIÓN (PN02 enriquecido): convierte la reserva en una relación continua.
    /// Recomienda experiencias según el historial del cliente (categorías ya vividas + puntajes),
    /// clasifica su fidelidad y detecta experiencias pendientes de calificar (seguimiento).
    /// No crea entidades nuevas: se apoya en Reserva, Experiencia, Categoría y Calificación.
    /// </summary>
    public class Recomendacion
    {
        private readonly DAL.Reserva     dalReserva = new DAL.Reserva();
        private readonly DAL.Experiencia dalExp     = new DAL.Experiencia();
        private readonly DAL.Cliente     dalCliente = new DAL.Cliente();
        private readonly Calificacion    bllCalif   = new Calificacion();

        /// <summary>Nivel de fidelidad del cliente según sus asistencias.</summary>
        public BE.NivelFidelidad NivelFidelidad(int idCliente)
            => BE.Fidelidad.Clasificar(dalReserva.ContarAsistencias(idCliente));

        /// <summary>Reservas Asistió del cliente aún sin calificar (seguimiento post-experiencia).</summary>
        public List<BE.Reserva> PendientesDeCalificar(int idCliente)
            => dalReserva.ObtenerAsistidasSinCalificar(idCliente);

        /// <summary>
        /// Top N experiencias recomendadas para el cliente: futuras y con cupo, en su ciudad, que
        /// cumplan su plan (premium/edad) y que no haya reservado/asistido ya. Se puntúan por afinidad
        /// de categoría con su historial + puntaje promedio, y se ordenan descendente.
        /// </summary>
        public List<BE.Recomendacion> RecomendarPara(int idCliente, int n = 5)
        {
            var cliente = dalCliente.ObtenerPorId(idCliente);
            if (cliente == null) return new List<BE.Recomendacion>();

            // Afinidad por categoría (historial) y experiencias que el cliente ya tomó (a excluir).
            var historial = dalReserva.ContarPorCategoriaDelHistorial(idCliente);
            var yaReservadas = new HashSet<int>();
            foreach (var r in dalReserva.ObtenerPorCliente(idCliente))
                if (r.Estado != BE.EstadoReserva.Cancelada)
                    yaReservadas.Add(r.IdExperiencia);

            bool accesoPremium = cliente.Suscripcion != null && cliente.Suscripcion.AccesoPremiumDelPlan;
            int edad = cliente.Edad();

            var candidatas = new List<BE.Recomendacion>();
            foreach (var exp in dalExp.ObtenerDisponibles())   // Programadas, futuras y con cupo
            {
                if (yaReservadas.Contains(exp.IdExperiencia)) continue;
                if (cliente.IdCiudad.HasValue && exp.IdCiudad != cliente.IdCiudad.Value) continue;
                if (exp.Premium && !accesoPremium) continue;
                if (!exp.CumpleEdadMinima(edad)) continue;

                int afinidad = historial.TryGetValue(exp.IdCategoria, out int c) ? c : 0;
                double puntaje = bllCalif.PromedioPorExperiencia(exp.IdExperiencia);
                candidatas.Add(new BE.Recomendacion
                {
                    Experiencia = exp,
                    Score       = BE.ScoringRecomendacion.Puntuar(afinidad, puntaje),
                    Motivo      = BE.ScoringRecomendacion.Motivo(afinidad, exp.NombreCategoria, puntaje)
                });
            }
            return BE.ScoringRecomendacion.TopN(candidatas, n);
        }
    }
}

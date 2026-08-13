using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL
{
    /// <summary>
    /// Regla 11 — motor de recomendaciones por REGLAS (explicable, sin IA). Puntúa las experiencias
    /// disponibles según el perfil del cliente (intereses declarados, categorías que ya frecuentó por
    /// su historial, ciudad y calificación de otros clientes) y descarta las que ya realizó. Cada
    /// recomendación expone un % de AFINIDAD y los MOTIVOS por los que se sugirió.
    ///
    /// Implementa <see cref="Interfaces.IMotorRecomendacion"/>: la lógica de reglas se puede
    /// reemplazar/complementar a futuro por un modelo inteligente sin tocar la UI.
    /// </summary>
    public class Recomendacion : Interfaces.IMotorRecomendacion
    {
        private readonly DAL.Cliente      dalCliente = new DAL.Cliente();
        private readonly DAL.Experiencia  dalExp     = new DAL.Experiencia();
        private readonly DAL.Reserva      dalReserva = new DAL.Reserva();
        private readonly DAL.Calificacion dalCalif   = new DAL.Calificacion();

        // Aporte de cada regla al % de afinidad (suman hasta 100). Explicable y ajustable en un solo lugar.
        private const int AF_INTERES   = 35;   // la categoría coincide con un interés declarado
        private const int AF_CATEG_FAV = 25;   // categoría que el cliente ya frecuentó (historial)
        private const int AF_CIUDAD    = 20;   // misma ciudad que el cliente
        private const int AF_CALIF_MAX = 20;   // × (promedio de calificación de otros / 5)

        /// <summary>
        /// Recomendaciones DETALLADAS (experiencia + % de afinidad + motivos), mejor puntuadas primero.
        /// Reutiliza las mismas entidades/servicios ya existentes; no genera datos ficticios.
        /// </summary>
        public List<BE.RecomendacionExperiencia> RecomendarDetallado(int idCliente, int cantidad = 12)
        {
            var cliente = dalCliente.ObtenerPorId(idCliente);
            if (cliente == null)
                throw new BE.AppException("err.bll.recomendacion.cliente_inexistente", "El cliente no existe.");

            // Perfil: intereses declarados + categorías que ya frecuentó (historial de reservas).
            var intereses = new HashSet<string>(
                (cliente.Intereses ?? new List<BE.Interes>()).Select(i => Norm(i.Nombre)));
            var categoriasFrecuentes = new HashSet<int>(
                dalReserva.ObtenerPorCliente(idCliente)
                          .Select(r => dalExp.ObtenerPorId(r.IdExperiencia)?.IdCategoria ?? 0)
                          .Where(id => id > 0));

            var resultado = new List<BE.RecomendacionExperiencia>();

            foreach (var e in dalExp.ObtenerDisponibles())
            {
                if (dalReserva.ExisteAsistenciaPrevia(idCliente, e.IdExperiencia)) continue; // ya realizada

                int afinidad = 0;
                var motivos = new List<string>();

                if (!string.IsNullOrEmpty(e.NombreCategoria) && intereses.Contains(Norm(e.NombreCategoria)))
                {
                    afinidad += AF_INTERES;
                    motivos.Add($"Coincide con su interés en {e.NombreCategoria}");
                }
                if (categoriasFrecuentes.Contains(e.IdCategoria))
                {
                    afinidad += AF_CATEG_FAV;
                    motivos.Add($"Suele elegir experiencias de {e.NombreCategoria}");
                }
                if (cliente.IdCiudad.HasValue && e.IdCiudad == cliente.IdCiudad.Value)
                {
                    afinidad += AF_CIUDAD;
                    motivos.Add($"Es en {e.NombreCiudad}, la ciudad del cliente");
                }
                double prom = dalCalif.PromedioPorExperiencia(e.IdExperiencia);
                if (prom > 0)
                {
                    int aporte = (int)Math.Round(AF_CALIF_MAX * (prom / 5.0));
                    if (aporte > 0)
                    {
                        afinidad += aporte;
                        motivos.Add($"Bien valorada por otros clientes ({prom:0.0} de 5)");
                    }
                }

                if (afinidad <= 0) continue;                  // sin afinidad → no se recomienda
                if (afinidad > 100) afinidad = 100;

                resultado.Add(new BE.RecomendacionExperiencia
                {
                    Experiencia = e,
                    Afinidad    = afinidad,
                    Motivos     = motivos
                });
            }

            return resultado
                .OrderByDescending(r => r.Afinidad)
                .ThenBy(r => r.Experiencia.FechaHoraInicio)
                .Take(cantidad)
                .ToList();
        }

        /// <summary>Compat: solo las experiencias recomendadas (proyección del método detallado).</summary>
        public List<BE.Experiencia> Recomendar(int idCliente, int cantidad = 10)
            => RecomendarDetallado(idCliente, cantidad).Select(r => r.Experiencia).ToList();

        private static string Norm(string s) => (s ?? "").Trim().ToLowerInvariant();
    }
}

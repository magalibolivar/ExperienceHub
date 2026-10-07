using System;
using System.Collections.Generic;

namespace BE
{
    /// <summary>
    /// Una experiencia recomendada a un cliente, con su puntaje de afinidad y el motivo.
    /// Convierte la reserva en una relación continua: se sugieren experiencias en línea con
    /// lo que el cliente ya vivió y mejor calificado.
    /// </summary>
    public class Recomendacion
    {
        public Experiencia Experiencia { get; set; }
        public double      Score       { get; set; }
        public string      Motivo      { get; set; }
    }

    /// <summary>
    /// Scoring PURO de recomendaciones (sin BD ni sesión, testeable). La afinidad combina:
    ///   • cuánto encaja la CATEGORÍA de la experiencia con el historial del cliente
    ///     (cantidad de asistencias previas en esa categoría), y
    ///   • el PUNTAJE promedio de la experiencia (0-5).
    /// </summary>
    public static class ScoringRecomendacion
    {
        /// <summary>Peso de cada asistencia previa en la categoría.</summary>
        public const double PESO_CATEGORIA = 2.0;
        /// <summary>Peso del puntaje promedio (0-5) de la experiencia.</summary>
        public const double PESO_PUNTAJE   = 1.0;

        /// <summary>
        /// Puntúa una experiencia candidata.
        /// </summary>
        /// <param name="asistenciasEnCategoria">Cuántas veces el cliente ya asistió a experiencias de esa categoría.</param>
        /// <param name="puntajePromedio">Puntaje promedio de la experiencia (0 si no tiene calificaciones).</param>
        public static double Puntuar(int asistenciasEnCategoria, double puntajePromedio)
            => asistenciasEnCategoria * PESO_CATEGORIA + puntajePromedio * PESO_PUNTAJE;

        /// <summary>Motivo legible según de dónde viene el score.</summary>
        public static string Motivo(int asistenciasEnCategoria, string nombreCategoria, double puntajePromedio)
        {
            if (asistenciasEnCategoria > 0)
                return $"Te suele gustar {nombreCategoria}";
            if (puntajePromedio >= 4.0)
                return "Muy bien calificada por otros clientes";
            return "Para descubrir algo nuevo";
        }

        /// <summary>
        /// Ordena candidatas por score descendente y devuelve el top N. Decisión pura: recibe los
        /// scores ya calculados (la BLL arma los insumos desde la BD).
        /// </summary>
        public static List<Recomendacion> TopN(IEnumerable<Recomendacion> candidatas, int n)
        {
            var lista = new List<Recomendacion>(candidatas);
            lista.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (n > 0 && lista.Count > n) lista = lista.GetRange(0, n);
            return lista;
        }
    }
}

using System;

namespace BE
{
    /// <summary>
    /// Reglas de negocio dependientes de la fecha actual, extraídas como funciones PURAS (reciben
    /// "hoy"/"ahora" como parámetro en vez de leer <see cref="DateTime.Now"/> internamente) — mismo
    /// patrón ya usado por <see cref="ListaEspera.OfertaVencida"/> y
    /// <see cref="Suscripcion.EstadoVigenciaCalculado"/>. Permite testear la regla con cualquier fecha
    /// sin depender del reloj del sistema operativo, y evita reglas duplicadas e inconsistentes entre
    /// capas (la mayoría de edad vivía repetida, por separado, en BLL.Cliente y BLL.Usuario).
    /// </summary>
    public static class ReglasFecha
    {
        /// <summary>¿La persona nacida en <paramref name="fechaNacimiento"/> ya cumplió
        /// <paramref name="edadMinima"/> años a la fecha <paramref name="hoy"/>?</summary>
        public static bool EsMayorDeEdad(DateTime fechaNacimiento, DateTime hoy, int edadMinima = 18)
            => fechaNacimiento.Date <= hoy.Date.AddYears(-edadMinima);
    }
}

using System;
using System.Collections.Generic;

namespace BE
{
    /// <summary>Entidad Cliente (suscriptor de experiencias). Mapea la tabla [Cliente].</summary>
    public class Cliente
    {
        public int       IdCliente       { get; set; }
        public DateTime  FechaAlta       { get; set; }
        public string    Nombre          { get; set; }
        public string    Apellido        { get; set; }
        public string    DNI             { get; set; }
        public string    Email           { get; set; }

        /// <summary>Fecha de nacimiento (para validar mayoría de edad y edad mínima de experiencias).</summary>
        public DateTime? FechaNacimiento { get; set; }

        /// <summary>FK → Ciudad de residencia/preferencia del cliente.</summary>
        public int?      IdCiudad        { get; set; }
        /// <summary>Nombre de la ciudad (cargado por JOIN, no persiste).</summary>
        public string    NombreCiudad    { get; set; }

        /// <summary>Intereses del cliente (cargados desde ClienteInteres). Alimentan las recomendaciones.</summary>
        public List<Interes> Intereses   { get; set; } = new List<Interes>();

        /// <summary>Suscripción vigente del cliente (cargada por JOIN; null si no tiene).</summary>
        public Suscripcion Suscripcion   { get; set; }

        // ── Derivados ─────────────────────────────────────────────────────────

        public string NombreCompleto => $"{Nombre} {Apellido}";

        /// <summary>Edad en años cumplidos; 0 si no hay fecha de nacimiento.</summary>
        public int Edad()
        {
            if (!FechaNacimiento.HasValue) return 0;
            var hoy = DateTime.Today;
            int edad = hoy.Year - FechaNacimiento.Value.Year;
            if (FechaNacimiento.Value.Date > hoy.AddYears(-edad)) edad--;
            return edad;
        }

        // ── Comportamiento de suscripción (delegado en Suscripcion) ────────────

        /// <summary>True si el cliente tiene una suscripción asociada.</summary>
        public bool TieneSuscripcion() => Suscripcion != null;

        /// <summary>True si el cliente tiene una suscripción activa y no vencida.</summary>
        public bool SuscripcionVigente() => Suscripcion != null && Suscripcion.EstaVigente();

        /// <summary>True si el cliente puede consumir al menos una reserva este mes.</summary>
        public bool PuedeReservar() => Suscripcion != null && Suscripcion.PuedeReservar();

        /// <summary>Reservas que le restan en el mes; 0 si no tiene suscripción.</summary>
        public int ReservasRestantes() => Suscripcion?.ReservasRestantes() ?? 0;
    }
}

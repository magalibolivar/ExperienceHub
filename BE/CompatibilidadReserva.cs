using System;

namespace BE
{
    /// <summary>
    /// Estado de compatibilidad de una experiencia para un cliente dado (PN02). Deriva de las MISMAS
    /// reglas que <see cref="object"/> aplica <c>BLL.Reserva.CrearReserva</c>; por eso el evaluador vive
    /// acá como decisión PURA (sin BD ni sesión) y lo reutilizan tanto el preview del formulario como la
    /// creación definitiva. La validación atómica de cupo sigue ocurriendo al confirmar.
    /// </summary>
    public enum EstadoCompatibilidad
    {
        Disponible,            // compatible y con cupo suficiente para los lugares pedidos
        SinLugar,              // compatible pero el cupo disponible es menor a los lugares pedidos
        ListaEspera,           // compatible salvo cupo (experiencia completa) y encolable
        ListaEsperaSinOferta,  // completa, pero falta menos del margen → una inscripción no recibiría oferta
        YaEnCola,              // completa y el cliente ya está en la lista de espera
        Incompatible           // incumple una regla dura (premium, edad, invitados, ciudad, ya realizada, solapamiento)
    }

    /// <summary>Resultado de evaluar la compatibilidad cliente×experiencia (solo lectura).</summary>
    public class CompatibilidadReserva
    {
        public int    IdExperiencia      { get; set; }
        public EstadoCompatibilidad Estado { get; set; }
        public int    LugaresDisponibles { get; set; }   // cupo disponible (para informar en SinLugar)
        public int    LugaresPedidos     { get; set; }    // 1 titular + invitados
        public string MotivoClave        { get; set; }    // clave i18n del motivo (solo si Incompatible)
        public string MotivoFallback     { get; set; }    // plantilla ES de respaldo
        public object[] MotivoArgs       { get; set; }

        public bool EsReservable     => Estado == EstadoCompatibilidad.Disponible;
        public bool PuedeListaEspera => Estado == EstadoCompatibilidad.ListaEspera;
        public bool EsIncompatible   => Estado == EstadoCompatibilidad.Incompatible;

        /// <summary>
        /// Decisión PURA. Evalúa primero las reglas DURAS en el MISMO orden que
        /// <c>BLL.Reserva.CrearReserva</c> (premium → edad → invitados → ciudad → ya realizada →
        /// solapamiento); si todas pasan, deriva el estado según el cupo y la lista de espera.
        /// Las claves/plantillas/argumentos de motivo coinciden EXACTAMENTE con las excepciones que
        /// lanza la BLL, para reutilizar la misma traducción.
        /// </summary>
        public static CompatibilidadReserva Evaluar(
            Experiencia exp, Suscripcion sus, PlanSuscripcion plan,
            int? idCiudadCliente, string nombreCiudadCliente, int edadCliente, int invitados,
            bool yaAsistio, string nombreSolapada, bool yaEnCola, bool hayMargenOferta,
            bool permitirFueraDeCiudad, bool bloquearSiYaRealizada)
        {
            var r = new CompatibilidadReserva
            {
                IdExperiencia      = exp.IdExperiencia,
                LugaresPedidos     = 1 + Math.Max(0, invitados),
                LugaresDisponibles = Math.Max(0, exp.CupoDisponible)
            };

            // ── Reglas DURAS (idéntico orden y mensajes que CrearReserva) ──────────
            if (exp.Premium && (sus == null || !sus.AccesoPremiumDelPlan))
                return Incompat(r, "err.bll.reserva.premium",
                    "'{0}' es una experiencia premium; el plan del cliente no incluye acceso premium.", exp.Nombre);

            if (!exp.CumpleEdadMinima(edadCliente))
                return Incompat(r, "err.bll.reserva.edad_minima",
                    "La experiencia requiere una edad mínima de {0} años.", exp.EdadMinima);

            if (plan != null && !plan.AdmiteInvitados(Math.Max(0, invitados)))
                return Incompat(r, "err.bll.reserva.invitados",
                    "El plan permite hasta {0} invitado(s) por reserva.", plan.CantidadInvitados);

            if (!permitirFueraDeCiudad && idCiudadCliente.HasValue && exp.IdCiudad != idCiudadCliente.Value)
                return Incompat(r, "err.bll.reserva.fuera_ciudad",
                    "La experiencia es en {0} y el cliente está en {1}.", exp.NombreCiudad, nombreCiudadCliente);

            if (yaAsistio && bloquearSiYaRealizada)
                return Incompat(r, "err.bll.reserva.ya_realizada",
                    "El cliente ya asistió a '{0}' anteriormente.", exp.Nombre);

            if (!string.IsNullOrEmpty(nombreSolapada))
                return Incompat(r, "err.bll.reserva.solapamiento",
                    "El cliente ya tiene la reserva '{0}' que se superpone en horario con '{1}'.",
                    nombreSolapada, exp.Nombre);

            // ── Compatible: el cupo y la lista de espera definen el estado ─────────
            if (r.LugaresDisponibles >= r.LugaresPedidos) { r.Estado = EstadoCompatibilidad.Disponible; return r; }

            if (exp.EstaCompleta())   // cupo 0
            {
                r.Estado = yaEnCola        ? EstadoCompatibilidad.YaEnCola
                         : !hayMargenOferta ? EstadoCompatibilidad.ListaEsperaSinOferta
                         :                     EstadoCompatibilidad.ListaEspera;
                return r;
            }

            // 0 < disponible < pedidos → hay lugares pero no alcanzan para el grupo.
            r.Estado = EstadoCompatibilidad.SinLugar;
            return r;
        }

        private static CompatibilidadReserva Incompat(CompatibilidadReserva r, string clave, string fallback, params object[] args)
        {
            r.Estado = EstadoCompatibilidad.Incompatible;
            r.MotivoClave = clave; r.MotivoFallback = fallback; r.MotivoArgs = args;
            return r;
        }
    }
}

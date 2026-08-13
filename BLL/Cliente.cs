using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL
{
    /// <summary>
    /// Capa de Lógica de Negocio — Gestión de Clientes (suscriptores de experiencias).
    /// Los clientes NO son usuarios del sistema.
    /// </summary>
    public class Cliente : Interfaces.IClienteService
    {
        private readonly DAL.Interfaces.IClienteDAL dalCliente;
        private readonly DAL.Interes         dalInteres  = new DAL.Interes();
        private readonly DAL.Reserva         dalReserva  = new DAL.Reserva();
        private readonly Suscripcion         bllSus      = new Suscripcion();
        private readonly Servicios.Bitacora        bitacora    = new Servicios.Bitacora();
        private readonly Servicios.BitacoraNegocio bitacoraNeg = new Servicios.BitacoraNegocio();

        // DI: el constructor por defecto usa el DAL real; el otro permite inyectar un doble.
        public Cliente() : this(new DAL.Cliente()) { }
        public Cliente(DAL.Interfaces.IClienteDAL dalCliente) { this.dalCliente = dalCliente; }

        public List<BE.Cliente> ObtenerTodos()      => dalCliente.ObtenerTodos();
        public BE.Cliente ObtenerPorId(int id)      => dalCliente.ObtenerPorId(id);

        // Alta de cliente. Si trae Suscripcion cargada, crea la suscripción inicial;
        // si trae Intereses, los persiste en ClienteInteres.
        public void Alta(string modulo, BE.Cliente cliente)
        {
            PermisosAccion.Exigir(BE.Patentes.ClientesEditar, BE.Patentes.Clientes);
            Validar(cliente, esAlta: true);

            if (dalCliente.ExisteDNI(cliente.DNI))
                throw new BE.AppException("err.bll.cliente.dni_duplicado", "Ya existe un cliente con DNI {0}.", cliente.DNI);

            cliente.FechaAlta = DateTime.Now;
            int idNuevo = dalCliente.Alta(cliente);
            cliente.IdCliente = idNuevo;

            GuardarIntereses(cliente);
            if (cliente.Suscripcion != null)
                bllSus.Crear(idNuevo, cliente.Suscripcion.IdPlan, cliente.Suscripcion.FechaVencimiento);

            bitacora.Registrar(modulo, $"Alta Cliente: {cliente.NombreCompleto} (DNI {cliente.DNI})", BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.AltaCliente,
                $"Nuevo cliente: {cliente.NombreCompleto} — DNI {cliente.DNI} — Ciudad: {cliente.NombreCiudad ?? "-"}",
                idCliente: idNuevo);
        }

        public void Modificar(string modulo, BE.Cliente cliente)
        {
            PermisosAccion.Exigir(BE.Patentes.ClientesEditar, BE.Patentes.Clientes);
            Validar(cliente, esAlta: false);

            if (dalCliente.ExisteDNIParaOtro(cliente.DNI, cliente.IdCliente))
                throw new BE.AppException("err.bll.cliente.dni_duplicado_otro",
                    "El DNI {0} ya está registrado para otro cliente.", cliente.DNI);

            dalCliente.Modificar(cliente);
            GuardarIntereses(cliente);

            bitacora.Registrar(modulo, $"Modificar Cliente ID {cliente.IdCliente}: {cliente.NombreCompleto}", BE.Criticidad.Baja);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.ModificacionCliente,
                $"Modificación cliente: {cliente.NombreCompleto} — DNI {cliente.DNI}", idCliente: cliente.IdCliente);
        }

        // Baja lógica. No se permite si el cliente tiene reservas activas (Pendiente/Confirmada).
        public void Baja(string modulo, BE.Cliente cliente)
        {
            PermisosAccion.Exigir(BE.Patentes.ClientesEditar, BE.Patentes.Clientes);

            int activas = dalReserva.ObtenerPorCliente(cliente.IdCliente).Count(r => r.OcupaCupo());
            if (activas > 0)
                throw new BE.AppException("err.bll.cliente.baja_reservas",
                    "No se puede eliminar a {0}: tiene {1} reserva(s) activa(s). Cancelalas primero.",
                    cliente.NombreCompleto, activas);

            dalCliente.Baja(cliente.IdCliente);
            bitacora.Registrar(modulo, $"Baja Cliente ID {cliente.IdCliente}: {cliente.NombreCompleto}", BE.Criticidad.Media);
            bitacoraNeg.Registrar(BE.TipoEventoNegocio.BajaCliente,
                $"Baja cliente: {cliente.NombreCompleto} — DNI {cliente.DNI}", idCliente: cliente.IdCliente);
        }

        // Evalúa si un cliente puede reservar. DTO listo para la GUI (sin reglas de negocio en la vista).
        public BE.EstadoComercialCliente ObtenerEstadoComercial(BE.Cliente cliente, int reservasSolicitadas)
        {
            if (cliente == null) throw new ArgumentNullException(nameof(cliente));

            if (!cliente.TieneSuscripcion())
                return new BE.EstadoComercialCliente { PuedeProceder = false, MotivoBloqueo = "SIN_SUSCRIPCION", FechaAlta = cliente.FechaAlta };

            var s = cliente.Suscripcion;
            if (!cliente.SuscripcionVigente())
                return new BE.EstadoComercialCliente
                {
                    PuedeProceder = false, MotivoBloqueo = "SUSCRIPCION_VENCIDA",
                    NombrePlan = s.NombrePlan, FechaVencimiento = s.FechaVencimiento, FechaAlta = cliente.FechaAlta
                };

            int disponibles = s.ReservasRestantes();
            bool supera = reservasSolicitadas > disponibles;

            return new BE.EstadoComercialCliente
            {
                PuedeProceder         = disponibles > 0,
                MotivoBloqueo         = disponibles > 0 ? null : "SIN_RESERVAS",
                NombrePlan            = s.NombrePlan,
                ReservasConsumidasMes = s.ReservasConsumidasMes,
                ReservasMensuales     = s.ReservasDelPlan,
                ReservasDisponibles   = disponibles,
                SuperaLimite          = supera,
                AccesoPremium         = s.AccesoPremiumDelPlan,
                FechaAlta             = cliente.FechaAlta,
                FechaVencimiento      = s.FechaVencimiento,
                SuscripcionProximaAVencer = s.ProximaAVencer(),
                DiasHastaVencimiento  = s.DiasHastaVencimiento()
            };
        }

        private void GuardarIntereses(BE.Cliente cliente)
        {
            if (cliente.Intereses == null) return;
            dalInteres.GuardarInteresesCliente(cliente.IdCliente, cliente.Intereses.Select(i => i.IdInteres));
        }

        private void Validar(BE.Cliente cliente, bool esAlta)
        {
            if (cliente == null) throw new ArgumentNullException(nameof(cliente));

            if (string.IsNullOrWhiteSpace(cliente.Nombre))
                throw new BE.AppException("err.bll.cliente.nombre_requerido", "El nombre del cliente es obligatorio.");
            if (string.IsNullOrWhiteSpace(cliente.Apellido))
                throw new BE.AppException("err.bll.cliente.apellido_requerido", "El apellido del cliente es obligatorio.");
            if (string.IsNullOrWhiteSpace(cliente.DNI))
                throw new BE.AppException("err.bll.cliente.dni_requerido", "El DNI del cliente es obligatorio.");
            if (cliente.DNI.Length < 7 || cliente.DNI.Length > 8)
                throw new BE.AppException("err.bll.cliente.dni_formato", "El DNI debe tener entre 7 y 8 dígitos.");
            foreach (char c in cliente.DNI)
                if (!char.IsDigit(c))
                    throw new BE.AppException("err.bll.cliente.dni_numeros", "El DNI solo puede contener números.");
            if (!cliente.IdCiudad.HasValue)
                throw new BE.AppException("err.bll.cliente.ciudad_requerida", "Debe seleccionar una ciudad.");
            if (!cliente.FechaNacimiento.HasValue)
                throw new BE.AppException("err.bll.cliente.fechanac_requerida", "La fecha de nacimiento es obligatoria.");
            if (cliente.FechaNacimiento.Value.Date > DateTime.Today.AddYears(-18))
                throw new BE.AppException("err.bll.cliente.menor_edad", "El cliente debe ser mayor de 18 años.");
            if (esAlta && (cliente.Suscripcion == null || cliente.Suscripcion.IdPlan <= 0))
                throw new BE.AppException("err.bll.cliente.plan_requerido", "Debe seleccionar un plan de suscripción.");
        }
    }
}

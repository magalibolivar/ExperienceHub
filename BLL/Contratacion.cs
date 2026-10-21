using System;
using System.Collections.Generic;

namespace BLL
{
    /// <summary>
    /// Lógica de negocio de PN01 — Comercialización de la suscripción.
    ///   CrearContratacion() — Venta registra la contratación (pendiente de pago) [CU01-VTA]
    ///   RegistrarCobro()    — Caja cobra y emite el comprobante en un paso; al 3er intento
    ///                          fallido cancela la contratación           [CU01-CAJ + CU02-CAJ]
    ///   Formalizar()        — Venta formaliza la suscripción vigente      [CU01-VTA final]
    ///
    /// Los CU de consultar planes (CU02-VTA) y registrar cliente (CU03-VTA) ya los cubren
    /// BLL.PlanSuscripcion.ObtenerActivos() y BLL.Cliente.Alta(), respectivamente.
    /// </summary>
    public class Contratacion
    {
        private readonly DAL.Contratacion   dal        = new DAL.Contratacion();
        private readonly DAL.Cliente         dalCliente = new DAL.Cliente();
        private readonly DAL.PlanSuscripcion dalPlan    = new DAL.PlanSuscripcion();
        private readonly DAL.Suscripcion     dalSus     = new DAL.Suscripcion();
        private readonly Servicios.Bitacora  bitacora   = new Servicios.Bitacora();

        // ── Parámetros de negocio configurables ────────────────────────────────
        /// <summary>Intentos de pago permitidos antes de cancelar la contratación.</summary>
        public const int MAX_INTENTOS_PAGO = 3;
        /// <summary>Meses de vigencia que se le dan a la suscripción al formalizarla.</summary>
        private const int MESES_VIGENCIA = 1;

        // ── Consultas ───────────────────────────────────────────────────────────
        public List<BE.Contratacion> ObtenerPendientes()   => dal.ObtenerPendientes();
        /// <summary>Cola de formalización (Venta): contrataciones Pagadas sin formalizar.</summary>
        public List<BE.Contratacion> ObtenerPagadas()      => dal.ObtenerPagadas();
        /// <summary>Cola para la pantalla de Caja: contrataciones Pendientes o Pagadas (sin formalizar aún).</summary>
        public List<BE.Contratacion> ObtenerActivas()      => dal.ObtenerActivas();
        public BE.Contratacion       ObtenerPorId(int id)  => dal.ObtenerPorId(id);

        // ── CU01-VTA (parte 1) — Venta registra la contratación ──────────────────
        /// <summary>
        /// Asocia cliente + plan y deja la contratación pendiente de pago, a derivarse a Caja.
        /// La suscripción todavía NO queda vigente (eso ocurre al formalizar, tras el cobro).
        /// </summary>
        public int CrearContratacion(string modulo, int idCliente, int idPlan)
        {
            PermisosAccion.Exigir(BE.Patentes.ContratacionVenta, BE.Patentes.ContratacionVenta);

            var cliente = dalCliente.ObtenerPorId(idCliente)
                ?? throw new BE.AppException("err.bll.contratacion.cliente_inexistente",
                    "El cliente seleccionado no existe.");

            var plan = dalPlan.ObtenerPorId(idPlan);
            if (plan == null || !plan.Estado)
                throw new BE.AppException("err.bll.contratacion.plan_inactivo",
                    "El plan seleccionado no existe o no está activo.");

            var c = new BE.Contratacion
            {
                IdCliente    = idCliente,
                IdPlan       = idPlan,
                Importe      = plan.Precio,
                Estado       = BE.EstadoContratacion.PendienteDePago,
                IntentosPago = 0,
                FechaAlta    = DateTime.Now
            };
            int id = dal.Alta(c);

            bitacora.Registrar(modulo,
                $"Contratación #{id} creada (pendiente de pago) — {cliente.NombreCompleto} — plan {plan.Nombre} — ${plan.Precio:0.00}",
                BE.Criticidad.Media);
            return id;
        }

        // ── CU01-VTA (renovación) — Venta registra la renovación cobrada ─────────
        /// <summary>
        /// Crea una contratación de tipo Renovación (Pendiente de pago) para extender la suscripción
        /// del cliente. Requiere que el cliente tenga una suscripción Activa o Vencida (una Suspendida
        /// se reactiva, no se renueva) y que no tenga otra contratación en proceso. El plan puede ser
        /// el mismo o uno distinto (upgrade/downgrade); el importe es el del plan elegido.
        /// </summary>
        public int CrearRenovacion(string modulo, int idCliente, int idPlan)
        {
            PermisosAccion.Exigir(BE.Patentes.ContratacionVenta, BE.Patentes.ContratacionVenta);

            var cliente = dalCliente.ObtenerPorId(idCliente)
                ?? throw new BE.AppException("err.bll.contratacion.cliente_inexistente",
                    "El cliente seleccionado no existe.");

            var plan = dalPlan.ObtenerPorId(idPlan);
            if (plan == null || !plan.Estado)
                throw new BE.AppException("err.bll.contratacion.plan_inactivo",
                    "El plan seleccionado no existe o no está activo.");

            var sus = dalSus.ObtenerVigentePorCliente(idCliente);
            if (sus == null)
                throw new BE.AppException("err.bll.contratacion.sin_suscripcion",
                    "El cliente no tiene una suscripción para renovar. Usá una contratación de alta.");
            // Solo se renueva una Activa o Vencida; una Suspendida se reactiva manualmente.
            var estadoReal = sus.EstadoVigenciaCalculado(DateTime.Today);
            if (estadoReal == BE.EstadoSuscripcion.Suspendida)
                throw new BE.AppException("err.bll.contratacion.suspendida",
                    "La suscripción está suspendida. Reactivala antes de renovar.");

            // Una contratación en proceso (Pendiente o Pagada) bloquea otra (alta o renovación).
            if (dal.ObtenerActivas().Exists(c => c.IdCliente == idCliente))
                throw new BE.AppException("err.bll.contratacion.en_proceso",
                    "El cliente ya tiene una contratación en proceso. Resolvela antes de crear otra.");

            var renov = new BE.Contratacion
            {
                IdCliente             = idCliente,
                IdPlan                = idPlan,
                Importe               = plan.Precio,
                Estado                = BE.EstadoContratacion.PendienteDePago,
                IntentosPago          = 0,
                FechaAlta             = DateTime.Now,
                Tipo                  = BE.TipoContratacion.Renovacion,
                IdSuscripcionRenovada = sus.IdSuscripcion
            };
            int id = dal.Alta(renov);

            bitacora.Registrar(modulo,
                $"Renovación #{id} creada (pendiente de pago) — {cliente.NombreCompleto} — plan {plan.Nombre} — ${plan.Precio:0.00} — extiende suscripción #{sus.IdSuscripcion}",
                BE.Criticidad.Media);
            return id;
        }

        // ── CU01-CAJ + CU02-CAJ — Caja cobra y emite comprobante (unificado) ─────
        /// <summary>
        /// Registra el resultado del cobro. Si el pago se concreta, marca la contratación Pagada,
        /// registra el medio y emite el comprobante (mismo paso). Si no se concreta, suma un intento
        /// fallido; al alcanzar <see cref="MAX_INTENTOS_PAGO"/> la contratación queda Cancelada.
        /// Devuelve la contratación actualizada.
        /// </summary>
        public BE.Contratacion RegistrarCobro(string modulo, int idContratacion, BE.MedioPago medio, int cuotas, bool pagoConcretado)
        {
            // Caja: cobra y, si se agotan los intentos, cancela la contratación.
            PermisosAccion.Exigir(BE.Patentes.ContratacionCaja, BE.Patentes.ContratacionCaja);

            // Regla de negocio: las cuotas deben ser válidas para el medio (solo Tarjeta financia).
            if (!BE.Contratacion.CuotasValidas(medio, cuotas))
                throw new BE.AppException("err.bll.contratacion.cuotas_invalidas",
                    "El medio de pago '{0}' no admite {1} cuota(s).", medio, cuotas);

            var c = dal.ObtenerPorId(idContratacion)
                ?? throw new BE.AppException("err.bll.contratacion.inexistente", "La contratación no existe.");
            if (!c.PuedeCobrarse())
                throw new BE.AppException("err.bll.contratacion.no_pendiente",
                    "La contratación no está pendiente de pago (estado '{0}').", c.Estado);

            if (pagoConcretado)
            {
                DateTime fecha = DateTime.Now;
                string nro = BE.Contratacion.GenerarNumeroComprobante(idContratacion, fecha);

                // Se SELLAN el recargo y el total financiado con el que realmente se cobra (no se recalculan).
                decimal recargo = BE.Contratacion.RecargoPorCuotas(cuotas);
                decimal total   = BE.Contratacion.ImporteConRecargo(c.Importe, cuotas);
                decimal porCuota = BE.Contratacion.ImportePorCuota(c.Importe, cuotas);

                dal.RegistrarPago(idContratacion, medio, nro, fecha, cuotas, recargo, total);
                bitacora.Registrar(modulo,
                    $"Cobro CONCRETADO contratación #{idContratacion} — {medio} — {cuotas} cuota(s)" +
                    (recargo > 0 ? $" (+{recargo:0.##}% financiación)" : "") +
                    $" — total ${total:0.00} ({cuotas}x ${porCuota:0.00}) — comprobante {nro}",
                    BE.Criticidad.Media);
            }
            else
            {
                var estadoResultante = BE.Contratacion.EstadoTrasIntentoFallido(c.IntentosPago, MAX_INTENTOS_PAGO);
                dal.RegistrarIntentoFallido(idContratacion, estadoResultante);

                if (estadoResultante == BE.EstadoContratacion.Cancelada)
                    bitacora.Registrar(modulo,
                        $"Contratación #{idContratacion} CANCELADA tras {MAX_INTENTOS_PAGO} intentos de pago fallidos (constancia de cancelación).",
                        BE.Criticidad.Alta);
                else
                    bitacora.Registrar(modulo,
                        $"Intento de pago fallido #{c.IntentosPago + 1}/{MAX_INTENTOS_PAGO} en contratación #{idContratacion}.",
                        BE.Criticidad.Baja);
            }

            return dal.ObtenerPorId(idContratacion);
        }

        // ── CU01-VTA (parte final) — Venta formaliza la suscripción ──────────────
        /// <summary>
        /// Formaliza la suscripción vigente a partir de una contratación ya pagada: fija inicio y
        /// vencimiento, la deja Activa y marca la contratación como Formalizada. Devuelve el Id de
        /// la suscripción generada (constancia de suscripción).
        /// </summary>
        public int Formalizar(string modulo, int idContratacion)
        {
            // Venta formaliza la suscripción una vez confirmado el cobro.
            PermisosAccion.Exigir(BE.Patentes.ContratacionVenta, BE.Patentes.ContratacionVenta);

            var c = dal.ObtenerPorId(idContratacion)
                ?? throw new BE.AppException("err.bll.contratacion.inexistente", "La contratación no existe.");
            if (!c.PuedeFormalizarse())
                throw new BE.AppException("err.bll.contratacion.no_pagada",
                    "Solo se formalizan contrataciones pagadas (estado '{0}').", c.Estado);

            // Renovación: NO crea suscripción nueva; extiende la existente un mes desde su
            // vencimiento actual (si sigue vigente) o desde hoy (si ya venció).
            if (c.Tipo == BE.TipoContratacion.Renovacion && c.IdSuscripcionRenovada.HasValue)
            {
                var susActual = dalSus.ObtenerPorId(c.IdSuscripcionRenovada.Value)
                    ?? throw new BE.AppException("err.bll.contratacion.sus_inexistente",
                        "La suscripción a renovar ya no existe.");

                DateTime baseCalc = (susActual.FechaVencimiento.HasValue && susActual.FechaVencimiento.Value.Date >= DateTime.Today)
                    ? susActual.FechaVencimiento.Value.Date
                    : DateTime.Today;
                DateTime nuevoVenc = baseCalc.AddMonths(MESES_VIGENCIA);

                dal.FormalizarRenovacion(idContratacion, susActual.IdSuscripcion, nuevoVenc,
                    BE.Suscripcion.InicioDeMes(DateTime.Today));

                bitacora.Registrar(modulo,
                    $"Renovación formalizada desde contratación #{idContratacion} — suscripción #{susActual.IdSuscripcion} extendida hasta {nuevoVenc:dd/MM/yyyy}.",
                    BE.Criticidad.Media);
                return susActual.IdSuscripcion;
            }

            // Alta: crea la suscripción vigente nueva.
            DateTime inicio = DateTime.Today;
            DateTime vencimiento = inicio.AddMonths(MESES_VIGENCIA);

            var s = new BE.Suscripcion
            {
                IdCliente             = c.IdCliente,
                IdPlan                = c.IdPlan,
                FechaInicio           = inicio,
                FechaVencimiento      = vencimiento,
                Estado                = BE.EstadoSuscripcion.Activa,
                ReservasConsumidasMes = 0
            };
            int idSus = dal.FormalizarConSuscripcion(idContratacion, s);

            bitacora.Registrar(modulo,
                $"Suscripción #{idSus} formalizada desde contratación #{idContratacion} — vigente hasta {vencimiento:dd/MM/yyyy} (constancia de suscripción).",
                BE.Criticidad.Media);
            return idSus;
        }
    }
}

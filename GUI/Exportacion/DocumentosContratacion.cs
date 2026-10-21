using System;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI.Exportacion
{
    /// <summary>
    /// Documentos de PN01 (Comercialización) generados a PDF vía <see cref="ExportadorPdf"/>
    /// (vista previa → "Microsoft Print to PDF"). Son documentos de TEXTO: el cuerpo de cada uno se
    /// arma desde una plantilla i18n multilínea (mismo patrón que eh.nva.plan.detalle), así se
    /// traducen en los 4 idiomas sin inflar el corpus. No contienen lógica de negocio: solo formatean
    /// datos ya resueltos por la BLL.
    ///
    /// Mapeo con el diagrama de actividad de PN01:
    ///   2) Condiciones del plan      → Condiciones(...)         (paso Plan, antes de aceptar)
    ///   6) Factura / Comprobante     → Comprobante(...)         (al concretarse el pago)
    ///   7) Constancia de suscripción → ConstanciaSuscripcion(...)(al formalizar)
    ///   5) Constancia de cancelación → ConstanciaCancelacion(...)(al cancelar por 3 intentos)
    /// </summary>
    public static class DocumentosContratacion
    {
        private const string ORIGEN = "Comercialización";

        private static string T(string clave, string fallback)
        {
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            return t.ContainsKey(clave) ? t[clave].Texto : fallback;
        }

        private static string Medio(BE.MedioPago m)
        {
            switch (m)
            {
                case BE.MedioPago.Tarjeta:       return T("eh.medio.tarjeta", "Tarjeta");
                case BE.MedioPago.Transferencia: return T("eh.medio.transferencia", "Transferencia");
                default:                         return T("eh.medio.efectivo", "Efectivo");
            }
        }

        // ── 2 · Condiciones del plan ──────────────────────────────────────────────
        public static ReporteExportable Condiciones(BE.PlanSuscripcion p)
        {
            string cuerpo = string.Format(
                T("eh.doc.condiciones",
                  "CONDICIONES DEL PLAN DE SUSCRIPCIÓN\n\n" +
                  "Plan: {0}\nValor de la suscripción: ${1:N2}\n" +
                  "Reservas incluidas por mes: {2}\nAcceso premium: {3}\n" +
                  "Invitados por reserva: hasta {4}\nAnticipación máxima de reserva: {5} días\n" +
                  "Vigencia: 1 mes desde la formalización, renovable.\n\n" +
                  "Condiciones particulares:\n" +
                  "- La suscripción se activa una vez confirmado el pago.\n" +
                  "- Las reservas no consumidas no se acumulan al mes siguiente.\n" +
                  "- La falta de pago de la renovación hace caducar la suscripción en su fecha de vencimiento."),
                p.Nombre, p.Precio, p.ReservasMensuales, p.AccesoPremium ? T("eh.si", "Sí") : "—",
                p.CantidadInvitados, p.AnticipacionMaximaDias);

            return Doc(T("eh.doc.condiciones.tit", "Condiciones del plan"),
                       $"Condiciones_{Sanit(p.Nombre)}", cuerpo);
        }

        // ── 6 · Factura / Comprobante ─────────────────────────────────────────────
        public static ReporteExportable Comprobante(BE.Contratacion c)
        {
            string lineaCuotas = c.Cuotas > 1
                ? string.Format(T("eh.doc.comp.cuotas", "Cuotas: {0} de ${1:N2}\nRecargo financiación: {2:0.##}%\n"),
                    c.Cuotas, BE.Contratacion.ImportePorCuota(c.Importe, c.Cuotas), c.RecargoPorcentaje)
                : "";
            string concepto = c.Tipo == BE.TipoContratacion.Renovacion
                ? T("eh.doc.concepto.renov", "Renovación de suscripción")
                : T("eh.doc.concepto.alta", "Alta de suscripción");

            string cuerpo = string.Format(
                T("eh.doc.comprobante",
                  "COMPROBANTE DE PAGO\n\n" +
                  "N° {0}\nFecha: {1:dd/MM/yyyy HH:mm}\n\n" +
                  "Cliente: {2}\nConcepto: {3} — Plan {4}\nMedio de pago: {5}\n{6}" +
                  "Importe base: ${7:N2}\nTOTAL COBRADO: ${8:N2}\n\n" +
                  "Comprobante interno de ExperienceHub (no válido como factura fiscal)."),
                c.NumeroComprobante ?? "—", c.FechaComprobante ?? c.FechaPago ?? DateTime.Now,
                c.NombreCliente, concepto, c.NombrePlan, c.Medio.HasValue ? Medio(c.Medio.Value) : "—",
                lineaCuotas, c.Importe, c.ImporteTotal > 0 ? c.ImporteTotal : c.Importe);

            return Doc(T("eh.doc.comprobante.tit", "Comprobante de pago"),
                       $"Comprobante_{c.IdContratacion:D6}", cuerpo);
        }

        // ── 7 · Constancia de suscripción ─────────────────────────────────────────
        public static ReporteExportable ConstanciaSuscripcion(BE.Contratacion c, BE.Suscripcion s)
        {
            string cuerpo = string.Format(
                T("eh.doc.constancia",
                  "CONSTANCIA DE SUSCRIPCIÓN\n\n" +
                  "Fecha de emisión: {0:dd/MM/yyyy}\n\n" +
                  "Se deja constancia de la suscripción vigente de:\n" +
                  "Cliente: {1}\nPlan: {2}\nInicio: {3:dd/MM/yyyy}\n" +
                  "Vigente hasta: {4}\nEstado: {5}\n" +
                  "Comprobante de pago asociado: {6}"),
                DateTime.Now, c.NombreCliente, s.NombrePlan ?? c.NombrePlan,
                s.FechaInicio,
                s.FechaVencimiento.HasValue ? s.FechaVencimiento.Value.ToString("dd/MM/yyyy") : T("eh.cli.sus.sinlimite", "sin límite"),
                s.Estado, c.NumeroComprobante ?? "—");

            return Doc(T("eh.doc.constancia.tit", "Constancia de suscripción"),
                       $"Constancia_Suscripcion_{c.IdContratacion:D6}", cuerpo);
        }

        // ── 5 · Constancia de cancelación ─────────────────────────────────────────
        public static ReporteExportable ConstanciaCancelacion(BE.Contratacion c, int maxIntentos)
        {
            string cuerpo = string.Format(
                T("eh.doc.cancelacion",
                  "CONSTANCIA DE CANCELACIÓN DE CONTRATACIÓN\n\n" +
                  "Fecha: {0:dd/MM/yyyy HH:mm}\n\n" +
                  "Contratación N° {1}\nCliente: {2}\nPlan: {3}\nImporte: ${4:N2}\n\n" +
                  "Motivo: se alcanzó el máximo de {5} intentos de pago sin concretarse.\n" +
                  "Estado: Cancelada."),
                DateTime.Now, c.IdContratacion, c.NombreCliente, c.NombrePlan, c.Importe, maxIntentos);

            return Doc(T("eh.doc.cancelacion.tit", "Constancia de cancelación"),
                       $"Constancia_Cancelacion_{c.IdContratacion:D6}", cuerpo);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────
        /// <summary>Abre la vista previa imprimible del documento (desde ahí se exporta a PDF).</summary>
        public static void Mostrar(ReporteExportable doc, IWin32Window propietario)
            => new ExportadorPdf(ORIGEN).Exportar(doc, propietario);

        private static ReporteExportable Doc(string titulo, string nombreArchivo, string cuerpo)
            => new ReporteExportable { Titulo = titulo, NombreArchivo = nombreArchivo, TextoPlano = cuerpo };

        private static string Sanit(string s)
            => System.Text.RegularExpressions.Regex.Replace(s ?? "", @"[^\w]", "_");
    }
}

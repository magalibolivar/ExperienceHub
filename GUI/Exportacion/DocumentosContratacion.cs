using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI.Exportacion
{
    /// <summary>
    /// Documentos de PN01 (Comercialización) como <see cref="DocumentoReporte"/> estructurado, renderizados
    /// con <see cref="ExportadorPdfArchivo"/> (PDF real a archivo, identidad ExperienceHub: encabezado
    /// institucional, fichas clave/valor, secciones, paginación y pie). Antes eran texto monoespaciado;
    /// ahora comparten el sistema documental unificado. No contienen lógica de negocio: solo formatean
    /// datos ya resueltos por la BLL.
    ///
    /// Mapeo con el diagrama de actividad de PN01:
    ///   2) Condiciones del plan      → Condiciones(...)
    ///   6) Comprobante de pago       → Comprobante(...)          (al concretarse el pago)
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

        private static DocumentoReporte Base(string titulo, string subtitulo, string nombreArchivo)
            => new DocumentoReporte
            {
                Institucion = "ExperienceHub",
                Modulo = T("eh.doc.modulo.pn01", "PN01 – Comercialización"),
                Titulo = titulo,
                Subtitulo = subtitulo,
                NombreArchivo = nombreArchivo
            };

        // ── 2 · Condiciones del plan ──────────────────────────────────────────────
        public static DocumentoReporte Condiciones(BE.PlanSuscripcion p)
        {
            var doc = Base(T("eh.doc.condiciones.tit", "Condiciones del plan"), p.Nombre, $"Condiciones_{Sanit(p.Nombre)}");
            doc.ClaveValor(new List<(string, string)>
            {
                (T("eh.doc.f.plan", "Plan"), p.Nombre),
                (T("eh.doc.f.valorsusc", "Valor de la suscripción"), p.Precio.ToString("$ #,##0.00")),
                (T("eh.doc.f.reservasmes", "Reservas incluidas por mes"), p.ReservasMensuales.ToString()),
                (T("eh.doc.f.premium", "Acceso premium"), p.AccesoPremium ? T("eh.si", "Sí") : "—"),
                (T("eh.doc.f.invitados", "Invitados por reserva"), string.Format(T("eh.doc.f.hasta", "hasta {0}"), p.CantidadInvitados)),
                (T("eh.doc.f.anticipacion", "Anticipación máxima de reserva"), string.Format(T("eh.doc.f.dias", "{0} días"), p.AnticipacionMaximaDias)),
                (T("eh.doc.f.vigencia", "Vigencia"), T("eh.doc.condiciones.vigencia", "1 mes desde la formalización, renovable.")),
            });
            doc.Seccion(T("eh.doc.condiciones.particulares", "Condiciones particulares"));
            doc.Parrafo("• " + T("eh.doc.condiciones.c1", "La suscripción se activa una vez confirmado el pago."));
            doc.Parrafo("• " + T("eh.doc.condiciones.c2", "Las reservas no consumidas no se acumulan al mes siguiente."));
            doc.Parrafo("• " + T("eh.doc.condiciones.c3", "La falta de pago de la renovación hace caducar la suscripción en su fecha de vencimiento."));
            return doc;
        }

        // ── 6 · Comprobante de pago ───────────────────────────────────────────────
        public static DocumentoReporte Comprobante(BE.Contratacion c)
        {
            string concepto = c.Tipo == BE.TipoContratacion.Renovacion
                ? T("eh.doc.concepto.renov", "Renovación de suscripción")
                : T("eh.doc.concepto.alta", "Alta de suscripción");
            var doc = Base(T("eh.doc.comprobante.tit", "Comprobante de pago"),
                           "N° " + (c.NumeroComprobante ?? "—"), $"Comprobante_{c.IdContratacion:D6}");
            var items = new List<(string, string)>
            {
                (T("eh.doc.f.fecha", "Fecha"), (c.FechaComprobante ?? c.FechaPago ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm")),
                (T("eh.doc.f.cliente", "Cliente"), c.NombreCliente ?? "—"),
                (T("eh.doc.f.concepto", "Concepto"), concepto + " — " + c.NombrePlan),
                (T("eh.doc.f.medio", "Medio de pago"), c.Medio.HasValue ? Medio(c.Medio.Value) : "—"),
            };
            if (c.Cuotas > 1)
            {
                items.Add((T("eh.doc.f.cuotas", "Cuotas"), string.Format(T("eh.doc.f.cuotasval", "{0} de {1}"), c.Cuotas, BE.Contratacion.ImportePorCuota(c.Importe, c.Cuotas).ToString("$ #,##0.00"))));
                items.Add((T("eh.doc.f.recargo", "Recargo financiación"), c.RecargoPorcentaje.ToString("0.##") + "%"));
            }
            items.Add((T("eh.doc.f.importebase", "Importe base"), c.Importe.ToString("$ #,##0.00")));
            items.Add((T("eh.doc.f.totalcobrado", "TOTAL COBRADO"), (c.ImporteTotal > 0 ? c.ImporteTotal : c.Importe).ToString("$ #,##0.00")));
            doc.ClaveValor(items);
            doc.Nota(T("eh.doc.comprobante.nota", "Comprobante interno de ExperienceHub (no válido como factura fiscal)."));
            return doc;
        }

        // ── 7 · Constancia de suscripción ─────────────────────────────────────────
        public static DocumentoReporte ConstanciaSuscripcion(BE.Contratacion c, BE.Suscripcion s)
        {
            var doc = Base(T("eh.doc.constancia.tit", "Constancia de suscripción"), c.NombreCliente, $"Constancia_Suscripcion_{c.IdContratacion:D6}");
            doc.Parrafo(T("eh.doc.constancia.intro", "Se deja constancia de la suscripción vigente de:"));
            doc.ClaveValor(new List<(string, string)>
            {
                (T("eh.doc.f.emision", "Fecha de emisión"), DateTime.Now.ToString("dd/MM/yyyy")),
                (T("eh.doc.f.cliente", "Cliente"), c.NombreCliente ?? "—"),
                (T("eh.doc.f.plan", "Plan"), s.NombrePlan ?? c.NombrePlan),
                (T("eh.doc.f.inicio", "Inicio"), s.FechaInicio.ToString("dd/MM/yyyy")),
                (T("eh.doc.f.vigentehasta", "Vigente hasta"), s.FechaVencimiento.HasValue ? s.FechaVencimiento.Value.ToString("dd/MM/yyyy") : T("eh.cli.sus.sinlimite", "sin límite")),
                (T("eh.doc.f.estado", "Estado"), s.Estado.ToString()),
                (T("eh.doc.f.compasociado", "Comprobante de pago asociado"), c.NumeroComprobante ?? "—"),
            });
            return doc;
        }

        // ── 5 · Constancia de cancelación ─────────────────────────────────────────
        public static DocumentoReporte ConstanciaCancelacion(BE.Contratacion c, int maxIntentos)
        {
            var doc = Base(T("eh.doc.cancelacion.tit", "Constancia de cancelación"), "N° " + c.IdContratacion, $"Constancia_Cancelacion_{c.IdContratacion:D6}");
            doc.ClaveValor(new List<(string, string)>
            {
                (T("eh.doc.f.fecha", "Fecha"), DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                (T("eh.doc.f.contratacion", "Contratación N°"), c.IdContratacion.ToString()),
                (T("eh.doc.f.cliente", "Cliente"), c.NombreCliente ?? "—"),
                (T("eh.doc.f.plan", "Plan"), c.NombrePlan ?? "—"),
                (T("eh.doc.f.importe", "Importe"), c.Importe.ToString("$ #,##0.00")),
                (T("eh.doc.f.estado", "Estado"), T("eh.res.estado.cancelada", "Cancelada")),
            });
            doc.Parrafo(string.Format(T("eh.doc.cancelacion.motivo", "Motivo: se alcanzó el máximo de {0} intentos de pago sin concretarse."), maxIntentos));
            return doc;
        }

        // ── Helper de presentación ───────────────────────────────────────────────
        /// <summary>Genera el PDF directo a archivo (PDFsharp): diálogo de guardado + abrir al terminar.</summary>
        public static void Mostrar(DocumentoReporte doc, IWin32Window propietario)
            => new ExportadorPdfArchivo().Exportar(doc, propietario);

        private static string Sanit(string s)
            => System.Text.RegularExpressions.Regex.Replace(s ?? "", @"[^\w]", "_");
    }
}

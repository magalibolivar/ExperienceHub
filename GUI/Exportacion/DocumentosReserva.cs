using System;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI.Exportacion
{
    /// <summary>
    /// Documento de PN02 (Reserva de Experiencia) generado a PDF vía <see cref="ExportadorPdf"/>
    /// (vista previa → "Microsoft Print to PDF"). Es un documento de TEXTO: el cuerpo se arma desde
    /// una plantilla i18n multilínea (mismo patrón que <see cref="DocumentosContratacion"/>), así se
    /// traduce en los 4 idiomas sin inflar el corpus. No contiene lógica de negocio: solo formatea
    /// datos ya resueltos y persistidos por la BLL/DAL.
    ///
    /// Mapea la actividad 16 del diagrama PN02 ("Generar confirmación de reserva"). Solo debe pedirse
    /// para reservas CONFIRMADAS (el llamador lo garantiza): no se emite para Pendientes/Canceladas.
    ///
    /// Nota de datos: la tabla [Reserva] no persiste una "fecha de confirmación" como columna; por eso
    /// el documento muestra la fecha de la reserva (real) y la fecha de emisión del comprobante. No se
    /// inventa ninguna fecha de confirmación.
    /// </summary>
    public static class DocumentosReserva
    {
        private const string ORIGEN = "Reservas";

        private static string T(string clave, string fallback)
        {
            var t = Traductor.ObtenerTraducciones(GestorIdioma.IdiomaActual);
            return t.ContainsKey(clave) ? t[clave].Texto : fallback;
        }

        private static string EstadoTexto(BE.EstadoReserva e)
        {
            switch (e)
            {
                case BE.EstadoReserva.Pendiente:  return T("eh.res.estado.pendiente",  "Pendiente");
                case BE.EstadoReserva.Confirmada: return T("eh.res.estado.confirmada", "Confirmada");
                case BE.EstadoReserva.Cancelada:  return T("eh.res.estado.cancelada",  "Cancelada");
                case BE.EstadoReserva.Asistio:    return T("eh.res.estado.asistio",    "Asistió");
                case BE.EstadoReserva.NoAsistio:  return T("eh.res.estado.noasistio",  "No asistió");
                default:                          return e.ToString();
            }
        }

        // ── Confirmación de reserva (actividad 16 de PN02) ────────────────────────
        public static ReporteExportable ConfirmacionReserva(BE.Reserva r, BE.Experiencia exp = null)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));

            // Fechas y textos pre-formateados (la plantilla usa solo {N} planos → sin riesgo de
            // FormatException por cultura/formato en ningún idioma).
            string fechaExp = r.FechaHoraExperiencia.HasValue
                ? r.FechaHoraExperiencia.Value.ToString("dd/MM/yyyy HH:mm") : "—";
            string duracion = r.DuracionExperienciaMinutos > 0
                ? string.Format(T("eh.doc.reserva.minutos", "{0} min"), r.DuracionExperienciaMinutos) : "—";
            string lugares = string.Format(
                T("eh.doc.reserva.lugares", "{0} (1 titular + {1} invitado/s)"),
                r.LugaresOcupados, Math.Max(0, r.CantidadInvitados));
            string fechaReserva = r.FechaReserva.ToString("dd/MM/yyyy HH:mm");
            string emision = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            // Ubicación + ciudad REALES de la experiencia (si se pasó). BE.Reserva no las transporta,
            // por eso el llamador puede enviar la Experiencia; si no, queda "—".
            string lugar = "—";
            if (exp != null)
            {
                string u = (exp.Ubicacion ?? "").Trim();
                string c = (exp.NombreCiudad ?? "").Trim();
                if (u.Length > 0 && c.Length > 0) lugar = u + ", " + c;
                else if (u.Length > 0)            lugar = u;
                else if (c.Length > 0)            lugar = c;
            }

            string cuerpo = string.Format(
                T("eh.doc.reserva",
                  "CONFIRMACIÓN DE RESERVA\n\n" +
                  "N° {0}\nFecha de emisión: {1}\n\n" +
                  "Cliente: {2}\nExperiencia: {3}\nLugar: {9}\nFecha y horario: {4}\nDuración: {5}\n" +
                  "Lugares reservados: {6}\nEstado: {7}\nFecha de la reserva: {8}\n\n" +
                  "Presentá esta confirmación al llegar a la experiencia.\n" +
                  "Comprobante interno de ExperienceHub."),
                r.IdReserva, emision,
                r.NombreCliente ?? "—", r.NombreExperiencia ?? "—",
                fechaExp, duracion, lugares, EstadoTexto(r.Estado), fechaReserva, lugar);

            return Doc(T("eh.doc.reserva.tit", "Confirmación de reserva"),
                       $"Reserva_{r.IdReserva:D6}", cuerpo);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────
        /// <summary>Abre la vista previa imprimible del documento (desde ahí se exporta a PDF).</summary>
        public static void Mostrar(ReporteExportable doc, IWin32Window propietario)
            => new ExportadorPdf(ORIGEN).Exportar(doc, propietario);

        private static ReporteExportable Doc(string titulo, string nombreArchivo, string cuerpo)
            => new ReporteExportable { Titulo = titulo, NombreArchivo = nombreArchivo, TextoPlano = cuerpo };
    }
}

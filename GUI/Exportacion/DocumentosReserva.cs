using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Servicios.Multiidioma;

namespace GUI.Exportacion
{
    /// <summary>
    /// Documento de PN02 (Reserva de Experiencia) como <see cref="DocumentoReporte"/> estructurado,
    /// renderizado con <see cref="ExportadorPdfArchivo"/> (PDF real a archivo, sistema documental unificado).
    /// Antes era texto monoespaciado. No contiene lógica de negocio: solo formatea datos ya resueltos y
    /// persistidos por BLL/DAL.
    ///
    /// Mapea la actividad 16 del diagrama PN02 ("Generar confirmación de reserva"). Solo debe pedirse para
    /// reservas CONFIRMADAS (el llamador lo garantiza): no se emite para Pendientes/Canceladas.
    /// La tabla [Reserva] no persiste "fecha de confirmación"; se muestran la fecha de la reserva (real) y la
    /// de emisión del comprobante. No se inventa ninguna fecha.
    /// </summary>
    public static class DocumentosReserva
    {
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
        public static DocumentoReporte ConfirmacionReserva(BE.Reserva r, BE.Experiencia exp = null)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));

            string fechaExp = r.FechaHoraExperiencia.HasValue ? r.FechaHoraExperiencia.Value.ToString("dd/MM/yyyy HH:mm") : "—";
            string duracion = r.DuracionExperienciaMinutos > 0 ? string.Format(T("eh.doc.reserva.minutos", "{0} min"), r.DuracionExperienciaMinutos) : "—";
            string lugares = string.Format(T("eh.doc.reserva.lugares", "{0} (1 titular + {1} invitado/s)"), r.LugaresOcupados, Math.Max(0, r.CantidadInvitados));

            string lugar = "—";
            if (exp != null)
            {
                string u = (exp.Ubicacion ?? "").Trim();
                string c = (exp.NombreCiudad ?? "").Trim();
                if (u.Length > 0 && c.Length > 0) lugar = u + ", " + c;
                else if (u.Length > 0)            lugar = u;
                else if (c.Length > 0)            lugar = c;
            }

            var doc = new DocumentoReporte
            {
                Institucion = "ExperienceHub",
                Modulo = T("eh.doc.modulo.pn02", "PN02 – Reservas"),
                Titulo = T("eh.doc.reserva.tit", "Confirmación de reserva"),
                Subtitulo = "N° " + r.IdReserva,
                NombreArchivo = $"Reserva_{r.IdReserva:D6}"
            };
            doc.ClaveValor(new List<(string, string)>
            {
                (T("eh.doc.f.emision", "Fecha de emisión"), DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                (T("eh.doc.f.cliente", "Cliente"), r.NombreCliente ?? "—"),
                (T("eh.doc.f.experiencia", "Experiencia"), r.NombreExperiencia ?? "—"),
                (T("eh.doc.f.lugar", "Lugar"), lugar),
                (T("eh.doc.f.fechahorario", "Fecha y horario"), fechaExp),
                (T("eh.doc.f.duracion", "Duración"), duracion),
                (T("eh.doc.f.lugaresres", "Lugares reservados"), lugares),
                (T("eh.doc.f.estado", "Estado"), EstadoTexto(r.Estado)),
                (T("eh.doc.f.fechareserva", "Fecha de la reserva"), r.FechaReserva.ToString("dd/MM/yyyy HH:mm")),
            });
            doc.Parrafo(T("eh.doc.reserva.presenta", "Presentá esta confirmación al llegar a la experiencia."));
            doc.Nota(T("eh.doc.reserva.nota", "Comprobante interno de ExperienceHub."));
            return doc;
        }

        /// <summary>Genera el PDF directo a archivo (PDFsharp): diálogo de guardado + abrir al terminar.</summary>
        public static void Mostrar(DocumentoReporte doc, IWin32Window propietario)
            => new ExportadorPdfArchivo().Exportar(doc, propietario);
    }
}

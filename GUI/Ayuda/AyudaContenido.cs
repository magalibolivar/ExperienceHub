using System.Collections.Generic;

namespace GUI.Ayuda
{
    /// <summary>
    /// Registro centralizado del contenido de ayuda contextual, por pantalla. Cada entrada responde:
    /// dónde estoy (título), qué hace esta pantalla (cuerpo) y qué puedo hacer acá (pasos). Todo el
    /// texto sale de <see cref="I18n"/> (corpus TSV, 4 idiomas) — nada hardcodeado. Agregar una pantalla
    /// nueva al sistema de ayuda es agregar una entrada acá y pasar el contexto a
    /// <see cref="UI.Encabezado"/>; no requiere tocar <see cref="AyudaDialog"/>.
    /// </summary>
    internal static class AyudaContenido
    {
        public struct Contenido
        {
            public string Titulo;
            public string Cuerpo;
            public string[] Pasos;
        }

        private static string T(string k, string fb) => I18n.T(k, fb);

        public static Contenido? Obtener(string contexto)
        {
            switch (contexto)
            {
                case "menu":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.menu.tit", "ExperienceHub: panorama general"),
                        Cuerpo = T("eh.ayuda.menu.cuerpo", "ExperienceHub organiza el trabajo en tres procesos: Comercialización (contratar y cobrar planes), Reservas (gestionar experiencias y listas de espera) y Planificación (analizar indicadores y decidir acciones). Cada bloque del menú superior corresponde a uno de estos procesos."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.menu.p1", "Comercialización: contratar un plan nuevo o cobrar una contratación pendiente."),
                            T("eh.ayuda.menu.p2", "Reservas: crear, confirmar o cancelar reservas de experiencias."),
                            T("eh.ayuda.menu.p3", "Planificación: ver indicadores, generar recomendaciones y seguir planes de acción."),
                        }
                    };

                case "pn01.nuevacontratacion":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.nc.tit", "Nueva contratación: 3 pasos"),
                        Cuerpo = T("eh.ayuda.nc.cuerpo", "Este asistente te guía para dar de alta una suscripción: primero identificás o creás al cliente, después elegís el plan, y por último confirmás. El indicador de pasos de arriba muestra en cuál de los tres estás."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.nc.p1", "Paso 1: buscá al cliente por DNI o creá uno nuevo si no existe."),
                            T("eh.ayuda.nc.p2", "Paso 2: elegí el plan de suscripción que va a contratar."),
                            T("eh.ayuda.nc.p3", "Paso 3: confirmá los datos. La contratación queda pendiente de pago hasta que se cobre en Caja."),
                        }
                    };

                case "pn01.formalizacion":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.form.tit", "Formalizar contratación"),
                        Cuerpo = T("eh.ayuda.form.cuerpo", "Acá se registran los datos definitivos de una contratación antes del cobro: vigencia, condiciones del plan y comprobante. Una vez formalizada, pasa a la pantalla de Cobros."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.form.p1", "Revisá los datos del plan y de la suscripción antes de confirmar."),
                            T("eh.ayuda.form.p2", "Podés exportar la constancia de suscripción en PDF."),
                        }
                    };

                case "pn01.cobros":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.cob.tit", "Cobros pendientes"),
                        Cuerpo = T("eh.ayuda.cob.cuerpo", "Lista las contrataciones formalizadas que todavía no se cobraron. Al cobrar una, la suscripción del cliente queda activa de inmediato."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.cob.p1", "Seleccioná una contratación y registrá el cobro con su medio de pago."),
                            T("eh.ayuda.cob.p2", "Tras 3 intentos de pago sin concretarse, la contratación se cancela automáticamente."),
                            T("eh.ayuda.cob.p3", "El comprobante de pago se puede exportar en PDF desde acá."),
                        }
                    };

                case "pn02.reservas":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.res.tit", "Reservas de experiencias"),
                        Cuerpo = T("eh.ayuda.res.cuerpo", "Gestioná las reservas de un cliente: crear, confirmar, cancelar y marcar asistencia. Una reserva nace Confirmada si hay cupo disponible; si no, el cliente puede anotarse en la lista de espera."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.res.p1", "Elegí al cliente y la experiencia para crear una reserva nueva."),
                            T("eh.ayuda.res.p2", "Cancelar con 48hs o más de anticipación devuelve el beneficio del plan; cancelar después, no."),
                            T("eh.ayuda.res.p3", "Marcá asistencia o inasistencia una vez realizada la experiencia."),
                        }
                    };

                case "pn02.listaespera":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.le.tit", "Lista de espera"),
                        Cuerpo = T("eh.ayuda.le.cuerpo", "Cuando una experiencia está completa, los clientes interesados quedan anotados acá. Si se libera un lugar, se le ofrece automáticamente al primero de la lista, con un plazo para confirmar."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.le.p1", "Una oferta vencida pasa automáticamente al siguiente en la lista."),
                            T("eh.ayuda.le.p2", "Podés exportar la confirmación de reserva una vez que el cliente acepta la oferta."),
                        }
                    };

                case "pn03.resumen":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.pn03r.tit", "Resumen ejecutivo"),
                        Cuerpo = T("eh.ayuda.pn03r.cuerpo", "Visión general del período: qué pasó con la demanda y la oferta. Los filtros de fecha/ciudad/categoría/organizador de acá se comparten con las demás pantallas de Planificación."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.pn03r.p1", "Ajustá el período y los filtros, y tocá Aplicar."),
                            T("eh.ayuda.pn03r.p2", "Exportá el informe del tablero en PDF, o guardá esta vista de análisis para reabrirla después."),
                        }
                    };

                case "pn03.demanda":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.pn03d.tit", "Demanda y capacidad"),
                        Cuerpo = T("eh.ayuda.pn03d.cuerpo", "Identificá experiencias con poca ocupación (candidatas a revisar) y con mucha lista de espera (demanda insatisfecha). Usá las pestañas internas para alternar entre ocupación, espera y el detalle de capacidad."),
                        Pasos = null
                    };

                case "pn03.desempeno":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.pn03e.tit", "Desempeño y calidad"),
                        Cuerpo = T("eh.ayuda.pn03e.cuerpo", "Calidad (calificación de organizadores), comportamiento (inasistencias) y utilización (uso de planes), cada uno en su propia pestaña para no mezclarlos."),
                        Pasos = null
                    };

                case "pn03.decisiones":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.pn03de.tit", "Decisiones y recomendaciones"),
                        Cuerpo = T("eh.ayuda.pn03de.cuerpo", "El motor analiza el período y sugiere acciones (ampliar cupo, revisar un organizador, etc.) con su evidencia. Esta es la etapa ① del proceso de planificación: Analizar y decidir."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.pn03de.p1", "Tocá «Generar recomendaciones» para ver las sugerencias del período."),
                            T("eh.ayuda.pn03de.p2", "Marcá las que te interesen y creá un plan de acción con ellas."),
                            T("eh.ayuda.pn03de.p3", "El plan creado te lleva directo a la etapa ② Ejecutar el plan, en Planes de acción."),
                        }
                    };

                case "pn03.planes":
                    return new Contenido
                    {
                        Titulo = T("eh.ayuda.pn03p.tit", "Planes de acción"),
                        Cuerpo = T("eh.ayuda.pn03p.cuerpo", "Acá se recorren las etapas ② Ejecutar y ③ Evaluar de un plan. El indicador de progreso (arriba del detalle) muestra el AVANCE REAL del plan; la marca azul debajo de una etapa indica cuál estás mirando — son cosas distintas."),
                        Pasos = new[]
                        {
                            T("eh.ayuda.pn03p.p1", "Elegí un plan de la lista para ver su detalle."),
                            T("eh.ayuda.pn03p.p2", "En «Analizar y decidir»: aprobá o descartá cada recomendación."),
                            T("eh.ayuda.pn03p.p3", "En «Ejecutar el plan»: ejecutá las tareas ya aprobadas."),
                            T("eh.ayuda.pn03p.p4", "En «Evaluar resultados»: una vez cerrado el plan, esperá el período de observación y tocá «Evaluar resultados» para ver el comparativo real."),
                        }
                    };

                default:
                    return null;
            }
        }
    }
}

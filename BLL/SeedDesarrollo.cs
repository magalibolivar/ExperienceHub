using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace BLL
{
    /// <summary>
    /// Sembrado de datos de PRUEBA (solo desarrollo) para visualizar PN02 — Lista de Espera / Reservas.
    /// Crea experiencias (fechadas hasta diciembre), clientes con suscripción vigente, colas FIFO y
    /// ofertas vigentes EXCLUSIVAMENTE por los flujos de negocio (BLL), conservando FIFO, cupos,
    /// vencimientos, transiciones y DV. Todo queda marcado con <see cref="MARCA"/> para poder limpiarlo
    /// sin tocar datos reales. No se ejecuta solo: lo dispara una acción admin del menú.
    /// </summary>
    public static class SeedDesarrollo
    {
        // Marcadores NO VISIBLES: los nombres de experiencias y clientes son reales (sin prefijo), para
        // que la UI no muestre ninguna etiqueta técnica. Los datos de prueba se identifican por campos
        // que el usuario no ve destacados:
        //   • Experiencia → Descripcion exacta DESC_SEED.
        //   • Cliente      → Email con dominio EMAIL_SEED_DOM (y DNI en rango reservado 90000000+).
        //   • Plan         → nombre exacto PLAN_SEED.
        private const string DESC_SEED      = "Dato de demostración (ExperienceHub).";
        private const string EMAIL_SEED_DOM = "@demo.experiencehub";
        private const string PLAN_SEED      = "Plan Demostración Interno";
        private const string MODULO = "Seed DEV";

        private static readonly Experiencia     _exp    = new Experiencia();
        private static readonly Cliente         _cli    = new Cliente();
        private static readonly Reserva         _res    = new Reserva();
        private static readonly ListaEspera     _esp    = new ListaEspera();
        private static readonly PlanSuscripcion _plan   = new PlanSuscripcion();
        private static readonly Ciudad          _ciu    = new Ciudad();
        private static readonly Categoria       _cat    = new Categoria();
        private static readonly Organizador     _org    = new Organizador();
        private static readonly DAL.Suscripcion _dalSus = new DAL.Suscripcion();

        public static bool YaSembrado()
        {
            foreach (var e in _exp.ObtenerTodos())
                if (string.Equals(e.Descripcion, DESC_SEED, StringComparison.Ordinal)) return true;
            return false;
        }

        public static string Sembrar()
        {
            if (YaSembrado())
                return "Los datos de prueba ya están sembrados. Usá «Limpiar datos de prueba» si querés regenerarlos.";

            // Catálogos: se REUSAN los existentes (no se fabrican a ciegas).
            var ciudades = _ciu.ObtenerTodos();
            var cats     = _cat.ObtenerTodos();
            var orgs     = _org.ObtenerTodos();
            if (ciudades.Count == 0 || cats.Count == 0 || orgs.Count == 0)
                return "Faltan catálogos (Ciudad / Categoría / Organizador). Cargá al menos uno de cada antes de sembrar.";
            int idCiudad = ciudades[0].IdCiudad;
            int idOrg    = orgs[0].IdOrganizador;

            // Plan de prueba generoso para que los clientes puedan reservar sin topes molestos.
            _plan.Alta(new BE.PlanSuscripcion
            {
                Nombre = PLAN_SEED, Precio = 1, Estado = true,
                ReservasMensuales = 50, AnticipacionMaximaDias = 400, AccesoPremium = true, CantidadInvitados = 3
            });
            int idPlan = 0;
            foreach (var p in _plan.ObtenerTodos()) if (p.Nombre == PLAN_SEED) { idPlan = p.IdPlan; break; }
            if (idPlan == 0) return "No se pudo crear el plan de prueba.";

            // Clientes con suscripción vigente hasta fin de 2026, en la MISMA ciudad (para pasar la regla
            // de ciudad al reservar).
            string[] nombres   = { "Nicolás","Valeria","Martín","Sofía","Lucas","Camila","Diego","Julieta","Tomás","Florencia","Mateo","Agustina","Bruno","Carla","Facundo","Rocío","Ignacio","Paula" };
            string[] apellidos = { "Herrera","Soto","Giménez","Rossi","Peralta","Navarro","Ibáñez","Cabrera","Molina","Ferreyra","Acosta","Vega","Ramírez","Luna","Ortiz","Suárez","Benítez","Campos" };
            var vto = new DateTime(2027, 1, 31);
            var cliIds = new List<int>();
            const int totalClientes = 50;   // pool amplio (nombres reales; marca por Email/DNI, no visible)
            for (int i = 0; i < totalClientes; i++)
            {
                // Combo (nombre, apellido) ÚNICO: con N=18, el par (i%N, (i%N + i/N)%N) no se repite para
                // i en 0..(N*N-1) → ningún "mismo cliente" dos veces (antes se duplicaban nombre+apellido).
                int N = nombres.Length;
                var c = new BE.Cliente
                {
                    Nombre = nombres[i % N], Apellido = apellidos[(i % N + i / N) % N],
                    DNI = (90000000 + i).ToString(), Email = "cli" + i + EMAIL_SEED_DOM,   // marcador NO visible
                    FechaNacimiento = new DateTime(1990, 1, 1), IdCiudad = idCiudad
                };
                _cli.Alta(MODULO, c);
                _dalSus.Alta(new BE.Suscripcion
                {
                    IdCliente = c.IdCliente, IdPlan = idPlan, FechaInicio = DateTime.Today,
                    FechaVencimiento = vto, Estado = BE.EstadoSuscripcion.Activa, ReservasConsumidasMes = 0
                });
                cliIds.Add(c.IdCliente);
            }

            int idxCli = 0;   // puntero rotativo de clientes (confirmamos cada reserva → quedan libres)
            int nExp = 0, nRes = 0, nEsp = 0, nOfe = 0;
            var baseFutura = new DateTime(2026, 10, 22);

            // Generador de 55 experiencias espaciadas ~1,75 días desde baseFutura (fechas DISTINTAS →
            // ningún cliente puede solaparse) con nombres/cupos/roles/colas variados; la última cae a
            // fines de enero 2027 (hasta enero).
            string[] baseNom =
            {
                "Masterclass de Alta Cocina","Taller de Vinos Naturales","Escape Room: La Bóveda",
                "Noche de Jazz en Vivo","Tour Gastronómico Urbano","Cata de Cafés de Especialidad",
                "Clase de Cerámica Artesanal","Senderismo Fotográfico","Degustación de Chocolates",
                "Taller de Coctelería","Ópera al Aire Libre","Experiencia Astronómica","Brunch Gourmet",
                "Yoga al Amanecer","Paseo en Velero","Clase de Pintura al Óleo","Teatro Inmersivo",
                "Ruta de Cervezas Artesanales","Taller de Sushi","Concierto Acústico","Catamarán Nocturno",
                "Excursión de Montaña","Taller de Perfumería","Cena a Ciegas","Carrera de Karting"
            };
            int[] horas = { 10, 12, 14, 17, 19, 20, 21 };
            var defs = new List<(string nom, int dias, int hora, int cupo, string rol, int cola)>();
            // Volumen ALTO: 45 experiencias espaciadas ~2 días → de oct/2026 a fin de enero/2027.
            const int totalExp = 45;
            for (int i = 0; i < totalExp; i++)
            {
                string nom = baseNom[i % baseNom.Length] + (i >= baseNom.Length ? " · ed. " + (i / baseNom.Length + 1) : "");
                int cupo = 2 + (i % 3);                       // 2..4 (completo barato)
                string rol; int cola;
                if (i % 6 == 0)      { rol = "oferta";    cola = 4 + (i % 3); }   // ~8 ofertas vigentes, cola 4..6
                else if (i == 4)     { rol = "confirmar"; cola = 4; }             // historial Confirmado
                else if (i == 9)     { rol = "rechazar";  cola = 4; }             // historial Retirado
                else if (i % 8 == 5) { rol = "empty";     cola = 0; }             // algunas vacías
                else                 { rol = "wait";      cola = 4 + (i % 6); }   // la mayoría: cola 4..9
                defs.Add((nom, i * 2, horas[i % horas.Length], cupo, rol, cola));   // ~2 días → hasta ~20/01/2027
            }

            foreach (var d in defs)
            {
                try
                {
                    var exp = new BE.Experiencia
                    {
                        Nombre = d.nom, Descripcion = DESC_SEED,
                        IdCategoria = cats[nExp % cats.Count].IdCategoria, IdCiudad = idCiudad, IdOrganizador = idOrg,
                        Ubicacion = "Sede de prueba", Fecha = baseFutura.AddDays(d.dias).Date,
                        HoraInicio = new TimeSpan(d.hora, 0, 0), DuracionMinutos = 120,
                        CupoMaximo = d.cupo, EdadMinima = 0, Premium = false
                    };
                    _exp.Alta(MODULO, exp);
                    nExp++;
                    int idExp = exp.IdExperiencia;
                    if (d.rol == "empty") continue;

                    // Llenar a completo con reservas CONFIRMADAS de clientes distintos (confirmar libera
                    // al cliente; una Confirmada no bloquea nuevas reservas). Cada asiento es tolerante a fallos.
                    var confirmadas = new List<int>();
                    for (int k = 0; k < d.cupo; k++)
                    {
                        try
                        {
                            int idCli = cliIds[idxCli++ % cliIds.Count];
                            int idR = _res.CrearReserva(MODULO, idCli, idExp, 0); nRes++;
                            var r = _res.ObtenerPorId(idR);
                            if (r != null && r.PuedeConfirmarse()) { _res.Confirmar(MODULO, r); confirmadas.Add(idR); }
                        }
                        catch { /* un asiento puntual puede fallar; se continúa con el resto */ }
                    }

                    // Cola FIFO (clientes distintos a los que llenaron).
                    for (int q = 0; q < d.cola; q++)
                        try { _esp.Ingresar(MODULO, idExp, cliIds[idxCli++ % cliIds.Count]); nEsp++; } catch { }

                    // Oferta: cancelar una reserva confirmada → se ofrece el cupo al primero de la fila.
                    if ((d.rol == "oferta" || d.rol == "confirmar" || d.rol == "rechazar") && confirmadas.Count > 0 && d.cola > 0)
                    {
                        var rc = _res.ObtenerPorId(confirmadas[0]);
                        if (rc != null) _res.Cancelar(MODULO, rc, "Seed: liberar cupo");
                        var oferta = ObtenerOferta(idExp);
                        if (oferta != null)
                        {
                            nOfe++;
                            if (d.rol == "confirmar")
                            {
                                try { int idRnew = _esp.ConfirmarOferta(MODULO, oferta);
                                      var rn = _res.ObtenerPorId(idRnew);
                                      if (rn != null && rn.PuedeConfirmarse()) _res.Confirmar(MODULO, rn); } catch { }
                            }
                            else if (d.rol == "rechazar")
                            {
                                try { _esp.RechazarOferta(MODULO, oferta); } catch { }
                            }
                            // "oferta" → se deja VIGENTE (escenario C)
                        }
                    }

                    // INVARIANTE de coherencia: una experiencia con cola DEBE estar completa (cupo 0) o tener
                    // una oferta vigente (cupo retenido). Si el flujo de oferta no se concretó (p.ej. sin
                    // margen para ofrecer) y quedó cupo libre + cola, se re-completa el cupo → queda completa
                    // + cola (coherente), nunca "cupo libre + gente esperando".
                    if (d.cola > 0)
                    {
                        var expAct = _exp.ObtenerPorId(idExp);
                        bool hayOferta = ObtenerOferta(idExp) != null;
                        int guard = 0;
                        while (!hayOferta && expAct != null && expAct.CupoDisponible > 0 && guard++ < 10)
                        {
                            try
                            {
                                int idCli = cliIds[idxCli++ % cliIds.Count];
                                int idR = _res.CrearReserva(MODULO, idCli, idExp, 0); nRes++;
                                var r = _res.ObtenerPorId(idR);
                                if (r != null && r.PuedeConfirmarse()) _res.Confirmar(MODULO, r);
                            }
                            catch { break; }
                            expAct = _exp.ObtenerPorId(idExp);
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError("[Seed] " + d.nom + ": " + ex.Message); }
            }

            // Dos experiencias finalizadas (para la pestaña Historial). No se les cargan reservas
            // (una experiencia pasada no admite reservar); se marcan Finalizada vía el flujo existente.
            for (int f = 0; f < 2; f++)
            {
                try
                {
                    var exp = new BE.Experiencia
                    {
                        Nombre = "Evento Pasado " + (f + 1), Descripcion = DESC_SEED,
                        IdCategoria = cats[0].IdCategoria, IdCiudad = idCiudad, IdOrganizador = idOrg,
                        Ubicacion = "Sede de prueba", Fecha = DateTime.Today.AddDays(5 + f),
                        HoraInicio = new TimeSpan(19, 0, 0), DuracionMinutos = 120, CupoMaximo = 5, EdadMinima = 0, Premium = false
                    };
                    _exp.Alta(MODULO, exp); nExp++;
                    _exp.CambiarEstado(MODULO, exp, BE.EstadoExperiencia.Finalizada);
                }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError("[Seed pasada] " + ex.Message); }
            }

            return string.Format(
                "Sembrado OK: {0} experiencias, {1} clientes, {2} reservas, {3} inscripciones en espera, {4} oferta(s) generada(s).",
                nExp, cliIds.Count, nRes, nEsp, nOfe);
        }

        private static BE.ListaEspera ObtenerOferta(int idExperiencia)
        {
            foreach (var le in _esp.ObtenerTodas(idExperiencia))
                if (le.Estado == BE.EstadoListaEspera.Ofrecido) return le;
            return null;
        }

        /// <summary>Elimina EXCLUSIVAMENTE los datos marcados [SEED], en orden de FK, y recalcula el DV
        /// de las tablas protegidas afectadas. No toca ningún registro real.</summary>
        public static string Limpiar()
        {
            var acc = DAL.Acceso.GetInstance();
            // Subconjuntos por MARCADORES NO VISIBLES (no por nombre): experiencia por Descripcion,
            // cliente por dominio de Email, plan por nombre interno.
            const string expSub  = "(SELECT IdExperiencia FROM Experiencia WHERE Descripcion = @desc)";
            const string cliSub  = "(SELECT IdCliente FROM Cliente WHERE Email LIKE @mail)";
            const string planSub = "(SELECT IdPlan FROM PlanSuscripcion WHERE Nombre = @plan)";
            const string susSub  = "(SELECT IdSuscripcion FROM Suscripcion WHERE IdCliente IN " + cliSub + " OR IdPlan IN " + planSub + ")";
            const string resSub  = "(SELECT IdReserva FROM Reserva WHERE IdExperiencia IN " + expSub + " OR IdCliente IN " + cliSub + " OR IdSuscripcion IN " + susSub + ")";
            SqlParameter[] P() => new[]
            {
                new SqlParameter("@desc", DESC_SEED),
                new SqlParameter("@mail", "%" + EMAIL_SEED_DOM),
                new SqlParameter("@plan", PLAN_SEED)
            };

            acc.Escribir("UPDATE BitacoraNegocio SET IdCliente = NULL WHERE IdCliente IN " + cliSub, P());
            acc.Escribir("DELETE FROM Calificacion WHERE IdReserva IN " + resSub, P());
            acc.Escribir("DELETE FROM ReservaHistorial WHERE IdReserva IN " + resSub, P());
            acc.Escribir("DELETE FROM ListaEspera WHERE IdExperiencia IN " + expSub + " OR IdCliente IN " + cliSub, P());
            acc.Escribir("DELETE FROM Reserva WHERE IdReserva IN " + resSub, P());
            acc.Escribir("DELETE FROM Contratacion WHERE IdCliente IN " + cliSub + " OR IdPlan IN " + planSub + " OR IdSuscripcion IN " + susSub, P());
            acc.Escribir("DELETE FROM Suscripcion WHERE IdCliente IN " + cliSub + " OR IdPlan IN " + planSub, P());
            acc.Escribir("UPDATE Cliente SET IdPlan = NULL WHERE IdPlan IN " + planSub, P());
            acc.Escribir("DELETE FROM Cliente WHERE Email LIKE @mail", P());
            acc.Escribir("DELETE FROM Experiencia WHERE Descripcion = @desc", P());
            acc.Escribir("DELETE FROM PlanSuscripcion WHERE Nombre = @plan", P());

            var dv = new DAL.DigitoVerificador();
            dv.RecalcularTabla(DAL.Cliente.DV_Tabla,     DAL.Cliente.DV_Pk,     DAL.Cliente.DV_Columnas);
            dv.RecalcularTabla(DAL.Experiencia.DV_Tabla, DAL.Experiencia.DV_Pk, DAL.Experiencia.DV_Columnas);
            dv.RecalcularTabla(DAL.Reserva.DV_Tabla,     DAL.Reserva.DV_Pk,     DAL.Reserva.DV_Columnas);
            return "Datos de prueba eliminados (solo los de demostración).";
        }
    }
}

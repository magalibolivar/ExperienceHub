using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL
{
    /// <summary>
    /// CU-ANA-03 — Análisis de popularidad y ocupación de experiencias.
    /// Reúne datos ya existentes (Experiencia + Reserva + ListaEspera + Calificacion) y arma un
    /// ranking. La agregación vive en el NÚCLEO PURO <see cref="Construir"/> (testeable sin BD).
    /// Sin IA: solo conteos y una regla de segmentación explícita.
    /// </summary>
    public class AnalisisExperiencias
    {
        private readonly Experiencia  _bllExp    = new Experiencia();
        private readonly Reserva      _bllRes    = new Reserva();
        private readonly ListaEspera  _bllEspera = new ListaEspera();
        private readonly Calificacion _bllCalif  = new Calificacion();

        /// <summary>Ocupación &lt; 25% (y sin lista de espera) ⇒ baja demanda.</summary>
        public const double UMBRAL_BAJA = 0.25;
        /// <summary>Ocupación ≥ 80% (o con lista de espera) ⇒ alta demanda.</summary>
        public const double UMBRAL_ALTA = 0.80;

        /// <summary>Arma el ranking leyendo los datos vigentes. Filtro opcional por fecha de la experiencia.</summary>
        public List<BE.AnalisisExperiencia> Ranking(DateTime? desde = null, DateTime? hasta = null)
        {
            var exps     = _bllExp.ObtenerTodos();
            var reservas = _bllRes.ObtenerTodos();

            var enEspera = new Dictionary<int, int>();
            var calif    = new Dictionary<int, double>();
            foreach (var e in exps)
            {
                try { enEspera[e.IdExperiencia] = _bllEspera.ObtenerCola(e.IdExperiencia).Count; } catch { enEspera[e.IdExperiencia] = 0; }
                try { calif[e.IdExperiencia]    = _bllCalif.PromedioPorExperiencia(e.IdExperiencia); } catch { calif[e.IdExperiencia] = 0; }
            }
            return Construir(exps, reservas, enEspera, calif, desde, hasta);
        }

        /// <summary>
        /// NÚCLEO PURO: arma el ranking a partir de datos ya cargados (sin acceso a datos ⇒ testeable).
        /// Reservas = reservas no canceladas (demanda); Ocupación = Reservas / CupoMáximo (acotada a 1).
        /// </summary>
        public static List<BE.AnalisisExperiencia> Construir(
            IList<BE.Experiencia> experiencias, IList<BE.Reserva> reservas,
            IDictionary<int, int> enEspera, IDictionary<int, double> promedioCalif,
            DateTime? desde = null, DateTime? hasta = null)
        {
            var salida = new List<BE.AnalisisExperiencia>();
            if (experiencias == null) return salida;
            reservas = reservas ?? new List<BE.Reserva>();

            foreach (var e in experiencias)
            {
                if (desde.HasValue && e.Fecha.Date < desde.Value.Date) continue;
                if (hasta.HasValue && e.Fecha.Date > hasta.Value.Date) continue;

                int ocupadas = 0, canceladas = 0;
                foreach (var r in reservas)
                {
                    if (r.IdExperiencia != e.IdExperiencia) continue;
                    if (r.Estado == BE.EstadoReserva.Cancelada) canceladas++;
                    else ocupadas++;
                }

                double ocup = e.CupoMaximo > 0 ? Math.Min(1.0, (double)ocupadas / e.CupoMaximo) : 0;
                int    esp  = (enEspera != null && enEspera.ContainsKey(e.IdExperiencia)) ? enEspera[e.IdExperiencia] : 0;
                double cal  = (promedioCalif != null && promedioCalif.ContainsKey(e.IdExperiencia)) ? promedioCalif[e.IdExperiencia] : 0;

                string seg = (ocup >= UMBRAL_ALTA || esp > 0) ? "Alta demanda"
                           : (ocup <  UMBRAL_BAJA)            ? "Baja demanda"
                           :                                    "Normal";

                salida.Add(new BE.AnalisisExperiencia
                {
                    IdExperiencia = e.IdExperiencia,
                    Experiencia   = e.Nombre,
                    Categoria     = e.NombreCategoria,
                    Ciudad        = e.NombreCiudad,
                    Fecha         = e.Fecha,
                    CupoMaximo    = e.CupoMaximo,
                    Reservas      = ocupadas,
                    Canceladas    = canceladas,
                    EnEspera      = esp,
                    Ocupacion     = ocup,
                    PromedioCalif = cal,
                    Segmento      = seg
                });
            }

            return salida
                .OrderByDescending(a => a.Reservas)
                .ThenByDescending(a => a.Ocupacion)
                .ToList();
        }
    }
}

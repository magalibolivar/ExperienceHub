using System;
using System.Collections.Generic;

namespace BE
{
    /// <summary>
    /// PN03 (A03 — Serialización) — "Vista de análisis" guardable y recuperable del tablero de
    /// planificación: período, filtros (ciudad/categoría/organizador), umbrales del motor de
    /// recomendaciones y qué visualizaciones mostrar. Es un OBJETO DE DOMINIO que se serializa a
    /// XML y se vuelve a deserializar tal cual para reabrir el informe (ida y vuelta, no solo salida).
    ///
    /// NO guarda datos sensibles ni resultados: solo parámetros de presentación. POCO puro para que
    /// el DataContractSerializer lo serialice sin acoplar BE a System.Runtime.Serialization.
    /// </summary>
    public class ConfiguracionInforme
    {
        /// <summary>Nombre con el que el usuario guarda la vista (ej. "Trimestre premium - Palermo").</summary>
        public string Nombre { get; set; } = "";

        public DateTime PeriodoDesde { get; set; }
        public DateTime PeriodoHasta { get; set; }

        public int? FiltroCiudad      { get; set; }
        public int? FiltroCategoria   { get; set; }
        public int? FiltroOrganizador { get; set; }

        /// <summary>Umbrales del motor (se reutiliza el mismo tipo que BLL.Analitica/MotorRecomendaciones).</summary>
        public UmbralesRecomendacion Umbrales { get; set; } = new UmbralesRecomendacion();

        /// <summary>Claves de los gráficos/indicadores elegidos para mostrar en el tablero y el PDF.</summary>
        public List<string> GraficosVisibles { get; set; } = new List<string>();

        /// <summary>Metadatos de trazabilidad (cuándo se guardó y con qué versión del esquema).</summary>
        public DateTime GuardadoEn { get; set; }
        public string   Version    { get; set; } = "1.0";
    }
}

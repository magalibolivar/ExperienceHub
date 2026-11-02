namespace BLL
{
    /// <summary>
    /// PN03 — Clasifica la variación de un indicador del comparativo Antes/Después (KpiSnapshot) según
    /// si su dirección de mejora es "subir" o "bajar". Lógica de NEGOCIO pura (sin WinForms), extraída
    /// de la capa de presentación para que sea testeable sin depender de GUI: antes de esta extracción,
    /// una variación se clasificaba solo por el signo crudo del delta, lo que interpretaba "subió la
    /// lista de espera" (peor) como una mejora. No todos los indicadores tienen una dirección clara
    /// (ej. CancelacionesATiempo no es buena ni mala en sí misma): esos se clasifican como neutros.
    /// </summary>
    public static class EvaluacionKpi
    {
        public enum Resultado { Mejoro, Empeoro, SinCambio, Neutro }

        /// <summary>true = subir es mejor (Altas/Renovaciones/Ingresos); false = bajar es mejor
        /// (demanda insatisfecha, cancelaciones tardías, inasistencias); null = sin dirección clara.</summary>
        public static bool? MejoraSiSube(string claveSnapshot)
        {
            switch (claveSnapshot)
            {
                case "Altas":
                case "Renovaciones":
                case "IngresosPlanes":
                    return true;
                case "EnEspera":
                case "OfertasVencidas":
                case "CancelacionesTardias":
                case "Inasistencias":
                    return false;
                default:   // CancelacionesATiempo y claves no reconocidas: no se opina.
                    return null;
            }
        }

        /// <summary>Clasifica una variación (Después − Antes) según la dirección de mejora del indicador.</summary>
        public static Resultado Clasificar(string claveSnapshot, decimal delta)
        {
            if (delta == 0) return Resultado.SinCambio;
            var mejoraSiSube = MejoraSiSube(claveSnapshot);
            if (mejoraSiSube == null) return Resultado.Neutro;
            bool mejoro = mejoraSiSube.Value ? delta > 0 : delta < 0;
            return mejoro ? Resultado.Mejoro : Resultado.Empeoro;
        }
    }
}

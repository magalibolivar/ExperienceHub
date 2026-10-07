namespace BE
{
    /// <summary>
    /// Nivel de fidelidad de un cliente según cuántas experiencias efectivamente vivió
    /// (reservas en estado Asistió). Da una lectura "comercial" del cliente, más allá del ABM.
    /// </summary>
    public enum NivelFidelidad
    {
        Nuevo = 0,       // sin asistencias
        Ocasional = 1,   // 1-2
        Frecuente = 2,   // 3-5
        VIP = 3          // 6+
    }

    /// <summary>Clasificador PURO de fidelidad (sin BD): umbrales documentados en un solo lugar.</summary>
    public static class Fidelidad
    {
        public const int UMBRAL_OCASIONAL = 1;   // >= 1 asistencia
        public const int UMBRAL_FRECUENTE = 3;   // >= 3
        public const int UMBRAL_VIP       = 6;   // >= 6

        public static NivelFidelidad Clasificar(int asistencias)
        {
            if (asistencias >= UMBRAL_VIP)       return NivelFidelidad.VIP;
            if (asistencias >= UMBRAL_FRECUENTE) return NivelFidelidad.Frecuente;
            if (asistencias >= UMBRAL_OCASIONAL) return NivelFidelidad.Ocasional;
            return NivelFidelidad.Nuevo;
        }

        /// <summary>Etiqueta amigable para mostrar en la UI.</summary>
        public static string Etiqueta(NivelFidelidad nivel)
        {
            switch (nivel)
            {
                case NivelFidelidad.VIP:       return "Cliente VIP";
                case NivelFidelidad.Frecuente: return "Cliente frecuente";
                case NivelFidelidad.Ocasional: return "Cliente ocasional";
                default:                       return "Cliente nuevo";
            }
        }
    }
}

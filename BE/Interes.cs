namespace BE
{
    /// <summary>
    /// Entidad Interés — etiqueta de preferencia que un cliente puede tener asociada
    /// (se relaciona N:M con Cliente vía [ClienteInteres]). Alimenta las recomendaciones.
    /// Mapea la tabla [Interes].
    /// </summary>
    public class Interes
    {
        public int    IdInteres { get; set; }
        public string Nombre    { get; set; }
    }
}

namespace BE
{
    /// <summary>Entidad Ciudad donde se ofrecen experiencias. Mapea la tabla [Ciudad].</summary>
    public class Ciudad
    {
        public int    IdCiudad  { get; set; }
        public string Nombre    { get; set; }
        public string Provincia { get; set; }

        /// <summary>Baja lógica: false = inactiva.</summary>
        public bool   Estado    { get; set; } = true;

        public string NombreCompleto =>
            string.IsNullOrWhiteSpace(Provincia) ? Nombre : $"{Nombre}, {Provincia}";
    }
}

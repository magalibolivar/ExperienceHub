namespace BE
{
    /// <summary>Entidad Organizador. Empresa o persona que ofrece experiencias. Mapea la tabla [Organizador].</summary>
    public class Organizador
    {
        public int    IdOrganizador { get; set; }
        public string Nombre        { get; set; }
        public string Contacto      { get; set; }
        public string Telefono      { get; set; }
        public string Mail          { get; set; }

        /// <summary>Baja lógica: false = inactivo.</summary>
        public bool   Estado        { get; set; } = true;
    }
}

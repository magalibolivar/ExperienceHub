namespace BE
{
    /// <summary>
    /// Entidad Categoría de experiencia (Taller, Cata, Teatro, Gastronomía, …).
    /// Antes era un simple campo de texto en Prenda; ahora es un catálogo administrable.
    /// Mapea la tabla [Categoria].
    /// </summary>
    public class Categoria
    {
        public int    IdCategoria { get; set; }
        public string Nombre      { get; set; }
        public string Descripcion { get; set; }

        /// <summary>Baja lógica: false = inactiva.</summary>
        public bool   Estado      { get; set; } = true;
    }
}

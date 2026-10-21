using System;

namespace BE
{
    /// <summary>
    /// Resumen de actividad de la lista de espera por experiencia (solo lectura, para la vista
    /// orientada a tareas de PN02): cuántos clientes están Esperando y cuántas ofertas hay Ofrecidas.
    /// No persiste; lo arma una consulta SELECT agregada en DAL.
    /// </summary>
    public class ResumenListaEspera
    {
        public int       IdExperiencia        { get; set; }
        public string    NombreExperiencia    { get; set; }
        public DateTime? FechaHoraExperiencia { get; set; }
        public int       EnEspera             { get; set; }   // entradas en estado Esperando
        public int       Ofrecidas            { get; set; }   // entradas en estado Ofrecido (oferta vigente)

        public int Activas => EnEspera + Ofrecidas;
    }
}

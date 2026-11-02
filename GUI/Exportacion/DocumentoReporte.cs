using System.Collections.Generic;
using System.Drawing;

namespace GUI.Exportacion
{
    /// <summary>
    /// Modelo de DOCUMENTO estructurado por bloques para informes profesionales en PDF (sin dependencias
    /// externas: lo renderiza <see cref="ExportadorPdfDocumento"/> con GDI+/PrintDocument). Reemplaza al
    /// "texto plano monoespaciado" para documentos con jerarquía: títulos, párrafos, tablas reales,
    /// pares clave/valor, etiquetas de estado y un gráfico de variación. Reutilizable por cualquier informe.
    /// </summary>
    public sealed class DocumentoReporte
    {
        public string NombreArchivo { get; set; }          // PrintDocument.DocumentName
        public string Institucion { get; set; } = "ExperienceHub";
        public string Modulo { get; set; }                 // ej. "PN03 – Planificación"
        public string Titulo { get; set; }                 // título del informe
        public string Subtitulo { get; set; }              // ej. "#7 · Verano CABA"
        public List<Bloque> Bloques { get; } = new List<Bloque>();

        // Helpers fluidos para armar el documento legible.
        public DocumentoReporte Seccion(string texto) { Bloques.Add(new BloqueSeccion { Texto = texto }); return this; }
        public DocumentoReporte Parrafo(string texto, bool enfasis = false) { Bloques.Add(new BloqueParrafo { Texto = texto, Enfasis = enfasis }); return this; }
        public DocumentoReporte ClaveValor(List<(string, string)> items) { Bloques.Add(new BloqueClaveValor { Items = items }); return this; }
        public DocumentoReporte Tabla(string[] headers, List<string[]> filas, float[] pesos, bool[] alinearDerecha) { Bloques.Add(new BloqueTabla { Headers = headers, Filas = filas, Pesos = pesos, AlinearDerecha = alinearDerecha }); return this; }
        public DocumentoReporte Etiquetas(List<(string, Color)> items) { Bloques.Add(new BloqueEtiquetas { Items = items }); return this; }
        public DocumentoReporte BarrasVariacion(string titulo, List<(string, decimal)> datos) { Bloques.Add(new BloqueBarrasVariacion { Titulo = titulo, Datos = datos }); return this; }
        public DocumentoReporte Espacio(int alto = 8) { Bloques.Add(new BloqueEspacio { Alto = alto }); return this; }
        public DocumentoReporte Nota(string texto) { Bloques.Add(new BloqueParrafo { Texto = texto, Nota = true }); return this; }
        /// <summary>Texto preformateado (p. ej. reportes con alineación manual por espacios) — se
        /// imprime línea por línea en fuente monoespaciada, SIN re-envolver ni recortar ninguna línea.</summary>
        public DocumentoReporte TextoMono(string texto) { Bloques.Add(new BloqueTextoMono { Texto = texto }); return this; }

        public abstract class Bloque { }
        public sealed class BloqueSeccion : Bloque { public string Texto; }
        public sealed class BloqueParrafo : Bloque { public string Texto; public bool Enfasis; public bool Nota; }
        public sealed class BloqueClaveValor : Bloque { public List<(string Clave, string Valor)> Items; }
        public sealed class BloqueTabla : Bloque { public string[] Headers; public List<string[]> Filas; public float[] Pesos; public bool[] AlinearDerecha; }
        public sealed class BloqueEtiquetas : Bloque { public List<(string Texto, Color Color)> Items; }
        public sealed class BloqueBarrasVariacion : Bloque { public string Titulo; public List<(string Etiqueta, decimal Pct)> Datos; }
        public sealed class BloqueEspacio : Bloque { public int Alto; }
        public sealed class BloqueTextoMono : Bloque { public string Texto; }
    }
}

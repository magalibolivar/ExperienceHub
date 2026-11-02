using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace Servicios.Serializacion
{
    /// <summary>
    /// A03 — Serialización / deserialización de estado de objetos (PN03). Persiste una
    /// <see cref="BE.ConfiguracionInforme"/> ("vista de análisis" del tablero) a XML con
    /// <see cref="DataContractSerializer"/> y la vuelve a cargar tal cual: es ida y vuelta real
    /// (serializar + deserializar + volver a usar), no una exportación de una sola dirección.
    ///
    /// Vive en la capa de Servicios (transversal), igual que <see cref="Exportacion.SerializadorCsv"/>:
    /// la GUI solo pide guardar/abrir una vista y recibe el objeto listo. Se eligió XML
    /// (DataContractSerializer) por ser legible, estable y sin BinaryFormatter (obsoleto/inseguro).
    /// Las vistas se guardan bajo %LOCALAPPDATA%\ExperienceHub\Informes\*.xml (ruta portable).
    /// </summary>
    public static class SerializadorInformes
    {
        private const string Extension = ".xml";

        private static readonly DataContractSerializer _serializer =
            new DataContractSerializer(typeof(BE.ConfiguracionInforme));

        /// <summary>Carpeta portable donde viven las vistas de análisis (se crea si no existe).</summary>
        public static string CarpetaInformes()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ExperienceHub", "Informes");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ── Round-trip en memoria (base de todo lo demás; testeable sin tocar disco) ──────────────

        /// <summary>Serializa la configuración a una cadena XML con sangría.</summary>
        public static string Serializar(BE.ConfiguracionInforme cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
            using (var w = XmlWriter.Create(sb, settings))
                _serializer.WriteObject(w, cfg);
            return sb.ToString();
        }

        /// <summary>Reconstruye la configuración desde una cadena XML. Lanza AppException clara si el
        /// contenido está dañado o es incompatible (no revienta con el error técnico crudo).</summary>
        public static BE.ConfiguracionInforme Deserializar(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new BE.AppException("err.informe.vacio", "El informe está vacío o no es válido.");
            try
            {
                using (var r = XmlReader.Create(new StringReader(xml)))
                    return (BE.ConfiguracionInforme)_serializer.ReadObject(r);
            }
            catch (Exception ex) when (ex is SerializationException || ex is XmlException || ex is InvalidCastException)
            {
                throw new BE.AppException("err.informe.corrupto",
                    "No se pudo leer la vista de análisis: el archivo está dañado o es de una versión incompatible.");
            }
        }

        // ── Persistencia a archivo ─────────────────────────────────────────────────────────────

        /// <summary>Guarda la vista en CarpetaInformes() con el nombre dado (saneado). Sella GuardadoEn.
        /// Devuelve la ruta del archivo escrito.</summary>
        public static string Guardar(BE.ConfiguracionInforme cfg, string nombreArchivo = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            cfg.GuardadoEn = DateTime.Now;
            string nombre = SanearNombre(string.IsNullOrWhiteSpace(nombreArchivo) ? cfg.Nombre : nombreArchivo);
            if (nombre.Length == 0) nombre = "informe";
            string ruta = Path.Combine(CarpetaInformes(), nombre + Extension);
            File.WriteAllText(ruta, Serializar(cfg), Encoding.UTF8);
            return ruta;
        }

        /// <summary>Carga una vista desde una ruta de archivo. Lanza AppException clara si no existe o
        /// está dañada.</summary>
        public static BE.ConfiguracionInforme Cargar(string ruta)
        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta))
                throw new BE.AppException("err.informe.no_existe", "No se encontró la vista de análisis solicitada.");
            return Deserializar(File.ReadAllText(ruta, Encoding.UTF8));
        }

        /// <summary>Lista las vistas guardadas (nombre sin extensión) en orden alfabético.</summary>
        public static List<string> Listar()
        {
            var lista = new List<string>();
            try
            {
                foreach (var f in Directory.GetFiles(CarpetaInformes(), "*" + Extension))
                    lista.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch { /* carpeta inaccesible → lista vacía */ }
            lista.Sort(StringComparer.CurrentCultureIgnoreCase);
            return lista;
        }

        /// <summary>Elimina una vista guardada por nombre (sin extensión). No falla si no existe.</summary>
        public static void Eliminar(string nombre)
        {
            try
            {
                string ruta = Path.Combine(CarpetaInformes(), SanearNombre(nombre) + Extension);
                if (File.Exists(ruta)) File.Delete(ruta);
            }
            catch { /* best-effort */ }
        }

        // Quita los caracteres inválidos de nombre de archivo (el nombre lo escribe el usuario).
        private static string SanearNombre(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Seguridad
{
    /// <summary>
    /// E1 — Diagnóstico de cifrado de SOLO LECTURA.
    ///
    /// No escribe archivos, no toca la base de datos, no regenera claves y NO expone DNI ni
    /// claves en claro. Su objetivo es clasificar un valor almacenado (p.ej. la columna DNI)
    /// y permitir IDENTIFICAR por huella cuáles de las claves disponibles descifran cada dato,
    /// antes de decidir cualquier migración. Es puro y determinístico (salvo la lectura de
    /// archivos de clave en <see cref="RecuperarClave"/>, que es solo lectura).
    /// </summary>
    public static class DiagnosticoCifrado
    {
        public enum EstadoDni
        {
            /// <summary>No tiene forma de blob AES → la app lo muestra tal cual (dato legible, legacy/plano).</summary>
            TextoPlano,
            /// <summary>Blob AES que ALGUNA clave disponible descifra correctamente.</summary>
            CifradoDescifrable,
            /// <summary>Blob AES que NINGUNA clave disponible pudo descifrar (candidato a migración/re-ingreso).</summary>
            CifradoNoDescifrable,
            /// <summary>Valor vacío o de formato desconocido.</summary>
            Invalido
        }

        /// <summary>
        /// Clasifica un valor contra el conjunto de claves disponibles. Puro, sin efectos secundarios.
        /// Un valor se considera "cifrado" solo si tiene la FORMA de un blob del <see cref="Encriptador"/>
        /// (IV[16] + texto cifrado múltiplo de 16, al menos un bloque). Caso contrario es texto plano/legacy.
        /// </summary>
        public static EstadoDni Clasificar(string valor, IReadOnlyList<byte[]> clavesDisponibles)
        {
            if (string.IsNullOrEmpty(valor)) return EstadoDni.Invalido;

            byte[] blob;
            try { blob = Convert.FromBase64String(valor); }
            catch (FormatException) { return EstadoDni.TextoPlano; }

            // Forma de un blob del Encriptador: IV[16] + cipher (múltiplo de 16, ≥ 1 bloque de 16).
            if (blob.Length < 32 || (blob.Length - 16) % 16 != 0)
                return EstadoDni.TextoPlano;

            if (clavesDisponibles != null)
                foreach (var clave in clavesDisponibles)
                    if (clave != null && clave.Length == 16 && IntentarDescifrar(blob, clave))
                        return EstadoDni.CifradoDescifrable;

            return EstadoDni.CifradoNoDescifrable;
        }

        // Intenta descifrar el blob con una clave. true = descifra (padding PKCS7 válido).
        private static bool IntentarDescifrar(byte[] blob, byte[] clave)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = clave;
                    var iv = new byte[16];
                    Array.Copy(blob, 0, iv, 0, 16);
                    aes.IV = iv;
                    using (var dec = aes.CreateDecryptor())
                        dec.TransformFinalBlock(blob, 16, blob.Length - 16);
                }
                return true;
            }
            catch (CryptographicException) { return false; }
        }

        /// <summary>
        /// Huella corta y NO reversible de una clave (prefijo de SHA-256), para identificar la
        /// correspondencia clave↔datos en informes SIN exponer la clave.
        /// </summary>
        public static string Huella(byte[] clave)
        {
            if (clave == null) return "(null)";
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(clave);
                var sb = new StringBuilder();
                for (int i = 0; i < 6 && i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Recupera (SOLO LECTURA, sin regenerar ni reescribir) la clave de un archivo key.dat.
        /// Devuelve null si el archivo no existe, no es Base64 o no puede desprotegerse con DPAPI
        /// del usuario actual. Soporta el formato legacy (16 bytes planos).
        /// </summary>
        public static byte[] RecuperarClave(string rutaKeyDat)
        {
            if (string.IsNullOrEmpty(rutaKeyDat) || !File.Exists(rutaKeyDat)) return null;

            byte[] bytes;
            try { bytes = Convert.FromBase64String(File.ReadAllText(rutaKeyDat).Trim()); }
            catch (FormatException) { return null; }

            try
            {
                byte[] clave = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                if (clave.Length == 16) return clave;
            }
            catch (CryptographicException) { /* no recuperable por DPAPI → se prueba legacy */ }

            return bytes.Length == 16 ? bytes : null; // legacy plano de 16 bytes, o irrecuperable
        }
    }
}

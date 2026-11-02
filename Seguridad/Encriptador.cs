using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Seguridad
{
    /// <summary>
    /// Centraliza la criptografia del sistema.
    /// PBKDF2-SHA256 para contrasenas (unidireccional) y AES-128-CBC para datos sensibles (reversible).
    /// </summary>
    public static class Encriptador
    {
        // ── PBKDF2-SHA256 — hash unidireccional para contrasenas ──────────────

        private const int SaltSize   = 16;
        private const int HashSize   = 32;
        private const int Iterations = 100000;

        /// <summary>
        /// Genera un hash de la contrasena para guardar en BD.
        /// Formato: Base64( Salt[16] + Hash[32] ).
        /// </summary>
        public static string Hash(string contrasena)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(
                contrasena, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(HashSize);

                byte[] hashBytes = new byte[SaltSize + HashSize];
                Array.Copy(salt, 0, hashBytes, 0,        SaltSize);
                Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

                return ConvertirBase64(hashBytes);
            }
        }

        /// <summary>
        /// Verifica si la contrasena ingresada coincide con el hash almacenado en BD.
        /// Extrae el Salt, rehashea y compara byte a byte.
        /// </summary>
        public static bool VerificarContrasena(string contrasenaIngresada, string hashAlmacenado)
        {
            byte[] hashBytes = Convert.FromBase64String(hashAlmacenado);

            byte[] salt = new byte[SaltSize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);

            using (var pbkdf2 = new Rfc2898DeriveBytes(
                contrasenaIngresada, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hashCalculado = pbkdf2.GetBytes(HashSize);

                // Comparación en TIEMPO CONSTANTE: recorre siempre los 32 bytes y acumula
                // las diferencias con XOR, en vez de cortar en el primer byte distinto. Así
                // el tiempo de respuesta no filtra cuántos bytes coincidieron (canal lateral).
                int diferencia = 0;
                for (int i = 0; i < HashSize; i++)
                    diferencia |= hashBytes[i + SaltSize] ^ hashCalculado[i];
                return diferencia == 0;
            }
        }

        // Hash "señuelo" precalculado una sola vez. Se usa para verificar contra un hash
        // VÁLIDO cuando el usuario no existe, de modo que el login consuma el mismo tiempo
        // de PBKDF2 que con un usuario real y no se pueda enumerar usuarios por temporización.
        private static readonly string _hashSenuelo = Hash("\0senuelo-sin-usuario\0");

        /// <summary>
        /// Ejecuta una verificación PBKDF2 contra el hash señuelo. Siempre devuelve false;
        /// su único objetivo es igualar el costo temporal del camino "usuario inexistente".
        /// </summary>
        public static bool VerificacionSenuelo(string contrasena)
        {
            return VerificarContrasena(contrasena, _hashSenuelo);
        }

        // ── Validacion de requisitos de contraseña ────────────────────────────

        // Requisitos: minimo 8 caracteres, al menos 1 numero y 1 caracter especial.
        public static (bool valida, string mensaje) ValidarContrasena(string contrasena)
        {
            if (string.IsNullOrWhiteSpace(contrasena) || contrasena.Length < 8)
                return (false, "La contrasena debe tener al menos 8 caracteres.");

            bool tieneNumero   = false;
            bool tieneEspecial = false;
            const string especiales = "!@#$%^&*()_+-=[]{}|;:',.<>?/";

            foreach (char c in contrasena)
            {
                if (char.IsDigit(c))            tieneNumero   = true;
                if (especiales.IndexOf(c) >= 0) tieneEspecial = true;
            }

            if (!tieneNumero)
                return (false, "La contrasena debe contener al menos un numero.");
            if (!tieneEspecial)
                return (false, "La contrasena debe contener al menos un caracter especial (!@#$%...).");

            return (true, string.Empty);
        }

        // ── AES-128-CBC — cifrado reversible para datos sensibles (ej: DNI) ───

        // Clave AES cargada (o generada SOLO la primera vez) desde key.dat.
        // El archivo nunca se sube al repositorio (.gitignore).
        private static readonly byte[] _claveAES = CargarClave();

        /// <summary>
        /// Cifra un texto con AES-128-CBC usando un IV aleatorio por operacion.
        /// Formato: Base64( IV[16] + CipherText ).
        /// </summary>
        public static string Encriptar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return texto;

            using (var aes = Aes.Create())
            {
                aes.KeySize = 128;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key     = _claveAES;
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] textoBytes = Encoding.UTF8.GetBytes(texto);
                    byte[] cifrado    = encryptor.TransformFinalBlock(textoBytes, 0, textoBytes.Length);

                    byte[] resultado = new byte[iv.Length + cifrado.Length];
                    Array.Copy(iv,      0, resultado, 0,         iv.Length);
                    Array.Copy(cifrado, 0, resultado, iv.Length, cifrado.Length);

                    return ConvertirBase64(resultado);
                }
            }
        }

        /// <summary>Descifra un valor cifrado con AES-128-CBC.</summary>
        public static string Desencriptar(string cifrado)
        {
            if (string.IsNullOrEmpty(cifrado)) return cifrado;

            byte[] datos = Convert.FromBase64String(cifrado);

            // Un blob AES válido es IV[16] + al menos un bloque de cifrado. Si el dato es más corto
            // (p.ej. un DNI en texto plano que resulta ser Base64 válido), NO es nuestro cifrado:
            // se trata como formato inválido para que TryDesencriptar devuelva el valor original.
            if (datos.Length < 16)
                throw new FormatException("Longitud insuficiente para un valor cifrado con AES.");

            byte[] iv           = new byte[16];
            byte[] textoCifrado = new byte[datos.Length - 16];
            Array.Copy(datos, 0,  iv,           0, 16);
            Array.Copy(datos, 16, textoCifrado,  0, textoCifrado.Length);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 128;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key     = _claveAES;
                aes.IV      = iv;

                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] textoBytes = decryptor.TransformFinalBlock(textoCifrado, 0, textoCifrado.Length);
                    return Encoding.UTF8.GetString(textoBytes);
                }
            }
        }

        /// <summary>
        /// Intenta desencriptar. Si falla (dato en texto plano o formato invalido),
        /// devuelve el valor original sin modificar.
        /// Solo captura excepciones criptográficas y de formato, no errores del sistema.
        /// </summary>
        public static string TryDesencriptar(string valor)
        {
            try { return Desencriptar(valor); }
            catch (CryptographicException) { return valor; }
            catch (FormatException)        { return valor; }
            catch (ArgumentException)      { return valor; }
            catch (OverflowException)      { return valor; }   // dato corto / no cifrado (defensa extra)
        }

        // Nombre del AMBIENTE (Desarrollo/Testing/…), leído de la config del host (App.config del
        // ejecutable). Cada ambiente usa su PROPIA clave → no se comparten claves entre aplicaciones
        // conectadas a bases distintas. Default seguro: "Desarrollo".
        private static string Ambiente()
        {
            try
            {
                string a = System.Configuration.ConfigurationManager.AppSettings["ExperienceHub.Ambiente"];
                return string.IsNullOrWhiteSpace(a) ? "Desarrollo" : a.Trim();
            }
            catch { return "Desarrollo"; }
        }

        /// <summary>
        /// Resuelve la ruta del key.dat SIN mover ni crear nada todavía. Estrategia segura de migración:
        ///   1) Si ya existe la clave PERSISTENTE (%APPDATA%\ExperienceHub\{Ambiente}\key.dat) → usarla.
        ///   2) Si no, pero existe una clave LEGACY junto al ejecutable (bin\key.dat) → usarla EN SU LUGAR
        ///      (no se copia ni se borra; la migración de ruta es un paso explícito y autorizado aparte).
        ///   3) Si no hay ninguna → se usará la ruta persistente (primer arranque real crea la clave ahí).
        /// La ubicación persistente sobrevive a rebuilds y limpiezas de bin; nunca es una ruta absoluta
        /// de una máquina concreta (se deriva de %APPDATA% del usuario).
        /// </summary>
        public static string RutaClave()
        {
            string persistente = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ExperienceHub", Ambiente(), "key.dat");
            if (File.Exists(persistente)) return persistente;

            string legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "key.dat");
            if (File.Exists(legacy)) return legacy;

            return persistente;
        }

        // Carga la clave AES, PROTEGIDA con DPAPI (ProtectedData, ámbito del usuario actual).
        // REGLA DE SEGURIDAD: nunca regenera ni sobrescribe una clave EXISTENTE que no se pueda
        // recuperar. Si el archivo existe pero no descifra (p.ej. otro usuario/máquina), LANZA para
        // que el problema se note, en vez de generar una clave nueva y volver irrecuperables los DNI
        // ya cifrados. Solo crea una clave nueva cuando NO existe ningún archivo (primer arranque).
        private static byte[] CargarClave()
        {
            string ruta = RutaClave();

            if (File.Exists(ruta))
            {
                byte[] bytes;
                try { bytes = Convert.FromBase64String(File.ReadAllText(ruta).Trim()); }
                catch (Exception ex)
                {
                    throw new CryptographicException(
                        "El archivo de clave '" + ruta + "' existe pero no es Base64 válido. " +
                        "NO se sobrescribe para no perder datos cifrados. Restaurá la clave correcta.", ex);
                }

                // 1) Caso normal: blob DPAPI del usuario actual → desproteger.
                try
                {
                    byte[] clave = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                    if (clave.Length == 16) return clave;
                }
                catch (CryptographicException ex)
                {
                    System.Diagnostics.Trace.TraceError("[Encriptador] key.dat no recuperable por DPAPI del usuario actual: " + ex.Message);
                }

                // 2) Legacy: clave plana de 16 bytes → usarla EN MEMORIA (no se reescribe el archivo).
                if (bytes.Length == 16) return bytes;

                // 3) Existe pero NO se pudo recuperar → NO regenerar, NO sobrescribir: avisar.
                throw new CryptographicException(
                    "El archivo de clave '" + ruta + "' existe pero no pudo descifrarse con DPAPI del " +
                    "usuario actual (posible cambio de usuario o de equipo). NO se regenera la clave para " +
                    "no perder el acceso a los datos cifrados. Restaurá la clave correcta o migrá los datos.");
            }

            // No existe ningún archivo de clave → primer arranque legítimo: crear una nueva.
            byte[] nueva = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(nueva);

            GuardarClaveProtegida(ruta, nueva);
            return nueva;
        }

        // Persiste la clave AES protegida con DPAPI (Base64 del blob protegido). Crea el directorio
        // destino si no existe. Solo se invoca al CREAR una clave nueva (primer arranque).
        private static void GuardarClaveProtegida(string ruta, byte[] clave)
        {
            string dir = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            byte[] protegida = ProtectedData.Protect(clave, null, DataProtectionScope.CurrentUser);
            File.WriteAllText(ruta, Convert.ToBase64String(protegida));
        }

        /// <summary>Convierte un array de bytes a Base64.</summary>
        public static string ConvertirBase64(byte[] data)
        {
            return Convert.ToBase64String(data);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Seguridad;

namespace Tests
{
    /// <summary>E1 — Pruebas del clasificador de cifrado de solo lectura (sin BD ni claves del host).</summary>
    [TestClass]
    public class DiagnosticoCifradoTests
    {
        // Genera un blob con el MISMO formato del Encriptador: Base64( IV[16] + cipher ), con una clave dada.
        private static string CifrarCon(byte[] clave, string texto)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 128; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                aes.Key = clave; aes.GenerateIV();
                byte[] iv = aes.IV;
                using (var enc = aes.CreateEncryptor())
                {
                    byte[] p = Encoding.UTF8.GetBytes(texto);
                    byte[] c = enc.TransformFinalBlock(p, 0, p.Length);
                    byte[] res = new byte[iv.Length + c.Length];
                    Array.Copy(iv, 0, res, 0, iv.Length);
                    Array.Copy(c, 0, res, iv.Length, c.Length);
                    return Convert.ToBase64String(res);
                }
            }
        }

        private static byte[] ClaveAleatoria()
        {
            byte[] k = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(k);
            return k;
        }

        [TestMethod]
        public void Clasificar_DniNumericoPlano_EsTextoPlano()
        {
            Assert.AreEqual(DiagnosticoCifrado.EstadoDni.TextoPlano,
                DiagnosticoCifrado.Clasificar("34555666", new List<byte[]>()));
        }

        [TestMethod]
        public void Clasificar_ValorNoBase64_EsTextoPlano()
        {
            Assert.AreEqual(DiagnosticoCifrado.EstadoDni.TextoPlano,
                DiagnosticoCifrado.Clasificar("DNI-sin-formato", new List<byte[]>()));
        }

        [TestMethod]
        public void Clasificar_CifradoConClaveDisponible_EsDescifrable()
        {
            byte[] clave = ClaveAleatoria();
            string blob = CifrarCon(clave, "20123456");
            Assert.AreEqual(DiagnosticoCifrado.EstadoDni.CifradoDescifrable,
                DiagnosticoCifrado.Clasificar(blob, new List<byte[]> { clave }));
        }

        [TestMethod]
        public void Clasificar_CifradoConOtraClave_NoEsDescifrable()
        {
            byte[] claveReal = ClaveAleatoria();
            byte[] claveAjena = ClaveAleatoria();
            string blob = CifrarCon(claveReal, "20123456");
            Assert.AreEqual(DiagnosticoCifrado.EstadoDni.CifradoNoDescifrable,
                DiagnosticoCifrado.Clasificar(blob, new List<byte[]> { claveAjena }));
        }

        [TestMethod]
        public void Clasificar_ProbandoVariasClaves_EncuentraLaCorrecta()
        {
            byte[] claveReal = ClaveAleatoria();
            string blob = CifrarCon(claveReal, "20123456");
            var disponibles = new List<byte[]> { ClaveAleatoria(), claveReal, ClaveAleatoria() };
            Assert.AreEqual(DiagnosticoCifrado.EstadoDni.CifradoDescifrable,
                DiagnosticoCifrado.Clasificar(blob, disponibles));
        }

        [TestMethod]
        public void Clasificar_ValorNulo_EsInvalido()
        {
            Assert.AreEqual(DiagnosticoCifrado.EstadoDni.Invalido,
                DiagnosticoCifrado.Clasificar(null, new List<byte[]>()));
        }

        [TestMethod]
        public void Huella_MismaClaveMismaHuella_ClavesDistintasDistintaHuella()
        {
            byte[] a = ClaveAleatoria();
            byte[] b = ClaveAleatoria();
            Assert.AreEqual(DiagnosticoCifrado.Huella(a), DiagnosticoCifrado.Huella((byte[])a.Clone()));
            Assert.AreNotEqual(DiagnosticoCifrado.Huella(a), DiagnosticoCifrado.Huella(b));
        }

        [TestMethod]
        public void RecuperarClave_ArchivoInexistente_DevuelveNull()
        {
            Assert.IsNull(DiagnosticoCifrado.RecuperarClave(@"Z:\no\existe\key.dat"));
        }
    }
}

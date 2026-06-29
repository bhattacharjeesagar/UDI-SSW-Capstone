using System;
using System.Security.Cryptography;
using System.Text;

namespace UDI_SSW_Prototype.Services
{
    public interface ICryptoService
    {
        (string PrivateKeyPem, string PublicKeyPem) GenerateKeyPair();
        string SignData(string data, string privateKeyPem);
        bool VerifySignature(string data, string signatureBase64, string publicKeyPem);
    }

    public class CryptoService : ICryptoService
    {
        public (string PrivateKeyPem, string PublicKeyPem) GenerateKeyPair()
        {
            using var rsa = RSA.Create(2048);
            var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
            var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
            return (privateKeyPem, publicKeyPem);
        }

        public string SignData(string data, string privateKeyPem)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            
            var bytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signatureBytes);
        }

        public bool VerifySignature(string data, string signatureBase64, string publicKeyPem)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(publicKeyPem);
                
                var bytes = Encoding.UTF8.GetBytes(data);
                var signatureBytes = Convert.FromBase64String(signatureBase64);
                return rsa.VerifyData(bytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }
    }
}

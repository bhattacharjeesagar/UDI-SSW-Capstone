namespace UDI_SSW_Prototype.Models
{
    public class KeyRegistry
    {
        public string IssuerName { get; set; } = string.Empty;
        public string PublicKeyPem { get; set; } = string.Empty;
        public string PrivateKeyPem { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}

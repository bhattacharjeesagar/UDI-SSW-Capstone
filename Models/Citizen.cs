using System;

namespace UDI_SSW_Prototype.Models
{
    public class Citizen
    {
        public string Did { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime Birthdate { get; set; }
        public string LicenseNumber { get; set; } = string.Empty;
        public string PublicKeyPem { get; set; } = string.Empty;
        public string PrivateKeyPem { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime? LastActiveTime { get; set; }
    }
}

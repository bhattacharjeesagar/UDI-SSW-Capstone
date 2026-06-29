using System;

namespace UDI_SSW_Prototype.Models
{
    public class VerifiableCredential
    {
        public int Id { get; set; }
        public string SubjectDid { get; set; } = string.Empty;
        public string IssuerName { get; set; } = string.Empty;
        public string ClaimName { get; set; } = string.Empty;
        public string ClaimValue { get; set; } = string.Empty;
        public string SignedPayload { get; set; } = string.Empty;
        public string? DocumentPath { get; set; } // PDF Upload path in wwwroot
        public bool IsRevoked { get; set; } = false;
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    }
}

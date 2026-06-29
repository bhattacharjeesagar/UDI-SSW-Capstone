using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UDI_SSW_Prototype.Data;
using UDI_SSW_Prototype.Models;
using UDI_SSW_Prototype.Services;

namespace UDI_SSW_Prototype.Controllers
{
    [Authorize(Roles = "Verifier")]
    public class VerifierController : Controller
    {
        private readonly IKeyRegistryRepository _keyRepo;
        private readonly ICredentialRepository _credRepo;
        private readonly ICitizenRepository _citizenRepo;
        private readonly ICryptoService _crypto;

        public VerifierController(
            IKeyRegistryRepository keyRepo, 
            ICredentialRepository credRepo, 
            ICitizenRepository citizenRepo,
            ICryptoService crypto)
        {
            _keyRepo = keyRepo;
            _credRepo = credRepo;
            _citizenRepo = citizenRepo;
            _crypto = crypto;
        }

        [HttpGet]
        public IActionResult Index(string? searchLicenseNumber)
        {
            ViewBag.SearchLicenseNumber = searchLicenseNumber;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Search(string licenseNumber)
        {
            if (string.IsNullOrEmpty(licenseNumber))
            {
                ViewBag.Error = "Please enter a driver's license number to search.";
                return View("Index");
            }

            var citizens = await _citizenRepo.GetAllAsync();
            var citizen = citizens.FirstOrDefault(c => c.LicenseNumber.Equals(licenseNumber, StringComparison.OrdinalIgnoreCase));
            
            if (citizen == null)
            {
                ViewBag.Error = $"No candidate found matching license number '{licenseNumber}'.";
                return View("Index");
            }

            if (!citizen.IsActive)
            {
                ViewBag.Error = $"Citizen profile associated with license '{licenseNumber}' is inactive. Verification cannot proceed.";
                return View("Index");
            }

            // Fetch credentials matching candidate DID
            var credentials = await _credRepo.GetBySubjectDidAsync(citizen.Did);
            var licenseCred = credentials.FirstOrDefault(c => c.ClaimName.Equals("Driving License", StringComparison.OrdinalIgnoreCase));

            ViewBag.Candidate = citizen;
            ViewBag.LicenseCredential = licenseCred;
            ViewBag.SearchLicenseNumber = licenseNumber;

            return View("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Verify(int credentialId, string searchLicenseNumber)
        {
            var cred = await _credRepo.GetByIdAsync(credentialId);
            if (cred == null)
            {
                ViewBag.Error = "Selected credential not found in system.";
                return RedirectToAction(nameof(Index), new { searchLicenseNumber });
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(cred.SignedPayload);
                JsonElement root = doc.RootElement;
                
                string signature = root.GetProperty("signature").GetString() ?? string.Empty;
                JsonElement payloadElement = root.GetProperty("payload");
                
                string payloadJson = payloadElement.GetRawText();

                string subjectDid = payloadElement.GetProperty("subjectDid").GetString() ?? string.Empty;
                string issuerName = payloadElement.GetProperty("issuerName").GetString() ?? string.Empty;
                string claimName = payloadElement.GetProperty("claimName").GetString() ?? string.Empty;
                string claimValue = payloadElement.GetProperty("claimValue").GetString() ?? string.Empty;
                string issuedAt = payloadElement.GetProperty("issuedAt").GetString() ?? string.Empty;

                var registry = await _keyRepo.GetByIssuerNameAsync(issuerName);
                if (registry == null)
                {
                    ViewBag.VerificationResult = $"FAILED: Public key for issuer '{issuerName}' not found in registry.";
                    ViewBag.VerificationSuccess = false;
                }
                else if (!registry.IsActive)
                {
                    ViewBag.VerificationResult = $"FAILED: The registry keys for issuer '{issuerName}' are currently deactivated by administrator.";
                    ViewBag.VerificationSuccess = false;
                }
                else
                {
                    bool isValid = _crypto.VerifySignature(payloadJson, signature, registry.PublicKeyPem);

                    if (isValid)
                    {
                        ViewBag.VerificationSuccess = true;
                        ViewBag.VerificationResult = "SUCCESS: The cryptographic signature is VALID. The Government Authority confirmed this credential is authentic and untampered.";
                        ViewBag.SubjectDid = subjectDid;
                        ViewBag.IssuerName = issuerName;
                        ViewBag.ClaimName = claimName;
                        ViewBag.ClaimValue = claimValue;
                        ViewBag.IssuedAt = issuedAt;
                        ViewBag.PiiStatus = "SUCCESS: Zero PII Exposed. Validated driver identity offline using digital signatures without storing raw birthdates or addresses.";
                    }
                    else
                    {
                        ViewBag.VerificationSuccess = false;
                        ViewBag.VerificationResult = "FAILED: The cryptographic signature is INVALID. The payload has been tampered with or signed by an untrusted key.";
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.VerificationSuccess = false;
                ViewBag.VerificationResult = $"FAILED: Error parsing payload structure. {ex.Message}";
            }

            // Restore candidate profile for display
            var citizen = await _citizenRepo.GetByDidAsync(cred.SubjectDid);
            var credentials = await _credRepo.GetBySubjectDidAsync(cred.SubjectDid);
            var licenseCred = credentials.FirstOrDefault(c => c.ClaimName.Equals("Driving License", StringComparison.OrdinalIgnoreCase));

            ViewBag.Candidate = citizen;
            ViewBag.LicenseCredential = licenseCred;
            ViewBag.SearchLicenseNumber = searchLicenseNumber;

            return View("Index");
        }
    }
}

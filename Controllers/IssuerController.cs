using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UDI_SSW_Prototype.Data;
using UDI_SSW_Prototype.Models;
using UDI_SSW_Prototype.Services;

namespace UDI_SSW_Prototype.Controllers
{
    [Authorize(Roles = "Issuer")]
    public class IssuerController : Controller
    {
        private readonly ICitizenRepository _citizenRepo;
        private readonly IKeyRegistryRepository _keyRepo;
        private readonly ICredentialRepository _credRepo;
        private readonly ICryptoService _crypto;
        private readonly IWebHostEnvironment _env;

        public IssuerController(
            ICitizenRepository citizenRepo, 
            IKeyRegistryRepository keyRepo, 
            ICredentialRepository credRepo, 
            ICryptoService crypto,
            IWebHostEnvironment env)
        {
            _citizenRepo = citizenRepo;
            _keyRepo = keyRepo;
            _credRepo = credRepo;
            _crypto = crypto;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var citizens = await _citizenRepo.GetAllAsync();
            var registries = await _keyRepo.GetAllAsync();
            
            ViewBag.Registries = registries.Where(k => k.IsActive).ToList();
            
            return View(citizens.ToList());
        }

        public async Task<IActionResult> Registries()
        {
            var registries = await _keyRepo.GetAllAsync();
            return View(registries.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> CreateRegistry(string issuerName)
        {
            if (string.IsNullOrEmpty(issuerName))
            {
                TempData["ErrorMessage"] = "Issuer Name cannot be empty.";
                return RedirectToAction(nameof(Registries));
            }

            var existing = await _keyRepo.GetByIssuerNameAsync(issuerName);
            if (existing != null)
            {
                TempData["ErrorMessage"] = $"Registry '{issuerName}' already exists.";
                return RedirectToAction(nameof(Registries));
            }

            var keys = _crypto.GenerateKeyPair();
            var registry = new KeyRegistry
            {
                IssuerName = issuerName,
                PrivateKeyPem = keys.PrivateKeyPem,
                PublicKeyPem = keys.PublicKeyPem,
                IsActive = true
            };

            await _keyRepo.AddAsync(registry);

            TempData["SuccessMessage"] = $"Registry '{issuerName}' and cryptographic keys created successfully!";
            return RedirectToAction(nameof(Registries));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleRegistryStatus(string issuerName, bool isActive)
        {
            await _keyRepo.ToggleActiveAsync(issuerName, isActive);
            TempData["SuccessMessage"] = $"Registry '{issuerName}' status updated successfully!";
            return RedirectToAction(nameof(Registries));
        }

        [HttpPost]
        public async Task<IActionResult> IssueCredential(
            string citizenDid, 
            string claimName, 
            string claimValue, 
            string issuerName, 
            IFormFile? documentFile)
        {
            var citizen = await _citizenRepo.GetByDidAsync(citizenDid);
            if (citizen == null)
            {
                TempData["ErrorMessage"] = "Citizen not found.";
                return RedirectToAction(nameof(Index));
            }

            var issuerKeys = await _keyRepo.GetByIssuerNameAsync(issuerName);
            if (issuerKeys == null || !issuerKeys.IsActive)
            {
                TempData["ErrorMessage"] = "Selected Issuer Key Registry is inactive or does not exist.";
                return RedirectToAction(nameof(Index));
            }

            string? savedFilePath = null;

            // Handle PDF Document upload (Max 1MB)
            if (documentFile != null && documentFile.Length > 0)
            {
                if (documentFile.Length > 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "Document file size exceeds the 1MB limit.";
                    return RedirectToAction(nameof(Index));
                }

                if (!documentFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Only PDF files are allowed for document upload.";
                    return RedirectToAction(nameof(Index));
                }

                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(documentFile.FileName)}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await documentFile.CopyToAsync(fileStream);
                }

                savedFilePath = $"/uploads/{uniqueFileName}";
            }

            // Define the VC Payload structure
            var payloadObj = new
            {
                subjectDid = citizen.Did,
                issuerName = issuerName,
                claimName = claimName,
                claimValue = claimValue,
                issuedAt = DateTime.UtcNow.ToString("o")
            };
            
            string payloadJson = JsonSerializer.Serialize(payloadObj);
            
            // Cryptographically sign the payload
            string signature = _crypto.SignData(payloadJson, issuerKeys.PrivateKeyPem);

            // Assemble signed VC
            var signedVc = new
            {
                payload = payloadObj,
                signature = signature
            };

            string fullSignedPayloadJson = JsonSerializer.Serialize(signedVc);

            var vc = new VerifiableCredential
            {
                SubjectDid = citizen.Did,
                IssuerName = issuerName,
                ClaimName = claimName,
                ClaimValue = claimValue,
                SignedPayload = fullSignedPayloadJson,
                DocumentPath = savedFilePath,
                IsRevoked = false,
                IssuedAt = DateTime.UtcNow
            };

            await _credRepo.AddAsync(vc);

            // If it is a Driver's License, update the citizen profile with the license number
            if (claimName.Equals("Driving License", StringComparison.OrdinalIgnoreCase))
            {
                await _citizenRepo.UpdateLicenseNumberAsync(citizen.Did, claimValue);
            }

            TempData["SuccessMessage"] = $"Successfully issued and signed '{claimName}' credential for {citizen.Name}!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleCitizenActive(string did, bool isActive)
        {
            await _citizenRepo.ToggleActiveAsync(did, isActive);
            TempData["SuccessMessage"] = $"Citizen wallet status updated successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}

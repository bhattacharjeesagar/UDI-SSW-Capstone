using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UDI_SSW_Prototype.Data;
using UDI_SSW_Prototype.Models;
using UDI_SSW_Prototype.Services;

namespace UDI_SSW_Prototype.Controllers
{
    public class WalletController : Controller
    {
        private readonly ICitizenRepository _citizenRepo;
        private readonly ICredentialRepository _credRepo;
        private readonly IUserRepository _userRepo;
        private readonly ICryptoService _crypto;

        public WalletController(
            ICitizenRepository citizenRepo, 
            ICredentialRepository credRepo, 
            IUserRepository userRepo,
            ICryptoService crypto)
        {
            _citizenRepo = citizenRepo;
            _credRepo = credRepo;
            _userRepo = userRepo;
            _crypto = crypto;
        }

        [Authorize(Roles = "Citizen")]
        public async Task<IActionResult> Index()
        {
            string? citizenDid = User.FindFirst("AssociatedDid")?.Value;
            if (string.IsNullOrEmpty(citizenDid))
            {
                TempData["ErrorMessage"] = "No registered wallet profile linked to this user account.";
                return RedirectToAction("Logout", "Auth");
            }

            var citizen = await _citizenRepo.GetByDidAsync(citizenDid);
            if (citizen == null)
            {
                TempData["ErrorMessage"] = "Wallet profile not found in database.";
                return RedirectToAction("Logout", "Auth");
            }

            if (!citizen.IsActive)
            {
                TempData["ErrorMessage"] = "Your wallet profile has been deactivated. Please contact admin for reactivation.";
                return RedirectToAction("Logout", "Auth");
            }

            // Update last active time stamp
            await _citizenRepo.UpdateLastActiveTimeAsync(citizenDid);

            var credentials = await _credRepo.GetBySubjectDidAsync(citizenDid);
            
            ViewBag.Citizen = citizen;
            return View(credentials.ToList());
        }

        [HttpGet]
        public IActionResult CreateProfile()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateProfile(
            string name, 
            DateTime birthdate, 
            string licenseNumber, 
            string username, 
            string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Username and Password are required.";
                return View();
            }

            var existingUser = await _userRepo.GetByUsernameAsync(username);
            if (existingUser != null)
            {
                ViewBag.Error = "Username is already taken.";
                return View();
            }

            // Generate keys and DID
            var keys = _crypto.GenerateKeyPair();
            string did = $"did:citizen:{Guid.NewGuid()}";

            var citizen = new Citizen
            {
                Did = did,
                Name = name,
                Birthdate = birthdate,
                LicenseNumber = licenseNumber,
                PrivateKeyPem = keys.PrivateKeyPem,
                PublicKeyPem = keys.PublicKeyPem,
                IsActive = true,
                LastActiveTime = DateTime.UtcNow
            };

            // Save Citizen profile to DB
            await _citizenRepo.AddAsync(citizen);

            // Save UserMaster account mapped to citizen role (RoleId = 2)
            var user = new UserMaster
            {
                Username = username,
                PasswordHash = password, // Will be hashed in UserRepository.AddAsync
                RoleId = 2,
                FullName = name,
                AssociatedDid = did,
                IsActive = true
            };

            await _userRepo.AddAsync(user);

            TempData["SuccessMessage"] = $"Wallet created successfully for {name}! Please log in with your credentials.";
            return RedirectToAction("Login", "Auth", new { role = "Citizen" });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using UDI_SSW_Prototype.Data;

namespace UDI_SSW_Prototype.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserRepository _userRepo;

        public AuthController(IUserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        [HttpGet]
        public IActionResult Login(string? role, string? returnUrl)
        {
            ViewBag.RoleType = role ?? "Issuer"; // Default to Issuer for presentation
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string roleType, string? returnUrl)
        {
            var user = await _userRepo.AuthenticateAsync(username, password);
            if (user == null)
            {
                ViewBag.Error = "Invalid credentials or deactivated account.";
                ViewBag.RoleType = roleType;
                return View();
            }

            // Check if user role matches the type they are trying to log in as
            int expectedRoleId = roleType switch
            {
                "Issuer" => 1,
                "Citizen" => 2,
                "Verifier" => 3,
                _ => 0
            };

            if (user.RoleId != expectedRoleId)
            {
                ViewBag.Error = $"Unauthorized: You are registered as a {GetRoleName(user.RoleId)}, not {roleType}.";
                ViewBag.RoleType = roleType;
                return View();
            }

            // Create user claims for cookie
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, roleType),
                new Claim("FullName", user.FullName),
                new Claim("UserId", user.UserId.ToString())
            };

            if (!string.IsNullOrEmpty(user.AssociatedDid))
            {
                claims.Add(new Claim("AssociatedDid", user.AssociatedDid));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            // Redirect based on role home screen
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return roleType switch
            {
                "Issuer" => RedirectToAction("Index", "Issuer"),
                "Citizen" => RedirectToAction("Index", "Wallet"),
                "Verifier" => RedirectToAction("Index", "Verifier"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("activeCitizenDid");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private string GetRoleName(int roleId)
        {
            return roleId switch
            {
                1 => "Issuer",
                2 => "Citizen",
                3 => "Verifier",
                _ => "Unknown"
            };
        }
    }
}

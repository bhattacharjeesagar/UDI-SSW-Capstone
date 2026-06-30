using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UDI_SSW_Prototype.Models;

namespace UDI_SSW_Prototype.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public static string? DbInitError { get; set; }

    public IActionResult Index()
    {
        ViewBag.DbInitError = DbInitError;
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

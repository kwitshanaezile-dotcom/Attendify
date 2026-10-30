
using Attendify.Data;
using Attendify.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Attendify.Controllers
{
    public class HomeController : Controller
    {
        private readonly AttendifyContext _context;

        public HomeController(AttendifyContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                bool connected = await _context.Database.CanConnectAsync();

                return Content(connected
                    ? "Attendify successfully connected to SQL Server."
                    : "Attendify could not connect to SQL Server.");
            }
            catch (Exception ex)
            {
                return Content($"Database connection failed: {ex.Message}");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
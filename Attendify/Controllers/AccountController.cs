using System.Security.Claims;
using Attendify.Data;
using Attendify.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Attendify.Controllers
{
    public class AccountController : Controller
    {
        private readonly AttendifyContext _context;
        private readonly PasswordHasher<Person> _hasher = new();

        public AccountController(AttendifyContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var username = model.Username.Trim();
            var person = await _context.Persons.FirstOrDefaultAsync(p => p.Username == username);

            if (person == null
                || string.IsNullOrEmpty(person.PasswordHash)
                || _hasher.VerifyHashedPassword(person, person.PasswordHash, model.Password)
                   == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            // Work out which role this person has
            string? role = null;
            int institutionId = 0;

            var admin = await _context.ITAdmins.FirstOrDefaultAsync(a => a.PersonId == person.PersonId);
            if (admin != null) { role = "Admin"; institutionId = admin.InstitutionId; }
            else
            {
                var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.PersonId == person.PersonId);
                if (lecturer != null) { role = "Lecturer"; institutionId = lecturer.InstitutionId; }
                else
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.PersonId == person.PersonId);
                    if (student != null) { role = "Student"; institutionId = student.InstitutionId; }
                }
            }

            if (role == null)
            {
                ModelState.AddModelError(string.Empty, "Your account has no role assigned. Contact your administrator.");
                return View(model);
            }

            var institutionName = await _context.Institutions
                .Where(i => i.InstitutionId == institutionId)
                .Select(i => i.InstitutionName)
                .FirstOrDefaultAsync() ?? string.Empty;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, person.PersonId.ToString()),
                new Claim(ClaimTypes.Name, $"{person.FirstName} {person.LastName}"),
                new Claim(ClaimTypes.Email, person.Email),
                new Claim(ClaimTypes.Role, role),
                new Claim("InstitutionId", institutionId.ToString()),
                new Claim("InstitutionName", institutionName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return Content("You don't have access to that page.");
        }
    }
}
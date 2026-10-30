using System.Globalization;
using System.Security.Claims;
using Attendify.Data;
using Attendify.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Attendify.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly AttendifyContext _context;

        public StudentController(AttendifyContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> MyAttendance(string? module)
        {
            var student = await GetStudent();
            if (student == null) return RedirectToAction("AccessDenied", "Account");

            var now = DateTime.Now;
            var today = now.Date;
            var nowTime = now.TimeOfDay;

            var sessions = await (
                from e in _context.Enrollments
                where e.StudentId == student.StudentId
                join s in _context.ClassSessions on e.ModuleId equals s.ModuleId
                join m in _context.Modules on s.ModuleId equals m.ModuleId
                join v in _context.Venues on s.VenueId equals v.VenueId
                select new { Session = s, m.ModuleCode, m.ModuleName, v.VenueName }
            ).ToListAsync();

            var records = (await _context.Attendances
                    .Where(a => a.StudentId == student.StudentId)
                    .ToListAsync())
                .GroupBy(a => a.SessionId)
                .ToDictionary(g => g.Key, g => g.First());

            var rows = new List<AttendanceRowItem>();
            foreach (var x in sessions)
            {
                var s = x.Session;
                records.TryGetValue(s.SessionId, out var rec);

                bool over = s.SessionDate.Date < today
                            || (s.SessionDate.Date == today && s.EndTime <= nowTime);

                if (rec == null && (IsCancelled(s) || !over)) continue;

                rows.Add(new AttendanceRowItem
                {
                    Date = s.SessionDate.Date,
                    ModuleCode = x.ModuleCode,
                    ModuleName = x.ModuleName,
                    VenueName = x.VenueName,
                    StartTime = s.StartTime,
                    SigninTime = rec?.SigninTime,
                    Status = rec != null ? Pretty(rec.Status) : "Absent"
                });
            }

            var summaries = rows
                .GroupBy(r => new { r.ModuleCode, r.ModuleName })
                .Select(g =>
                {
                    int held = g.Count();
                    int attended = g.Count(r => r.Status == "Present" || r.Status == "Late");
                    return new ModuleSummaryItem
                    {
                        ModuleCode = g.Key.ModuleCode,
                        ModuleName = g.Key.ModuleName,
                        Held = held,
                        Attended = attended,
                        Rate = held == 0 ? 0 : (int)Math.Round(attended * 100.0 / held)
                    };
                })
                .OrderBy(x => x.ModuleCode)
                .ToList();

            var tableRows = string.IsNullOrEmpty(module)
                ? rows
                : rows.Where(r => r.ModuleCode == module).ToList();

            return View(new MyAttendanceViewModel
            {
                Summaries = summaries,
                Rows = tableRows.OrderByDescending(r => r.Date).ThenByDescending(r => r.StartTime).ToList(),
                ModuleCodes = rows.Select(r => r.ModuleCode).Distinct().OrderBy(c => c).ToList(),
                SelectedModule = module
            });
        }

        [HttpGet]
        public async Task<IActionResult> Timetable()
        {
            var student = await GetStudent();
            if (student == null) return RedirectToAction("AccessDenied", "Account");

            var today = DateTime.Today;

            var raw = await (
                from e in _context.Enrollments
                where e.StudentId == student.StudentId
                join s in _context.ClassSessions on e.ModuleId equals s.ModuleId
                join m in _context.Modules on s.ModuleId equals m.ModuleId
                join v in _context.Venues on s.VenueId equals v.VenueId
                select new { Session = s, m.ModuleCode, m.ModuleName, v.VenueName }
            ).ToListAsync();

            // Collapse dated classes into weekly slots
            var rows = raw
                .GroupBy(x => new
                {
                    x.Session.SessionDate.DayOfWeek,
                    x.Session.StartTime,
                    x.Session.EndTime,
                    x.Session.ModuleId,
                    x.Session.VenueId
                })
                .Select(g =>
                {
                    var first = g.First();
                    var todays = g.FirstOrDefault(x => x.Session.SessionDate.Date == today);

                    string status;
                    if (g.All(x => IsCancelled(x.Session)))
                        status = "Cancelled";
                    else if (todays != null && todays.Session.IsOpen && !IsCancelled(todays.Session))
                        status = "Open now";
                    else
                        status = "Scheduled";

                    return new TimetableRowItem
                    {
                        Day = g.Key.DayOfWeek.ToString(),
                        DayOrder = ((int)g.Key.DayOfWeek + 6) % 7,   // Monday = 0 ... Sunday = 6
                        StartTime = g.Key.StartTime,
                        EndTime = g.Key.EndTime,
                        ModuleCode = first.ModuleCode,
                        ModuleName = first.ModuleName,
                        VenueName = first.VenueName,
                        Status = status,
                        IsToday = g.Key.DayOfWeek == today.DayOfWeek
                    };
                })
                .OrderBy(r => r.DayOrder)
                .ThenBy(r => r.StartTime)
                .ToList();

            return View(new TimetableViewModel { Rows = rows });
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var student = await GetStudent();
            if (student == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(student.PersonId);
            if (person == null) return RedirectToAction("AccessDenied", "Account");

            var model = new ProfileViewModel
            {
                FirstName = person.FirstName,
                LastName = person.LastName,
                Email = person.Email,
                PhoneNumber = person.PhoneNumber
            };
            await FillReadOnly(model, student, person);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var student = await GetStudent();
            if (student == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(student.PersonId);
            if (person == null) return RedirectToAction("AccessDenied", "Account");

            if (!ModelState.IsValid)
            {
                await FillReadOnly(model, student, person);
                return View(model);
            }

            person.Email = model.Email.Trim();
            person.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;
            await _context.SaveChangesAsync();

            var claims = User.Claims
                .Where(c => c.Type != ClaimTypes.Name && c.Type != ClaimTypes.Email)
                .ToList();
            claims.Add(new Claim(ClaimTypes.Name, $"{person.FirstName} {person.LastName}"));
            claims.Add(new Claim(ClaimTypes.Email, person.Email));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task<Student?> GetStudent()
        {
            var personId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await _context.Students.FirstOrDefaultAsync(s => s.PersonId == personId);
        }

        private async Task FillReadOnly(ProfileViewModel model, Student student, Person person)
        {
            model.Username = person.Username;
            model.StudentNumber = student.StudentNumber;
            model.Qualification = student.Qualification;
            model.InstitutionName = await _context.Institutions
                .Where(i => i.InstitutionId == student.InstitutionId)
                .Select(i => i.InstitutionName)
                .FirstOrDefaultAsync();
        }

        private static bool IsCancelled(ClassSession s) =>
            string.Equals(s.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static string Pretty(string? status) =>
            string.IsNullOrWhiteSpace(status)
                ? "Absent"
                : char.ToUpper(status[0]) + status.Substring(1).ToLower();
    }
}
using System.Security.Claims;
using Attendify.Data;
using Attendify.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Attendify.Controllers
{
    [Authorize(Roles = "Lecturer")]
    public class LecturerController : Controller
    {
        private readonly AttendifyContext _context;

        public LecturerController(AttendifyContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Modules()
        {
            var lecturer = await GetLecturer();
            if (lecturer == null) return RedirectToAction("AccessDenied", "Account");

            var modules = await _context.Modules
                .Where(m => m.LecturerId == lecturer.LecturerId)
                .OrderBy(m => m.ModuleCode)
                .ToListAsync();
            var ids = modules.Select(m => m.ModuleId).ToList();

            var studentCounts = await _context.Enrollments
                .Where(e => ids.Contains(e.ModuleId))
                .GroupBy(e => e.ModuleId)
                .Select(g => new { ModuleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ModuleId, x => x.Count);

            var sessions = await _context.ClassSessions
                .Where(s => ids.Contains(s.ModuleId))
                .ToListAsync();

            var items = modules.Select(m => new LecturerModuleItem
            {
                ModuleId = m.ModuleId,
                ModuleCode = m.ModuleCode,
                ModuleName = m.ModuleName,
                StudentCount = studentCounts.GetValueOrDefault(m.ModuleId),
                SessionCount = sessions.Count(s => s.ModuleId == m.ModuleId && !IsCancelled(s))
            }).ToList();

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Sessions()
        {
            var lecturer = await GetLecturer();
            if (lecturer == null) return RedirectToAction("AccessDenied", "Account");

            var today = DateTime.Today;

            var moduleIds = await _context.Modules
                .Where(m => m.LecturerId == lecturer.LecturerId)
                .Select(m => m.ModuleId)
                .ToListAsync();

            var raw = await (
                from s in _context.ClassSessions
                where moduleIds.Contains(s.ModuleId)
                join m in _context.Modules on s.ModuleId equals m.ModuleId
                join v in _context.Venues on s.VenueId equals v.VenueId
                select new { Session = s, m.ModuleCode, m.ModuleName, v.VenueName }
            ).ToListAsync();

            var todays = raw.Where(x => x.Session.SessionDate.Date == today).ToList();
            var todayIds = todays.Select(x => x.Session.SessionId).ToList();

            var signedIn = await _context.Attendances
                .Where(a => todayIds.Contains(a.SessionId))
                .GroupBy(a => a.SessionId)
                .Select(g => new { SessionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SessionId, x => x.Count);

            var enrolled = await _context.Enrollments
                .Where(e => moduleIds.Contains(e.ModuleId))
                .GroupBy(e => e.ModuleId)
                .Select(g => new { ModuleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ModuleId, x => x.Count);

            var todayItems = todays
                .OrderBy(x => x.Session.StartTime)
                .Select(x => new TodaySessionItem
                {
                    SessionId = x.Session.SessionId,
                    ModuleCode = x.ModuleCode,
                    ModuleName = x.ModuleName,
                    VenueName = x.VenueName,
                    StartTime = x.Session.StartTime,
                    EndTime = x.Session.EndTime,
                    IsOpen = x.Session.IsOpen,
                    IsCancelled = IsCancelled(x.Session),
                    SignedIn = signedIn.GetValueOrDefault(x.Session.SessionId),
                    Enrolled = enrolled.GetValueOrDefault(x.Session.ModuleId)
                }).ToList();

             var weekly = raw
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
                    return new TimetableRowItem
                    {
                        Day = g.Key.DayOfWeek.ToString(),
                        DayOrder = ((int)g.Key.DayOfWeek + 6) % 7,
                        StartTime = g.Key.StartTime,
                        EndTime = g.Key.EndTime,
                        ModuleCode = first.ModuleCode,
                        ModuleName = first.ModuleName,
                        VenueName = first.VenueName,
                        IsToday = g.Key.DayOfWeek == today.DayOfWeek
                    };
                })
                .OrderBy(r => r.DayOrder).ThenBy(r => r.StartTime)
                .ToList();

            return View(new LecturerSessionsViewModel { Today = todayItems, Weekly = weekly });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenSession(int id)
        {
            var s = await GetOwnedSession(id);
            if (s == null) { TempData["Error"] = "Session not found."; return RedirectToAction(nameof(Sessions)); }

            if (IsCancelled(s))
                TempData["Error"] = "This class is cancelled and cannot be opened.";
            else if (s.SessionDate.Date != DateTime.Today)
                TempData["Error"] = "You can only open attendance for a class that is scheduled today.";
            else
            {
                s.IsOpen = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Attendance is open. Students can now sign in.";
            }
            return RedirectToAction(nameof(Sessions));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseSession(int id)
        {
            var s = await GetOwnedSession(id);
            if (s == null) { TempData["Error"] = "Session not found."; return RedirectToAction(nameof(Sessions)); }

            s.IsOpen = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Attendance is closed.";
            return RedirectToAction(nameof(Sessions));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSession(int id)
        {
            var s = await GetOwnedSession(id);
            if (s == null) { TempData["Error"] = "Session not found."; return RedirectToAction(nameof(Sessions)); }

            bool hasRecords = await _context.Attendances.AnyAsync(a => a.SessionId == id);

            if (s.SessionDate.Date < DateTime.Today)
                TempData["Error"] = "You can't cancel a class that has already taken place.";
            else if (hasRecords)
                TempData["Error"] = "Students have already signed in to this class, so it can't be cancelled.";
            else
            {
                s.SessionStatus = "Cancelled";
                s.IsOpen = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "The class has been cancelled.";
            }
            return RedirectToAction(nameof(Sessions));
        }

        [HttpGet]
        public async Task<IActionResult> Attendances(int? moduleId, int? sessionId)
        {
            var lecturer = await GetLecturer();
            if (lecturer == null) return RedirectToAction("AccessDenied", "Account");

            var today = DateTime.Today;

            var modules = await _context.Modules
                .Where(m => m.LecturerId == lecturer.LecturerId)
                .OrderBy(m => m.ModuleCode)
                .ToListAsync();

            var vm = new LecturerAttendanceViewModel
            {
                Modules = modules.Select(m => new LecturerModuleItem
                {
                    ModuleId = m.ModuleId,
                    ModuleCode = m.ModuleCode,
                    ModuleName = m.ModuleName
                }).ToList()
            };
            if (!modules.Any()) return View(vm);

            var module = modules.FirstOrDefault(m => m.ModuleId == moduleId) ?? modules.First();
            vm.SelectedModuleId = module.ModuleId;

            var sessions = (await (
                from s in _context.ClassSessions
                where s.ModuleId == module.ModuleId
                join v in _context.Venues on s.VenueId equals v.VenueId
                select new { Session = s, v.VenueName }
            ).ToListAsync())
                .Where(x => x.Session.SessionDate.Date <= today && !IsCancelled(x.Session))
                .OrderByDescending(x => x.Session.SessionDate)
                .ThenByDescending(x => x.Session.StartTime)
                .ToList();

            vm.Sessions = sessions.Select(x => new SessionOption
            {
                SessionId = x.Session.SessionId,
                Label = x.Session.SessionDate.ToString("ddd, dd MMM yyyy") + " · " +
                        x.Session.StartTime.ToString(@"hh\:mm") + "–" +
                        x.Session.EndTime.ToString(@"hh\:mm") + " · " + x.VenueName
            }).ToList();

            var selected = sessions.FirstOrDefault(x => x.Session.SessionId == sessionId)
                           ?? sessions.FirstOrDefault();
            if (selected == null) return View(vm);

            vm.SelectedSessionId = selected.Session.SessionId;
            vm.IsLive = selected.Session.IsOpen && selected.Session.SessionDate.Date == today;

            var enrolled = await (
                from e in _context.Enrollments
                where e.ModuleId == module.ModuleId
                join st in _context.Students on e.StudentId equals st.StudentId
                join p in _context.Persons on st.PersonId equals p.PersonId
                select new { st.StudentId, st.StudentNumber, p.FirstName, p.LastName }
            ).ToListAsync();

            var records = (await _context.Attendances
                    .Where(a => a.SessionId == selected.Session.SessionId)
                    .ToListAsync())
                .GroupBy(a => a.StudentId)
                .ToDictionary(g => g.Key, g => g.First());

            vm.Roster = enrolled
                .OrderBy(r => r.LastName).ThenBy(r => r.FirstName)
                .Select(r =>
                {
                    records.TryGetValue(r.StudentId, out var rec);
                    return new RosterRow
                    {
                        StudentNumber = r.StudentNumber,
                        FullName = r.FirstName + " " + r.LastName,
                        SigninTime = rec?.SigninTime,
                        Status = rec != null ? Pretty(rec.Status)
                                             : (vm.IsLive ? "Not signed in" : "Absent")
                    };
                }).ToList();

            vm.Present = vm.Roster.Count(r => r.Status == "Present");
            vm.Late = vm.Roster.Count(r => r.Status == "Late");
            vm.Absent = vm.Roster.Count(r => r.Status == "Absent" || r.Status == "Not signed in");

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var lecturer = await GetLecturer();
            if (lecturer == null) return RedirectToAction("AccessDenied", "Account");

            var now = DateTime.Now;
            var today = now.Date;
            var nowTime = now.TimeOfDay;

            var modules = await _context.Modules
                .Where(m => m.LecturerId == lecturer.LecturerId)
                .OrderBy(m => m.ModuleCode)
                .ToListAsync();
            var ids = modules.Select(m => m.ModuleId).ToList();

            var studentCounts = await _context.Enrollments
                .Where(e => ids.Contains(e.ModuleId))
                .GroupBy(e => e.ModuleId)
                .Select(g => new { ModuleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ModuleId, x => x.Count);

            var allSessions = await _context.ClassSessions
                .Where(s => ids.Contains(s.ModuleId))
                .ToListAsync();
            var sessionIds = allSessions.Select(s => s.SessionId).ToList();

            var bySession = (await _context.Attendances
                    .Where(a => sessionIds.Contains(a.SessionId))
                    .ToListAsync())
                .GroupBy(a => a.SessionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var items = new List<ModuleReportItem>();
            foreach (var m in modules)
            {
                int enrolled = studentCounts.GetValueOrDefault(m.ModuleId);
                var rates = new List<double>();

                foreach (var s in allSessions.Where(s => s.ModuleId == m.ModuleId && !IsCancelled(s)))
                {
                    bySession.TryGetValue(s.SessionId, out var recs);

                    bool over = s.SessionDate.Date < today
                                || (s.SessionDate.Date == today && s.EndTime <= nowTime);
                    if (!over && recs == null) continue;

                    int attended = recs?.Count(a => IsAttended(a.Status)) ?? 0;
                    rates.Add(enrolled == 0 ? 0 : Math.Min(100, attended * 100.0 / enrolled));
                }

                items.Add(new ModuleReportItem
                {
                    ModuleCode = m.ModuleCode,
                    ModuleName = m.ModuleName,
                    Students = enrolled,
                    SessionsHeld = rates.Count,
                    AvgRate = rates.Any() ? (int)Math.Round(rates.Average()) : 0,
                    LowestRate = rates.Any() ? (int)Math.Round(rates.Min()) : 0
                });
            }

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var lecturer = await GetLecturer();
            if (lecturer == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(lecturer.PersonId);
            if (person == null) return RedirectToAction("AccessDenied", "Account");

            var model = new LecturerProfileViewModel
            {
                Email = person.Email,
                PhoneNumber = person.PhoneNumber
            };
            await FillReadOnly(model, lecturer, person);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(LecturerProfileViewModel model)
        {
            var lecturer = await GetLecturer();
            if (lecturer == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(lecturer.PersonId);
            if (person == null) return RedirectToAction("AccessDenied", "Account");

            if (!ModelState.IsValid)
            {
                await FillReadOnly(model, lecturer, person);
                return View(model);
            }

            person.Email = model.Email.Trim();
            person.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task<Lecturer?> GetLecturer()
        {
            var personId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await _context.Lecturers.FirstOrDefaultAsync(l => l.PersonId == personId);
        }

        private async Task<ClassSession?> GetOwnedSession(int sessionId)
        {
            var lecturer = await GetLecturer();
            if (lecturer == null) return null;

            return await (
                from s in _context.ClassSessions
                where s.SessionId == sessionId
                join m in _context.Modules on s.ModuleId equals m.ModuleId
                where m.LecturerId == lecturer.LecturerId
                select s
            ).FirstOrDefaultAsync();
        }

        private async Task FillReadOnly(LecturerProfileViewModel model, Lecturer lecturer, Person person)
        {
            model.FirstName = person.FirstName;
            model.LastName = person.LastName;
            model.Username = person.Username;
            model.StaffNumber = lecturer.StaffNumber;
            model.Department = lecturer.Department;
            model.InstitutionName = await _context.Institutions
                .Where(i => i.InstitutionId == lecturer.InstitutionId)
                .Select(i => i.InstitutionName)
                .FirstOrDefaultAsync();
        }

        private static bool IsCancelled(ClassSession s) =>
            string.Equals(s.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static bool IsAttended(string? status) =>
            string.Equals(status, "Present", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Late", StringComparison.OrdinalIgnoreCase);

        private static string Pretty(string? status) =>
            string.IsNullOrWhiteSpace(status)
                ? "Absent"
                : char.ToUpper(status[0]) + status.Substring(1).ToLower();
    }
}
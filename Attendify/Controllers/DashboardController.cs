using System.Security.Claims;
using Attendify.Data;
using Attendify.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendify.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AttendifyContext _context;

        public DashboardController(AttendifyContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Admin");
            if (User.IsInRole("Lecturer")) return RedirectToAction("Lecturer");
            return RedirectToAction("Student");
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Student()
        {
            var personId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var student = await _context.Students.FirstOrDefaultAsync(s => s.PersonId == personId);
            if (student == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(personId);
            var today = DateTime.Today;

            var modules = await (
                from e in _context.Enrollments
                where e.StudentId == student.StudentId
                join m in _context.Modules on e.ModuleId equals m.ModuleId
                select m).ToListAsync();

            var moduleIds = modules.Select(m => m.ModuleId).ToList();

            var sessions = await (
                from s in _context.ClassSessions
                where moduleIds.Contains(s.ModuleId)
                join v in _context.Venues on s.VenueId equals v.VenueId
                join m in _context.Modules on s.ModuleId equals m.ModuleId
                select new { Session = s, v.VenueName, m.ModuleCode, m.ModuleName }).ToListAsync();

            var attendances = await _context.Attendances
                .Where(a => a.StudentId == student.StudentId)
                .ToListAsync();

            bool IsCancelled(ClassSession s) =>
                string.Equals(s.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

            bool IsAttended(Attendance a) =>
                string.Equals(a.Status, "Present", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Status, "Late", StringComparison.OrdinalIgnoreCase);

            var heldSessions = sessions
                .Where(x => x.Session.SessionDate.Date <= today && !IsCancelled(x.Session))
                .ToList();
            var attendedCount = heldSessions.Count(x =>
                attendances.Any(a => a.SessionId == x.Session.SessionId && IsAttended(a)));

            var model = new StudentDashboardViewModel
            {
                FirstName = person?.FirstName ?? "Student",
                ModuleCount = modules.Count,
                SessionsAttended = attendedCount,
                AttendanceRate = heldSessions.Count == 0
                    ? 0
                    : (int)Math.Round(attendedCount * 100.0 / heldSessions.Count),
                SessionsToday = sessions.Count(x =>
                    x.Session.SessionDate.Date == today && !IsCancelled(x.Session)),

                Modules = modules
                    .OrderBy(m => m.ModuleCode)
                    .Select(m => new ModuleCardItem
                    {
                        ModuleCode = m.ModuleCode,
                        ModuleName = m.ModuleName
                    }).ToList(),

                LiveSessions = sessions
                    .Where(x => x.Session.IsOpen
                             && x.Session.SessionDate.Date == today
                             && !IsCancelled(x.Session))
                    .Select(x => new LiveSessionItem
                    {
                        SessionId = x.Session.SessionId,
                        ModuleCode = x.ModuleCode,
                        ModuleName = x.ModuleName,
                        VenueName = x.VenueName,
                        StartTime = x.Session.StartTime,
                        EndTime = x.Session.EndTime,
                        AlreadySignedIn = attendances.Any(a => a.SessionId == x.Session.SessionId)
                    }).ToList(),

                Recent = (
                    from a in attendances
                    join x in sessions on a.SessionId equals x.Session.SessionId
                    orderby x.Session.SessionDate descending
                    select new RecentAttendanceItem
                    {
                        ModuleCode = x.ModuleCode,
                        VenueName = x.VenueName,
                        SessionDate = x.Session.SessionDate,
                        Status = a.Status
                    }).Take(5).ToList()
            };

            return View(model);
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Lecturer()
        {
            var personId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.PersonId == personId);
            if (lecturer == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(personId);

            var now = DateTime.Now;
            var today = now.Date;
            var nowTime = now.TimeOfDay;

            var modules = await _context.Modules
                .Where(m => m.LecturerId == lecturer.LecturerId)
                .ToListAsync();
            var ids = modules.Select(m => m.ModuleId).ToList();

            var sessions = await _context.ClassSessions
                .Where(s => ids.Contains(s.ModuleId))
                .ToListAsync();
            var sessionIds = sessions.Select(s => s.SessionId).ToList();

            var bySession = (await _context.Attendances
                    .Where(a => sessionIds.Contains(a.SessionId))
                    .ToListAsync())
                .GroupBy(a => a.SessionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var enrollments = await _context.Enrollments
                .Where(e => ids.Contains(e.ModuleId))
                .ToListAsync();
            var enrolledCount = enrollments
                .GroupBy(e => e.ModuleId)
                .ToDictionary(g => g.Key, g => g.Count());

            var rates = new List<double>();
            foreach (var s in sessions.Where(s => !SessionCancelled(s)))
            {
                bySession.TryGetValue(s.SessionId, out var recs);

                bool over = s.SessionDate.Date < today
                            || (s.SessionDate.Date == today && s.EndTime <= nowTime);
                if (!over && recs == null) continue;

                int enrolled = enrolledCount.GetValueOrDefault(s.ModuleId);
                int attended = recs?.Count(a => StatusAttended(a.Status)) ?? 0;
                rates.Add(enrolled == 0 ? 0 : Math.Min(100, attended * 100.0 / enrolled));
            }

            var todays = sessions
                .Where(s => s.SessionDate.Date == today && !SessionCancelled(s))
                .ToList();
            var live = todays.Where(s => s.IsOpen).OrderBy(s => s.StartTime).ToList();

            var model = new LecturerDashboardViewModel
            {
                LastName = person?.LastName ?? "",
                ModuleCount = modules.Count,
                AvgRate = rates.Any() ? (int)Math.Round(rates.Average()) : 0,
                SessionsToday = todays.Count,
                LiveSessions = live.Count,
                TotalStudents = enrollments.Select(e => e.StudentId).Distinct().Count(),
                SignedInLive = live.Sum(s =>
                    bySession.TryGetValue(s.SessionId, out var r) ? r.Count(a => StatusAttended(a.Status)) : 0)
            };

            if (live.Any())
            {
                var liveModuleIds = live.Select(s => s.ModuleId).Distinct().ToList();
                var venueIds = live.Select(s => s.VenueId).Distinct().ToList();

                var venues = await _context.Venues
                    .Where(v => venueIds.Contains(v.VenueId))
                    .ToDictionaryAsync(v => v.VenueId, v => v.VenueName);

                var people = await (
                    from e in _context.Enrollments
                    where liveModuleIds.Contains(e.ModuleId)
                    join st in _context.Students on e.StudentId equals st.StudentId
                    join p in _context.Persons on st.PersonId equals p.PersonId
                    select new { e.ModuleId, st.StudentId, st.StudentNumber, p.FirstName, p.LastName }
                ).ToListAsync();

                foreach (var s in live)
                {
                    var module = modules.First(m => m.ModuleId == s.ModuleId);
                    var records = bySession.TryGetValue(s.SessionId, out var recList)
                        ? recList.GroupBy(a => a.StudentId).ToDictionary(g => g.Key, g => g.First())
                        : new Dictionary<int, Attendance>();

                    model.LiveRosters.Add(new LiveRosterGroup
                    {
                        SessionId = s.SessionId,
                        ModuleCode = module.ModuleCode,
                        ModuleName = module.ModuleName,
                        VenueName = venues.GetValueOrDefault(s.VenueId) ?? "",
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Rows = people
                            .Where(p => p.ModuleId == s.ModuleId)
                            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                            .Select(p =>
                            {
                                records.TryGetValue(p.StudentId, out var rec);
                                return new RosterRow
                                {
                                    StudentNumber = p.StudentNumber,
                                    FullName = p.FirstName + " " + p.LastName,
                                    SigninTime = rec?.SigninTime,
                                    Status = rec != null ? PrettyStatus(rec.Status) : "Not signed in"
                                };
                            }).ToList()
                    });
                }
            }

            return View(model);
        }

        private static bool SessionCancelled(ClassSession s) =>
            string.Equals(s.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static bool StatusAttended(string? status) =>
            string.Equals(status, "Present", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Late", StringComparison.OrdinalIgnoreCase);

        private static string PrettyStatus(string? status) =>
            string.IsNullOrWhiteSpace(status)
                ? "Absent"
                : char.ToUpper(status[0]) + status.Substring(1).ToLower();

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin()
        {
            var personId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var admin = await _context.ITAdmins.FirstOrDefaultAsync(a => a.PersonId == personId);
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(personId);
            var inst = admin.InstitutionId;
            var today = DateTime.Today;

            var moduleIds = await (
                from m in _context.Modules
                join l in _context.Lecturers on m.LecturerId equals l.LecturerId
                where l.InstitutionId == inst
                select m.ModuleId).ToListAsync();

            var sessions = (await _context.ClassSessions
                    .Where(s => moduleIds.Contains(s.ModuleId))
                    .ToListAsync())
                .Where(s => !SessionCancelled(s))
                .ToList();

            var model = new AdminDashboardViewModel
            {
                FirstName = person?.FirstName ?? "",
                Students = await _context.Students.CountAsync(s => s.InstitutionId == inst),
                Lecturers = await _context.Lecturers.CountAsync(l => l.InstitutionId == inst),
                Modules = moduleIds.Count,
                Venues = await _context.Venues.CountAsync(v => v.InstitutionId == inst),
                ClassesToday = sessions.Count(s => s.SessionDate.Date == today),
                LiveNow = sessions.Count(s => s.SessionDate.Date == today && s.IsOpen),
                WeeklySlots = sessions
                    .GroupBy(s => new { s.ModuleId, s.VenueId, s.SessionDate.DayOfWeek, s.StartTime, s.EndTime })
                    .Count()
            };

            return View(model);
        }
    }
}
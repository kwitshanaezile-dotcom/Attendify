using System.Security.Claims;
using Attendify.Data;
using Attendify.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Attendify.Controllers
{
    [Authorize(Roles = "Student")]
    public class AttendanceController : Controller
    {
        private readonly AttendifyContext _context;

        private const int LateAfterMinutes = 15;

        public AttendanceController(AttendifyContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var student = await GetCurrentStudent();
            if (student == null) return RedirectToAction("AccessDenied", "Account");

            var today = DateTime.Today;

            var open = await (
                from s in _context.ClassSessions
                where s.IsOpen && s.SessionDate.Date == today && s.SessionStatus != "Cancelled"
                join m in _context.Modules on s.ModuleId equals m.ModuleId
                join v in _context.Venues on s.VenueId equals v.VenueId
                where _context.Enrollments.Any(e => e.StudentId == student.StudentId && e.ModuleId == s.ModuleId)
                select new LiveSessionItem
                {
                    SessionId = s.SessionId,
                    ModuleCode = m.ModuleCode,
                    ModuleName = m.ModuleName,
                    VenueName = v.VenueName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    AlreadySignedIn = _context.Attendances.Any(a =>
                        a.SessionId == s.SessionId && a.StudentId == student.StudentId)
                }).ToListAsync();

            if (open.Count == 1 && !open[0].AlreadySignedIn)
                return RedirectToAction("Mark", new { id = open[0].SessionId });

            return View(open);
        }

        [HttpGet]
        public async Task<IActionResult> Mark(int id)
        {
            var student = await GetCurrentStudent();
            if (student == null) return RedirectToAction("AccessDenied", "Account");

            var data = await LoadSession(id, student.StudentId);
            if (data == null)
            {
                TempData["Error"] = "That session is not available for attendance.";
                return RedirectToAction("Student", "Dashboard");
            }

            return View(new MarkAttendanceViewModel
            {
                SessionId = data.Session.SessionId,
                ModuleCode = data.Module.ModuleCode,
                ModuleName = data.Module.ModuleName,
                VenueName = data.Venue.VenueName,
                AllowedRadius = data.Venue.AllowedRadius,
                StartTime = data.Session.StartTime,
                EndTime = data.Session.EndTime
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignIn([FromBody] SignInRequest req)
        {
            var student = await GetCurrentStudent();
            if (student == null)
                return Json(new { success = false, message = "Student account not found." });

            var data = await LoadSession(req.SessionId, student.StudentId);
            if (data == null)
                return Json(new { success = false, message = "This session is not open for your attendance." });

            var alreadySigned = await _context.Attendances
                .AnyAsync(a => a.SessionId == req.SessionId && a.StudentId == student.StudentId);
            if (alreadySigned)
                return Json(new { success = false, message = "You have already signed in to this session." });

            double distance = DistanceMeters(
                req.Latitude, req.Longitude,
                (double)data.Venue.Latitude, (double)data.Venue.Longitude);
            int roundedDistance = (int)Math.Round(distance);
            int allowed = (int)data.Venue.AllowedRadius;

            if (distance > (double)data.Venue.AllowedRadius)
            {
                return Json(new
                {
                    success = false,
                    message = $"You are {roundedDistance} m from {data.Venue.VenueName}. " +
                              $"You must be within {allowed} m to sign in."
                });
            }

            var now = DateTime.Now;
            var classStart = now.Date + data.Session.StartTime;
            var status = now > classStart.AddMinutes(LateAfterMinutes) ? "Late" : "Present";

            _context.Attendances.Add(new Attendance
            {
                SessionId = req.SessionId,
                StudentId = student.StudentId,
                SigninTime = now,
                Status = status,
                StudentLatitude = (decimal)req.Latitude,
                StudentLongitude = (decimal)req.Longitude
            });
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Attendance recorded as {status}. You are {roundedDistance} m from the venue."
            });
        }

        private async Task<Student?> GetCurrentStudent()
        {
            var personId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await _context.Students.FirstOrDefaultAsync(s => s.PersonId == personId);
        }

        private async Task<SessionData?> LoadSession(int sessionId, int studentId)
        {
            var today = DateTime.Today;

            var data = await (
                from s in _context.ClassSessions
                where s.SessionId == sessionId
                join m in _context.Modules on s.ModuleId equals m.ModuleId
                join v in _context.Venues on s.VenueId equals v.VenueId
                where _context.Enrollments.Any(e => e.StudentId == studentId && e.ModuleId == s.ModuleId)
                select new SessionData { Session = s, Module = m, Venue = v }
            ).FirstOrDefaultAsync();

            if (data == null) return null;
            if (!data.Session.IsOpen) return null;
            if (data.Session.SessionDate.Date != today) return null;
            if (string.Equals(data.Session.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return null;

            return data;
        }

        private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; 
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180;

        private class SessionData
        {
            public ClassSession Session { get; set; } = null!;
            public Module Module { get; set; } = null!;
            public Venue Venue { get; set; } = null!;
        }
    }
}
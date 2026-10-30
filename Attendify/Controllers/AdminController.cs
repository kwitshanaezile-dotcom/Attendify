using System.Security.Claims;
using Attendify.Data;
using Attendify.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Attendify.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AttendifyContext _context;
        private readonly PasswordHasher<Person> _hasher = new();
        
        public AdminController(AttendifyContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Sessions()
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var today = DateTime.Today;
            var modules = await LoadModules(admin.InstitutionId);
            var moduleIds = modules.Select(m => m.ModuleId).ToList();

            var venues = await _context.Venues
                .Where(v => v.InstitutionId == admin.InstitutionId)
                .ToDictionaryAsync(v => v.VenueId, v => v.VenueName);

            var sessions = await _context.ClassSessions
                .Where(s => moduleIds.Contains(s.ModuleId))
                .ToListAsync();

            var slots = sessions
                .GroupBy(s => new { s.ModuleId, s.VenueId, s.SessionDate.DayOfWeek, s.StartTime, s.EndTime })
                .Select(g =>
                {
                    var m = modules.First(x => x.ModuleId == g.Key.ModuleId);
                    var active = g.Where(s => !IsCancelled(s)).ToList();
                    return new AdminSlotItem
                    {
                        ModuleId = g.Key.ModuleId,
                        VenueId = g.Key.VenueId,
                        Day = g.Key.DayOfWeek.ToString(),
                        DayOrder = ((int)g.Key.DayOfWeek + 6) % 7,
                        StartTime = g.Key.StartTime,
                        EndTime = g.Key.EndTime,
                        ModuleCode = m.ModuleCode,
                        ModuleName = m.ModuleName,
                        LecturerName = m.LecturerName,
                        VenueName = venues.GetValueOrDefault(g.Key.VenueId) ?? "—",
                        FromDate = g.Min(s => s.SessionDate.Date),
                        ToDate = g.Max(s => s.SessionDate.Date),
                        TotalClasses = active.Count,
                        UpcomingClasses = active.Count(s => s.SessionDate.Date >= today)
                    };
                })
                .OrderBy(x => x.DayOrder).ThenBy(x => x.StartTime).ThenBy(x => x.ModuleCode)
                .ToList();

            return View(slots);
        }

        [HttpGet]
        public async Task<IActionResult> CreateSlot()
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var model = new CreateSlotViewModel
            {
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(4)
            };
            await FillLists(model, admin.InstitutionId);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSlot(CreateSlotViewModel model)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var modules = await LoadModules(admin.InstitutionId);

            if (!ModelState.IsValid)
            {
                await FillLists(model, admin.InstitutionId);
                return View(model);
            }

            var start = model.StartTime!.Value;
            var end = model.EndTime!.Value;
            var from = model.FromDate!.Value.Date;
            var to = model.ToDate!.Value.Date;
            var day = model.Day!.Value;

            var module = modules.FirstOrDefault(m => m.ModuleId == model.ModuleId);
            var venueOk = await _context.Venues.AnyAsync(v =>
                v.VenueId == model.VenueId && v.InstitutionId == admin.InstitutionId);

            if (module == null) ModelState.AddModelError(nameof(model.ModuleId), "Choose a module from your institution.");
            if (!venueOk) ModelState.AddModelError(nameof(model.VenueId), "Choose a venue from your institution.");
            if (end <= start) ModelState.AddModelError(nameof(model.EndTime), "The end time must be after the start time.");
            if (to < from) ModelState.AddModelError(nameof(model.ToDate), "The last date must be on or after the first date.");
            else if ((to - from).TotalDays > 366) ModelState.AddModelError(nameof(model.ToDate), "Choose a period of one year or less.");

            if (!ModelState.IsValid)
            {
                await FillLists(model, admin.InstitutionId);
                return View(model);
            }

            var dates = new List<DateTime>();
            for (var d = from; d <= to; d = d.AddDays(1))
                if (d.DayOfWeek == day) dates.Add(d);

            if (!dates.Any())
            {
                ModelState.AddModelError(string.Empty, $"There is no {day} between those two dates.");
                await FillLists(model, admin.InstitutionId);
                return View(model);
            }

            var lecturerModuleIds = modules
                .Where(m => m.LecturerId == module!.LecturerId)
                .Select(m => m.ModuleId)
                .ToList();

            var existing = await _context.ClassSessions
                .Where(s => s.SessionDate >= from && s.SessionDate < to.AddDays(1)
                         && (s.VenueId == model.VenueId || lecturerModuleIds.Contains(s.ModuleId)))
                .ToListAsync();

            var dateSet = dates.ToHashSet();
            var clash = existing.FirstOrDefault(s =>
                dateSet.Contains(s.SessionDate.Date)
                && !IsCancelled(s)
                && s.StartTime < end && start < s.EndTime);

            if (clash != null)
            {
                string reason = clash.VenueId == model.VenueId
                    ? "that venue is already booked"
                    : "the lecturer already has a class";
                ModelState.AddModelError(string.Empty,
                    "Clash on " + clash.SessionDate.ToString("dd MMM yyyy") + ": " + reason +
                    " from " + clash.StartTime.ToString(@"hh\:mm") + " to " + clash.EndTime.ToString(@"hh\:mm") + ".");
                await FillLists(model, admin.InstitutionId);
                return View(model);
            }

            foreach (var d in dates)
            {
                _context.ClassSessions.Add(new ClassSession
                {
                    ModuleId = model.ModuleId!.Value,
                    VenueId = model.VenueId!.Value,
                    SessionDay = d.DayOfWeek.ToString(),
                    SessionDate = d,
                    StartTime = start,
                    EndTime = end,
                    SessionStatus = "Scheduled",
                    IsOpen = false
                });
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Created {dates.Count} {day} classes for {module!.ModuleCode} " +
                                  $"({from:dd MMM yyyy} to {to:dd MMM yyyy}).";
            return RedirectToAction(nameof(Sessions));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSlot(int moduleId, int venueId, DayOfWeek day,
                                                    TimeSpan startTime, TimeSpan endTime)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var modules = await LoadModules(admin.InstitutionId);
            if (!modules.Any(m => m.ModuleId == moduleId))
            {
                TempData["Error"] = "Slot not found.";
                return RedirectToAction(nameof(Sessions));
            }

            var today = DateTime.Today;

            var matching = (await _context.ClassSessions
                    .Where(s => s.ModuleId == moduleId && s.VenueId == venueId && s.SessionDate >= today)
                    .ToListAsync())
                .Where(s => s.SessionDate.DayOfWeek == day
                         && s.StartTime == startTime && s.EndTime == endTime
                         && !s.IsOpen)
                .ToList();

            var ids = matching.Select(s => s.SessionId).ToList();
            var withRecords = (await _context.Attendances
                    .Where(a => ids.Contains(a.SessionId))
                    .Select(a => a.SessionId)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();

            var toDelete = matching.Where(s => !withRecords.Contains(s.SessionId)).ToList();
            _context.ClassSessions.RemoveRange(toDelete);
            await _context.SaveChangesAsync();

            int kept = matching.Count - toDelete.Count;
            TempData["Success"] = $"Removed {toDelete.Count} upcoming classes." +
                                  (kept > 0 ? $" {kept} kept because students have already signed in." : "") +
                                  " Past classes are never removed.";
            return RedirectToAction(nameof(Sessions));
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? q)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");
            var inst = admin.InstitutionId;

            var admins = await (
                from a in _context.ITAdmins
                where a.InstitutionId == inst
                join p in _context.Persons on a.PersonId equals p.PersonId
                select new AdminUserRow
                {
                    PersonId = p.PersonId,
                    Role = "IT Administrator",
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Username = p.Username ?? "",
                    Email = p.Email,
                    Reference = a.EmployeeNumber ?? "",
                    Detail = a.OfficeNumber ?? ""
                }).ToListAsync();

            var lecturers = await (
                from l in _context.Lecturers
                where l.InstitutionId == inst
                join p in _context.Persons on l.PersonId equals p.PersonId
                select new AdminUserRow
                {
                    PersonId = p.PersonId,
                    Role = "Lecturer",
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Username = p.Username ?? "",
                    Email = p.Email,
                    Reference = l.StaffNumber,
                    Detail = l.Department ?? ""
                }).ToListAsync();

            var students = await (
                from s in _context.Students
                where s.InstitutionId == inst
                join p in _context.Persons on s.PersonId equals p.PersonId
                select new AdminUserRow
                {
                    PersonId = p.PersonId,
                    Role = "Student",
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Username = p.Username ?? "",
                    Email = p.Email,
                    Reference = s.StudentNumber,
                    Detail = s.Qualification ?? ""
                }).ToListAsync();

            var rows = admins.OrderBy(r => r.LastName)
                .Concat(lecturers.OrderBy(r => r.LastName))
                .Concat(students.OrderBy(r => r.LastName))
                .ToList();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                rows = rows.Where(r =>
                    r.FullName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    r.Username.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    r.Reference.Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return View(new AdminUsersViewModel { Query = q, Rows = rows });
        }

        [HttpGet]
        public async Task<IActionResult> AddUser()
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");
            return View(new AddUserViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(AddUserViewModel model)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var username = model.Username?.Trim() ?? "";
            var reference = model.Reference?.Trim() ?? "";

            if (model.Role != "Student" && model.Role != "Lecturer")
                ModelState.AddModelError(nameof(model.Role), "Choose Student or Lecturer.");

            if (ModelState.IsValid)
            {
                if (await _context.Persons.AnyAsync(p => p.Username == username))
                    ModelState.AddModelError(nameof(model.Username), "That username is already taken.");

                if (model.Role == "Student" && await _context.Students.AnyAsync(s => s.StudentNumber == reference))
                    ModelState.AddModelError(nameof(model.Reference), "That student number already exists.");

                if (model.Role == "Lecturer" && await _context.Lecturers.AnyAsync(l => l.StaffNumber == reference))
                    ModelState.AddModelError(nameof(model.Reference), "That staff number already exists.");
            }

            if (!ModelState.IsValid) return View(model);

            await using var tx = await _context.Database.BeginTransactionAsync();

            var person = new Person
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty,
                Username = username
            };
            person.PasswordHash = _hasher.HashPassword(person, model.Password);
            _context.Persons.Add(person);
            await _context.SaveChangesAsync();          

            if (model.Role == "Student")
            {
                _context.Students.Add(new Student
                {
                    StudentNumber = reference,
                    Qualification = model.Qualification?.Trim(),
                    InstitutionId = admin.InstitutionId,
                    PersonId = person.PersonId
                });
            }
            else
            {
                _context.Lecturers.Add(new Lecturer
                {
                    StaffNumber = reference,
                    Department = model.Department?.Trim(),
                    InstitutionId = admin.InstitutionId,
                    PersonId = person.PersonId
                });
            }
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Success"] = $"{model.Role} {person.FirstName} {person.LastName} was added. " +
                                  "Give them their username and the initial password you set.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var role = await RoleInInstitution(id, admin.InstitutionId);
            var person = role == null ? null : await _context.Persons.FindAsync(id);
            if (person == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            return View(new EditUserViewModel
            {
                PersonId = id,
                FirstName = person.FirstName,
                LastName = person.LastName,
                Email = person.Email,
                PhoneNumber = person.PhoneNumber,
                Role = role,
                Username = person.Username
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var role = await RoleInInstitution(model.PersonId, admin.InstitutionId);
            var person = role == null ? null : await _context.Persons.FindAsync(model.PersonId);
            if (person == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            if (!ModelState.IsValid)
            {
                model.Role = role;
                model.Username = person.Username;
                return View(model);
            }

            person.FirstName = model.FirstName.Trim();
            person.LastName = model.LastName.Trim();
            person.Email = model.Email.Trim();
            person.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(model.NewPassword))
                person.PasswordHash = _hasher.HashPassword(person, model.NewPassword);

            await _context.SaveChangesAsync();
            TempData["Success"] = "The user was updated." +
                                  (string.IsNullOrEmpty(model.NewPassword) ? "" : " Their password was reset.");
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> Modules()
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var modules = await LoadModules(admin.InstitutionId);
            var ids = modules.Select(m => m.ModuleId).ToList();

            var students = await _context.Enrollments
                .Where(e => ids.Contains(e.ModuleId))
                .GroupBy(e => e.ModuleId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var sessions = await _context.ClassSessions
                .Where(s => ids.Contains(s.ModuleId))
                .ToListAsync();

            var rows = modules.Select(m => new AdminModuleRow
            {
                ModuleId = m.ModuleId,
                ModuleCode = m.ModuleCode,
                ModuleName = m.ModuleName,
                LecturerName = m.LecturerName,
                Students = students.GetValueOrDefault(m.ModuleId),
                Classes = sessions.Count(s => s.ModuleId == m.ModuleId && !IsCancelled(s))
            }).ToList();

            return View(rows);
        }

        [HttpGet]
        public async Task<IActionResult> AddModule()
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var model = new AddModuleViewModel();
            await FillLecturers(model, admin.InstitutionId);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddModule(AddModuleViewModel model)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");
            var inst = admin.InstitutionId;

            var code = model.ModuleCode?.Trim().ToUpperInvariant() ?? "";

            if (ModelState.IsValid)
            {
                bool lecturerOk = await _context.Lecturers.AnyAsync(l =>
                    l.LecturerId == model.LecturerId && l.InstitutionId == inst);
                if (!lecturerOk)
                    ModelState.AddModelError(nameof(model.LecturerId), "Choose a lecturer from your institution.");

                var existing = await LoadModules(inst);
                if (existing.Any(m => string.Equals(m.ModuleCode, code, StringComparison.OrdinalIgnoreCase)))
                    ModelState.AddModelError(nameof(model.ModuleCode), "That module code already exists.");
            }

            if (!ModelState.IsValid)
            {
                await FillLecturers(model, inst);
                return View(model);
            }

            _context.Modules.Add(new Attendify.Models.Module
            {
                ModuleCode = code,
                ModuleName = model.ModuleName.Trim(),
                LecturerId = model.LecturerId!.Value
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Module {code} was added. Now enrol its students.";
            return RedirectToAction(nameof(Modules));
        }

        [HttpGet]
        public async Task<IActionResult> EnrollStudents(int id)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var module = (await LoadModules(admin.InstitutionId)).FirstOrDefault(m => m.ModuleId == id);
            if (module == null)
            {
                TempData["Error"] = "Module not found.";
                return RedirectToAction(nameof(Modules));
            }
            return View(await BuildEnroll(module, admin.InstitutionId));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EnrollStudents(int id, List<int>? studentIds)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");
            var inst = admin.InstitutionId;

            var module = (await LoadModules(inst)).FirstOrDefault(m => m.ModuleId == id);
            if (module == null)
            {
                TempData["Error"] = "Module not found.";
                return RedirectToAction(nameof(Modules));
            }

            var validIds = (await _context.Students
                    .Where(s => s.InstitutionId == inst)
                    .Select(s => s.StudentId)
                    .ToListAsync())
                .ToHashSet();

            var selected = (studentIds ?? new List<int>()).Where(validIds.Contains).ToHashSet();

            var current = await _context.Enrollments.Where(e => e.ModuleId == id).ToListAsync();
            var currentIds = current.Select(e => e.StudentId).ToHashSet();

            var toRemove = current.Where(e => validIds.Contains(e.StudentId) && !selected.Contains(e.StudentId)).ToList();
            var toAdd = selected.Where(s => !currentIds.Contains(s))
                .Select(s => new Enrollment { StudentId = s, ModuleId = id, EnrollmentDate = DateTime.Today })
                .ToList();

            _context.Enrollments.RemoveRange(toRemove);
            _context.Enrollments.AddRange(toAdd);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{module.ModuleCode}: {toAdd.Count} student(s) enrolled, {toRemove.Count} removed.";
            return RedirectToAction(nameof(Modules));
        }

        [HttpGet]
        public async Task<IActionResult> Venues()
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var venues = await _context.Venues
                .Where(v => v.InstitutionId == admin.InstitutionId)
                .OrderBy(v => v.VenueName)
                .ToListAsync();
            var ids = venues.Select(v => v.VenueId).ToList();

            var counts = await _context.ClassSessions
                .Where(s => ids.Contains(s.VenueId))
                .GroupBy(s => s.VenueId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return View(venues.Select(v => new AdminVenueRow
            {
                VenueId = v.VenueId,
                VenueName = v.VenueName,
                Latitude = v.Latitude,
                Longitude = v.Longitude,
                AllowedRadius = v.AllowedRadius,
                Classes = counts.GetValueOrDefault(v.VenueId)
            }).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> VenueForm(int? id)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            if (id == null) return View(new VenueFormViewModel());

            var v = await _context.Venues.FirstOrDefaultAsync(x =>
                x.VenueId == id && x.InstitutionId == admin.InstitutionId);
            if (v == null)
            {
                TempData["Error"] = "Venue not found.";
                return RedirectToAction(nameof(Venues));
            }

            return View(new VenueFormViewModel
            {
                VenueId = v.VenueId,
                VenueName = v.VenueName,
                Latitude = v.Latitude,
                Longitude = v.Longitude,
                AllowedRadius = v.AllowedRadius
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> VenueForm(VenueFormViewModel model)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");
            var inst = admin.InstitutionId;

            var name = model.VenueName?.Trim() ?? "";

            if (ModelState.IsValid &&
                await _context.Venues.AnyAsync(v => v.InstitutionId == inst && v.VenueName == name && v.VenueId != model.VenueId))
                ModelState.AddModelError(nameof(model.VenueName), "A venue with that name already exists.");

            if (!ModelState.IsValid) return View(model);

            if (model.VenueId == 0)
            {
                _context.Venues.Add(new Venue
                {
                    InstitutionId = inst,
                    VenueName = name,
                    Latitude = model.Latitude!.Value,
                    Longitude = model.Longitude!.Value,
                    AllowedRadius = model.AllowedRadius!.Value
                });
            }
            else
            {
                var v = await _context.Venues.FirstOrDefaultAsync(x => x.VenueId == model.VenueId && x.InstitutionId == inst);
                if (v == null)
                {
                    TempData["Error"] = "Venue not found.";
                    return RedirectToAction(nameof(Venues));
                }
                v.VenueName = name;
                v.Latitude = model.Latitude!.Value;
                v.Longitude = model.Longitude!.Value;
                v.AllowedRadius = model.AllowedRadius!.Value;
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = model.VenueId == 0 ? "Venue added." : "Venue updated.";
            return RedirectToAction(nameof(Venues));
        }

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var now = DateTime.Now;
            var today = now.Date;
            var nowTime = now.TimeOfDay;

            var modules = await LoadModules(admin.InstitutionId);
            var ids = modules.Select(m => m.ModuleId).ToList();

            var studentCounts = await _context.Enrollments
                .Where(e => ids.Contains(e.ModuleId))
                .GroupBy(e => e.ModuleId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var allSessions = await _context.ClassSessions.Where(s => ids.Contains(s.ModuleId)).ToListAsync();
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
                    LecturerName = m.LecturerName,
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
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(admin.PersonId);
            if (person == null) return RedirectToAction("AccessDenied", "Account");

            var model = new AdminProfileViewModel { Email = person.Email, PhoneNumber = person.PhoneNumber };
            await FillProfile(model, admin, person);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(AdminProfileViewModel model)
        {
            var admin = await GetAdmin();
            if (admin == null) return RedirectToAction("AccessDenied", "Account");

            var person = await _context.Persons.FindAsync(admin.PersonId);
            if (person == null) return RedirectToAction("AccessDenied", "Account");

            if (!ModelState.IsValid)
            {
                await FillProfile(model, admin, person);
                return View(model);
            }

            person.Email = model.Email.Trim();
            person.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }
        private async Task<ITAdmin?> GetAdmin()
        {
            var personId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await _context.ITAdmins.FirstOrDefaultAsync(a => a.PersonId == personId);
        }

        private async Task<List<ModuleInfo>> LoadModules(int institutionId)
        {
            return await (
                from m in _context.Modules
                join l in _context.Lecturers on m.LecturerId equals l.LecturerId
                where l.InstitutionId == institutionId
                join p in _context.Persons on l.PersonId equals p.PersonId
                orderby m.ModuleCode
                select new ModuleInfo
                {
                    ModuleId = m.ModuleId,
                    ModuleCode = m.ModuleCode,
                    ModuleName = m.ModuleName,
                    LecturerId = l.LecturerId,
                    LecturerName = p.FirstName + " " + p.LastName
                }
            ).ToListAsync();
        }

        private async Task FillLists(CreateSlotViewModel model, int institutionId)
        {
            var modules = await LoadModules(institutionId);
            model.Modules = modules.Select(m => new SelectListItem
            {
                Value = m.ModuleId.ToString(),
                Text = m.ModuleCode + " · " + m.ModuleName + " (" + m.LecturerName + ")"
            }).ToList();

            model.Venues = await _context.Venues
                .Where(v => v.InstitutionId == institutionId)
                .OrderBy(v => v.VenueName)
                .Select(v => new SelectListItem { Value = v.VenueId.ToString(), Text = v.VenueName })
                .ToListAsync();
        }

        private async Task<string?> RoleInInstitution(int personId, int inst)
        {
            if (await _context.Students.AnyAsync(s => s.PersonId == personId && s.InstitutionId == inst)) return "Student";
            if (await _context.Lecturers.AnyAsync(l => l.PersonId == personId && l.InstitutionId == inst)) return "Lecturer";
            if (await _context.ITAdmins.AnyAsync(a => a.PersonId == personId && a.InstitutionId == inst)) return "IT Administrator";
            return null;
        }

        private async Task FillLecturers(AddModuleViewModel model, int inst)
        {
            model.Lecturers = await (
                from l in _context.Lecturers
                where l.InstitutionId == inst
                join p in _context.Persons on l.PersonId equals p.PersonId
                orderby p.LastName, p.FirstName
                select new SelectListItem
                {
                    Value = l.LecturerId.ToString(),
                    Text = p.FirstName + " " + p.LastName + " (" + l.StaffNumber + ")"
                }).ToListAsync();
        }

        private async Task<EnrollViewModel> BuildEnroll(ModuleInfo module, int inst)
        {
            var enrolledIds = (await _context.Enrollments
                    .Where(e => e.ModuleId == module.ModuleId)
                    .Select(e => e.StudentId)
                    .ToListAsync())
                .ToHashSet();

            var students = await (
                from s in _context.Students
                where s.InstitutionId == inst
                join p in _context.Persons on s.PersonId equals p.PersonId
                orderby p.LastName, p.FirstName
                select new { s.StudentId, s.StudentNumber, p.FirstName, p.LastName }).ToListAsync();

            return new EnrollViewModel
            {
                ModuleId = module.ModuleId,
                ModuleCode = module.ModuleCode,
                ModuleName = module.ModuleName,
                Students = students.Select(s => new EnrollStudentItem
                {
                    StudentId = s.StudentId,
                    StudentNumber = s.StudentNumber,
                    FullName = s.FirstName + " " + s.LastName,
                    Enrolled = enrolledIds.Contains(s.StudentId)
                }).ToList()
            };
        }

        private async Task FillProfile(AdminProfileViewModel model, ITAdmin admin, Person person)
        {
            model.FirstName = person.FirstName;
            model.LastName = person.LastName;
            model.Username = person.Username;
            model.EmployeeNumber = admin.EmployeeNumber;
            model.OfficeNumber = admin.OfficeNumber;
            model.InstitutionName = await _context.Institutions
                .Where(i => i.InstitutionId == admin.InstitutionId)
                .Select(i => i.InstitutionName)
                .FirstOrDefaultAsync();
        }

        private static bool IsAttended(string? status) =>
            string.Equals(status, "Present", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Late", StringComparison.OrdinalIgnoreCase);
        private static bool IsCancelled(ClassSession s) =>
            string.Equals(s.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private class ModuleInfo
        {
            public int ModuleId { get; set; }
            public string ModuleCode { get; set; } = string.Empty;
            public string ModuleName { get; set; } = string.Empty;
            public int LecturerId { get; set; }
            public string LecturerName { get; set; } = string.Empty;
        }
    }
}
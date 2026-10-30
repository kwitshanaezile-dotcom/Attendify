using System.ComponentModel.DataAnnotations;

namespace Attendify.Models
{
    public class LecturerModuleItem
    {
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int SessionCount { get; set; }
    }

    public class LecturerSessionsViewModel
    {
        public List<TodaySessionItem> Today { get; set; } = new();
        public List<TimetableRowItem> Weekly { get; set; } = new();
    }

    public class TodaySessionItem
    {
        public int SessionId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsOpen { get; set; }
        public bool IsCancelled { get; set; }
        public int SignedIn { get; set; }
        public int Enrolled { get; set; }
    }

    public class LecturerAttendanceViewModel
    {
        public List<LecturerModuleItem> Modules { get; set; } = new();
        public int? SelectedModuleId { get; set; }
        public List<SessionOption> Sessions { get; set; } = new();
        public int? SelectedSessionId { get; set; }
        public bool IsLive { get; set; }
        public int Present { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public List<RosterRow> Roster { get; set; } = new();
    }

    public class SessionOption
    {
        public int SessionId { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class RosterRow
    {
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime? SigninTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ModuleReportItem
    {
        public string LecturerName { get; set; } = string.Empty;
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public int Students { get; set; }
        public int SessionsHeld { get; set; }
        public int AvgRate { get; set; }
        public int LowestRate { get; set; }
    }

    public class LecturerProfileViewModel
    {
        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(Rules.Phone, ErrorMessage = Rules.PhoneMsg)]
        public string? PhoneNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? StaffNumber { get; set; }
        public string? Department { get; set; }
        public string? InstitutionName { get; set; }
    }

    public class LecturerDashboardViewModel
    {
        public string LastName { get; set; } = string.Empty;
        public int LiveSessions { get; set; }
        public int ModuleCount { get; set; }
        public int AvgRate { get; set; }
        public int SessionsToday { get; set; }
        public int TotalStudents { get; set; }
        public int SignedInLive { get; set; }
        public List<LiveRosterGroup> LiveRosters { get; set; } = new();
    }

    public class LiveRosterGroup
    {
        public int SessionId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public List<RosterRow> Rows { get; set; } = new();
    }
}
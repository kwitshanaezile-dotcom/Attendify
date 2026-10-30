namespace Attendify.Models
{
    public class StudentDashboardViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public int AttendanceRate { get; set; }
        public int ModuleCount { get; set; }
        public int SessionsToday { get; set; }
        public int SessionsAttended { get; set; }

        public List<ModuleCardItem> Modules { get; set; } = new();
        public List<LiveSessionItem> LiveSessions { get; set; } = new();
        public List<RecentAttendanceItem> Recent { get; set; } = new();
    }

    public class ModuleCardItem
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
    }

    public class LiveSessionItem
    {
        public int SessionId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool AlreadySignedIn { get; set; }
    }

    public class RecentAttendanceItem
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
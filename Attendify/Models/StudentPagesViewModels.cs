using System.ComponentModel.DataAnnotations;

namespace Attendify.Models
{
    public class MyAttendanceViewModel
    {
        public List<ModuleSummaryItem> Summaries { get; set; } = new();
        public List<AttendanceRowItem> Rows { get; set; } = new();
        public List<string> ModuleCodes { get; set; } = new();
        public string? SelectedModule { get; set; }
    }

    public class ModuleSummaryItem
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public int Held { get; set; }
        public int Attended { get; set; }
        public int Rate { get; set; }
    }

    public class AttendanceRowItem
    {
        public DateTime Date { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public DateTime? SigninTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TimetableViewModel
    {
        public List<TimetableRowItem> Rows { get; set; } = new();
    }

    public class TimetableRowItem
    {
        public string Day { get; set; } = string.Empty;
        public int DayOrder { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsToday { get; set; }
    }

    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Enter your first name.")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your last name.")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(Rules.Phone, ErrorMessage = Rules.PhoneMsg)]
        public string? PhoneNumber { get; set; }

        public string? Username { get; set; }
        public string? StudentNumber { get; set; }
        public string? Qualification { get; set; }
        public string? InstitutionName { get; set; }
    }
}
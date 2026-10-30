using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;

namespace Attendify.Models
{
    public class AdminDashboardViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public int LiveNow { get; set; }
        public int Students { get; set; }
        public int Lecturers { get; set; }
        public int Modules { get; set; }
        public int Venues { get; set; }
        public int ClassesToday { get; set; }
        public int WeeklySlots { get; set; }
    }

    public class AdminSlotItem
    {
        public int ModuleId { get; set; }
        public int VenueId { get; set; }
        public string Day { get; set; } = string.Empty;
        public int DayOrder { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string LecturerName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalClasses { get; set; }
        public int UpcomingClasses { get; set; }
    }

    public class CreateSlotViewModel
    {
        [Required(ErrorMessage = "Choose a module.")]
        public int? ModuleId { get; set; }

        [Required(ErrorMessage = "Choose a venue.")]
        public int? VenueId { get; set; }

        [Required(ErrorMessage = "Choose a day.")]
        public DayOfWeek? Day { get; set; }

        [Required(ErrorMessage = "Enter a start time.")]
        public TimeSpan? StartTime { get; set; }

        [Required(ErrorMessage = "Enter an end time.")]
        public TimeSpan? EndTime { get; set; }

        [Required(ErrorMessage = "Enter the first date.")]
        public DateTime? FromDate { get; set; }

        [Required(ErrorMessage = "Enter the last date.")]
        public DateTime? ToDate { get; set; }

        // Dropdown contents (filled by the server, not posted)
        public List<SelectListItem> Modules { get; set; } = new();
        public List<SelectListItem> Venues { get; set; } = new();
    }

    public class AdminUserRow
    {
        public int PersonId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string FullName => FirstName + " " + LastName;
    }

    public class AdminUsersViewModel
    {
        public string? Query { get; set; }
        public List<AdminUserRow> Rows { get; set; } = new();
    }

    public class AddUserViewModel
    {
        [Required(ErrorMessage = "Choose a role.")]
        public string Role { get; set; } = "Student";

		[Required(ErrorMessage = "Enter the first name.")]
		[StringLength(100)]
		[RegularExpression(Rules.PersonName, ErrorMessage = Rules.PersonNameMsg)]
		public string FirstName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Enter the last name.")]
		[StringLength(100)]
		[RegularExpression(Rules.PersonName, ErrorMessage = Rules.PersonNameMsg)]
		public string LastName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Enter a username.")]
		[StringLength(100)]
		[RegularExpression(Rules.Username, ErrorMessage = Rules.UsernameMsg)]
		public string Username { get; set; } = string.Empty;

		[Required(ErrorMessage = "Enter the student or staff number.")]
		[StringLength(50)]
		[RegularExpression(Rules.Number, ErrorMessage = Rules.NumberMsg)]
		public string Reference { get; set; } = string.Empty;

		[StringLength(100)]
		[RegularExpression(Rules.Text, ErrorMessage = Rules.TextMsg)]
		public string? Qualification { get; set; }

		[StringLength(100)]
		[RegularExpression(Rules.Text, ErrorMessage = Rules.TextMsg)]
		public string? Department { get; set; }


        [Required(ErrorMessage = "Enter an email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(Rules.Phone, ErrorMessage = Rules.PhoneMsg)]
        public string? PhoneNumber { get; set; }


        [Required(ErrorMessage = "Set an initial password.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;
    }

    public class EditUserViewModel
    {
        public int PersonId { get; set; }

        [Required(ErrorMessage = "Enter the first name.")]
        [StringLength(100)]
		[RegularExpression(Rules.PersonName, ErrorMessage = Rules.PersonNameMsg)]
		public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the last name.")]
        [StringLength(100)]
		[RegularExpression(Rules.PersonName, ErrorMessage = Rules.PersonNameMsg)]
		public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter an email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(Rules.Phone, ErrorMessage = Rules.PhoneMsg)]
        public string? PhoneNumber { get; set; }

        [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters.")]
        public string? NewPassword { get; set; }

        public string? Role { get; set; }
        public string? Username { get; set; }
    }

    public class AdminModuleRow
    {
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string LecturerName { get; set; } = string.Empty;
        public int Students { get; set; }
        public int Classes { get; set; }
    }

    public class AddModuleViewModel
    {
        [Required(ErrorMessage = "Enter the module code.")]
        [StringLength(20)]
		[RegularExpression(Rules.ModuleCode, ErrorMessage = Rules.ModuleCodeMsg)]
		public string ModuleCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the module name.")]
        [StringLength(100)]
		[RegularExpression(Rules.Title, ErrorMessage = Rules.TitleMsg)]
		public string ModuleName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose a lecturer.")]
        public int? LecturerId { get; set; }

        public List<SelectListItem> Lecturers { get; set; } = new();
    }

    public class EnrollViewModel
    {
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public List<EnrollStudentItem> Students { get; set; } = new();
    }

    public class EnrollStudentItem
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool Enrolled { get; set; }
    }

    public class AdminVenueRow
    {
        public int VenueId { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal AllowedRadius { get; set; }
        public int Classes { get; set; }
    }

    public class VenueFormViewModel
    {
        public int VenueId { get; set; }

        [Required(ErrorMessage = "Enter the venue name.")]
        [StringLength(100)]
		[RegularExpression(Rules.Title, ErrorMessage = Rules.TitleMsg)]
		public string VenueName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the latitude.")]
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
        public decimal? Latitude { get; set; }

        [Required(ErrorMessage = "Enter the longitude.")]
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
        public decimal? Longitude { get; set; }

        [Required(ErrorMessage = "Enter the allowed radius.")]
        [Range(5.0, 5000.0, ErrorMessage = "The radius must be between 5 and 5000 metres.")]
        public decimal? AllowedRadius { get; set; }
    }

    public class AdminProfileViewModel
    {
        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [RegularExpression(Rules.Phone, ErrorMessage = Rules.PhoneMsg)]
        public string? PhoneNumber { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? OfficeNumber { get; set; }
        public string? InstitutionName { get; set; }
    }
}
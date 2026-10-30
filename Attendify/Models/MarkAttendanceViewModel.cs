namespace Attendify.Models
{
    public class MarkAttendanceViewModel
    {
        public int SessionId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public decimal AllowedRadius { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    public class SignInRequest
    {
        public int SessionId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
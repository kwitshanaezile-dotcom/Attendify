
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("ATTENDANCES")]
    public class Attendance
    {
        [Key]
        [Column("ATTENDANCE_ID")]
        public int AttendanceId { get; set; }

        [Column("SESSION_ID")]
        public int SessionId { get; set; }

        [Column("STUDENT_ID")]
        public int StudentId { get; set; }

        [Column("SIGNIN_TIME")]
        public DateTime? SigninTime { get; set; }

        [Column("SIGNOUT_TIME")]
        public DateTime? SignoutTime { get; set; }

        [Column("STATUS")]
        public string Status { get; set; } = string.Empty;

        [Column("STUDENT_LATITUDE")]
        public decimal? StudentLatitude { get; set; }

        [Column("STUDENT_LONGITUDE")]
        public decimal? StudentLongitude { get; set; }
    }
}
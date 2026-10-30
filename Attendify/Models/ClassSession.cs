
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("CLASS_SESSIONS")]
    public class ClassSession
    {
        [Key]
        [Column("SESSION_ID")]
        public int SessionId { get; set; }

        [Column("MODULE_ID")]
        public int ModuleId { get; set; }

        [Column("VENUE_ID")]
        public int VenueId { get; set; }

        [Column("SESSION_DAY")]
        public string SessionDay { get; set; } = string.Empty;

        [Column("SESSION_DATE")]
        public DateTime SessionDate { get; set; }

        [Column("START_TIME")]
        public TimeSpan StartTime { get; set; }

        [Column("END_TIME")]
        public TimeSpan EndTime { get; set; }

        [Column("SESSION_STATUS")]
        public string SessionStatus { get; set; } = string.Empty;

        [Column("IS_OPEN")]
        public bool IsOpen { get; set; }
    }
}
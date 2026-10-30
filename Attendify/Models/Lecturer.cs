
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("LECTURERS")]
    public class Lecturer
    {
        [Key]
        [Column("LECTURER_ID")]
        public int LecturerId { get; set; }

        [Column("STAFF_NUMBER")]
        public string StaffNumber { get; set; } = string.Empty;

        [Column("DEPARTMENT")]
        public string? Department { get; set; }

        [Column("INSTITUTION_ID")]
        public int InstitutionId { get; set; }

        [Column("PERSON_ID")]
        public int PersonId { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("ENROLLMENTS")]
    public class Enrollment
    {
        [Key]
        [Column("ENROLLMENT_ID")]
        public int EnrollmentId { get; set; }

        [Column("STUDENT_ID")]
        public int StudentId { get; set; }

        [Column("MODULE_ID")]
        public int ModuleId { get; set; }

        [Column("ENROLLMENT_DATE")]
        public DateTime EnrollmentDate { get; set; }
    }
}
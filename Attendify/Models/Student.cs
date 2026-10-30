
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("STUDENTS")]
    public class Student
    {
        [Key]
        [Column("STUDENT_ID")]
        public int StudentId { get; set; }

        [Column("STUDENT_NUMBER")]
        public string StudentNumber { get; set; } = string.Empty;

        [Column("QUALIFICATION")]
        public string? Qualification { get; set; }

        [Column("INSTITUTION_ID")]
        public int InstitutionId { get; set; }

        [Column("PERSON_ID")]
        public int PersonId { get; set; }
    }
}
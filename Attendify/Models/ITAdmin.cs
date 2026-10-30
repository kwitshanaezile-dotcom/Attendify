
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("IT_ADMINS")]
    public class ITAdmin
    {
        [Key]
        [Column("ADMIN_ID")]
        public int AdminId { get; set; }

        [Column("EMPLOYEE_NUMBER")]
        public string? EmployeeNumber { get; set; }

        [Column("INSTITUTION_ID")]
        public int InstitutionId { get; set; }

        [Column("OFFICE_NUMBER")]
        public string? OfficeNumber { get; set; }

        [Column("PERSON_ID")]
        public int PersonId { get; set; }
    }
}
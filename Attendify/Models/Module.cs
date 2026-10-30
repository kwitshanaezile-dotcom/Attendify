
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("MODULES")]
    public class Module
    {
        [Key]
        [Column("MODULE_ID")]
        public int ModuleId { get; set; }

        [Column("MODULE_CODE")]
        public string ModuleCode { get; set; } = string.Empty;

        [Column("MODULE_NAME")]
        public string ModuleName { get; set; } = string.Empty;

        [Column("LECTURER_ID")]
        public int LecturerId { get; set; }
    }
}
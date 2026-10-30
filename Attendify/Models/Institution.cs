
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("INSTITUTIONS")]
    public class Institution
    {
        [Key]
        [Column("INSTITUTION_ID")]
        public int InstitutionId { get; set; }

        [Column("INSTITUTION_NAME")]
        public string InstitutionName { get; set; } = string.Empty;

        [Column("INSTITUTION_TYPE")]
        public string InstitutionType { get; set; } = string.Empty;

        [Column("ADDRESS")]
        public string? Address { get; set; }

        [Column("TOWN")]
        public string? Town { get; set; }
    }
}
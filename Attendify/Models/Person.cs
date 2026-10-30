
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("PERSONS")]
    public class Person
    {
        [Key]
        [Column("PERSON_ID")]
        public int PersonId { get; set; }

        [Column("FIRST_NAME")]
        public string FirstName { get; set; } = string.Empty;

        [Column("LAST_NAME")]
        public string LastName { get; set; } = string.Empty;

        [Column("EMAIL")]
        public string Email { get; set; } = string.Empty;

        [Column("PHONE_NUMBER")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("USERNAME")]
        public string? Username { get; set; }

        [Column("PASSWORD_HASH")]
        public string? PasswordHash { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Attendify.Models
{
    [Table("VENUES")]
    public class Venue
    {
        [Key]
        [Column("VENUE_ID")]
        public int VenueId { get; set; }

        [Column("INSTITUTION_ID")]
        public int InstitutionId { get; set; }

        [Column("VENUE_NAME")]
        public string VenueName { get; set; } = string.Empty;

        [Column("LATITUDE")]
        public decimal Latitude { get; set; }

        [Column("LONGITUDE")]
        public decimal Longitude { get; set; }

        [Column("ALLOWED_RADIUS")]
        public decimal AllowedRadius { get; set; }
    }
}
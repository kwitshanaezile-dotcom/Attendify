using System.ComponentModel.DataAnnotations;

namespace Attendify.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Enter your username or email.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
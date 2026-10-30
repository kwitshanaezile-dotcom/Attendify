namespace Attendify.Models
{
    public static class Rules
    {
        public const string PersonName = @"^\s*[A-Za-zÀ-ÖØ-öø-ÿ]+([ '\-][A-Za-zÀ-ÖØ-öø-ÿ]+)*\s*$";
        public const string PersonNameMsg = "Use letters only. Numbers and special characters are not allowed.";

        public const string Phone = @"^\s*\+?[0-9]{10,13}\s*$";
        public const string PhoneMsg = "Enter digits only (10 to 13), for example 0712345678 or +27712345678.";

        public const string Number = @"^\s*[0-9]{5,12}\s*$";
        public const string NumberMsg = "Use digits only (5 to 12 digits). Letters and special characters are not allowed.";

        public const string Username = @"^\s*[A-Za-z0-9._@\-]+\s*$";
        public const string UsernameMsg = "Use letters, numbers and . _ - @ only, with no spaces.";
        
        public const string ModuleCode = @"^\s*[A-Za-z]{2,6}[0-9]{1,4}\s*$";
        public const string ModuleCodeMsg = "Use letters followed by numbers, for example TP301.";

        public const string Title = @"^\s*[A-Za-zÀ-ÖØ-öø-ÿ][A-Za-z0-9À-ÖØ-öø-ÿ &'\-(),]*\s*$";
        public const string TitleMsg = "Start with a letter. Only letters, numbers, spaces and & ' - ( ) , are allowed.";

        public const string Text = @"^\s*[A-Za-zÀ-ÖØ-öø-ÿ][A-Za-zÀ-ÖØ-öø-ÿ ,:&'\-().]*\s*$";
        public const string TextMsg = "Start with a letter. Only letters, spaces and & ' - ( ) , : . are allowed. No numbers.";
    }
}
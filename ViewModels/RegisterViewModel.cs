using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Use only letters, numbers, dots, underscores, or hyphens.")]
    [Display(Name = "Username (Login)")]
    public string UserName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [RegularExpression(@"^[A-Za-z0-9._%+-]+@([Gg][Mm][Aa][Ii][Ll]\.[Cc][Oo][Mm]|[Uu][Kk][Rr]\.[Nn][Ee][Tt])$",
        ErrorMessage = "Only Gmail (@gmail.com) and Ukr.net (@ukr.net) addresses are allowed.")]
    [Display(Name = "Email (optional)")]
    public string? Email { get; set; }

    [Required, DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[A-Za-z]).{8,}$",
        ErrorMessage = "Password must be at least 8 characters long and contain at least one letter.")]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.ViewModels;

public class EditProfileViewModel
{
    [StringLength(100)]
    [Display(Name = "First name")]
    public string? FirstName { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [StringLength(100)]
    [Display(Name = "Last name")]
    public string? LastName { get; set; }

    [RegularExpression(@"^380\d{9}$", ErrorMessage = "Enter a valid Ukrainian phone number in the format 380XXXXXXXXX.")]
    [StringLength(12)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Make my favorites list public")]
    public bool IsFavoritesPublic { get; set; }
}

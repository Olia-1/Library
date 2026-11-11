using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.ViewModels;

public class EditProfileViewModel
{
    // Nullable reference types prevent MVC from adding implicit [Required] validation.
    // Empty values are stored as empty strings in the existing non-nullable User columns.
    [StringLength(100)]
    [Display(Name = "First name")]
    public string? FirstName { get; set; }

    [StringLength(100)]
    [Display(Name = "Last name")]
    public string? LastName { get; set; }

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(30)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Make my favorites list public")]
    public bool IsFavoritesPublic { get; set; }
}

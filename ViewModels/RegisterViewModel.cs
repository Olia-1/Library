using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.ViewModels;

public class RegisterViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Ім'я")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Прізвище")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "Електронна пошта")]
    public string Email { get; set; } = string.Empty;

    [Required, Phone]
    [Display(Name = "Номер телефону")]
    public string Phone { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    [Display(Name = "Підтвердження пароля")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

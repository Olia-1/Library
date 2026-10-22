using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Електронна пошта")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Запам'ятати мене")]
    public bool RememberMe { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class AuthorTranslation
{
    public int AuthorTranslationId { get; set; }

    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    [Required, StringLength(5)]
    public string LanguageCode { get; set; } = "en";

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public string? Biography { get; set; }
}
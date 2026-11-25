using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace PrivateLibrary.Models;

public class Author
{
    public int AuthorId { get; set; }

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public string? Biography { get; set; }
    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public ICollection<AuthorTranslation> Translations { get; set; }= new List<AuthorTranslation>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DisplayFullName => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "uk"
        ? Translations.FirstOrDefault(t => t.LanguageCode == "uk") is { } translation
            ? $"{translation.FirstName} {translation.LastName}".Trim()
            : $"{FirstName} {LastName}".Trim()
        : $"{FirstName} {LastName}".Trim();
}

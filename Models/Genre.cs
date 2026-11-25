using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace PrivateLibrary.Models;

public class Genre
{
    public int GenreId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
    public ICollection<GenreTranslation> Translations { get; set; } = new List<GenreTranslation>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DisplayName => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "uk"
        ? Translations.FirstOrDefault(t => t.LanguageCode == "uk")?.Name ?? Name
        : Name;
}

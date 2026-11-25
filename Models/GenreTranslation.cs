using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class GenreTranslation
{
    public int GenreTranslationId { get; set; }

    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;

    [Required, StringLength(5)]
    public string LanguageCode { get; set; } = "en";

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class BookTranslation
{
    public int BookTranslationId { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    [Required, StringLength(5)]
    public string LanguageCode { get; set; } = "en";

    [Required, StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public sealed class AuthorNamesValidationAttribute : ValidationAttribute
{
    private static readonly Regex ValidName = new(
        @"^\p{L}[\p{L}\p{M}'’\-]*(?:\s+\p{L}[\p{L}\p{M}'’\-]*)+$",
        RegexOptions.Compiled);

    public AuthorNamesValidationAttribute()
    {
        ErrorMessage = "Enter each author as a first name and last name on a separate line. Use letters, apostrophes or hyphens only.";
    }

    protected override ValidationResult? IsValid(
        object? value,
        ValidationContext validationContext)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return ValidationResult.Success;

        foreach (var rawLine in Regex.Split(text.Trim(), @"\r\n|\r|\n"))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
                return new ValidationResult("Remove empty lines between authors.");

            if (!ValidName.IsMatch(line))
                return new ValidationResult(ErrorMessage);

            var parts = Regex.Split(line, @"\s+");

            if (parts[0].Length > 100 ||
                string.Join(" ", parts.Skip(1)).Length > 100)
            {
                return new ValidationResult(
                    "An author's first name and last name must not exceed 100 characters each.");
            }
        }

        return ValidationResult.Success;
    }
}

public class AdminBookViewModel : IValidatableObject
{
    public int BookId { get; set; }

    // English
    [Required(ErrorMessage = "The English title is required.")]
    [StringLength(255)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(5000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Enter at least one author in English.")]
    [StringLength(1000)]
    [AuthorNamesValidation]
    [Display(Name = "Authors (one per line)")]
    public string AuthorNames { get; set; } = string.Empty;

    // Ukrainian
    [Required(ErrorMessage = "The Ukrainian title is required.")]
    [StringLength(255)]
    [Display(Name = "Назва")]
    public string TitleUk { get; set; } = string.Empty;

    [StringLength(5000)]
    [Display(Name = "Опис")]
    public string? DescriptionUk { get; set; }

    [Required(ErrorMessage = "Вкажіть принаймні одного автора українською.")]
    [StringLength(1000)]
    [AuthorNamesValidation]
    [Display(Name = "Автори (кожен з нового рядка)")]
    public string AuthorNamesUk { get; set; } = string.Empty;

    // Shared book information
    [StringLength(20)]
    public string? ISBN { get; set; }

    [Range(0, 2100)]
    public short? PublicationYear { get; set; }

    [StringLength(255)]
    public string? Publisher { get; set; }

    [StringLength(100)]
    public string? Language { get; set; }

    [StringLength(2048)]
    [Url]
    public string? CoverImageUrl { get; set; }

    public int? GenreId { get; set; }

    [Range(0, 1000)]
    [Display(Name = "Initial number of copies")]
    public int InitialCopies { get; set; } = 1;

    // Remembers which language panel was open after validation.
    // This property is not stored in the database.
    public string ActiveLanguage { get; set; } = "en";

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        var englishAuthors = GetAuthorLines(AuthorNames);
        var ukrainianAuthors = GetAuthorLines(AuthorNamesUk);

        if (englishAuthors.Count > 0 &&
            ukrainianAuthors.Count > 0 &&
            englishAuthors.Count != ukrainianAuthors.Count)
        {
            yield return new ValidationResult(
                "The number of English and Ukrainian authors must be the same. Keep the same order in both language panels.",
                new[] { nameof(AuthorNames), nameof(AuthorNamesUk) });
        }

        if (englishAuthors.Count > 0 &&
            englishAuthors.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != englishAuthors.Count)
        {
            yield return new ValidationResult(
                "The same English author must not be entered more than once.",
                new[] { nameof(AuthorNames) });
        }
    }

    private static List<string> GetAuthorLines(string? value) =>
        (value ?? string.Empty)
            .Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
}

public class AdminBookManagementViewModel
{
    public string? Search { get; set; }
    public string Status { get; set; } = "active";
    public int? GenreId { get; set; }
    public string Sort { get; set; } = "title";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int ArchivedCount { get; set; }

    public List<Genre> Genres { get; set; } = new();
    public List<AdminBookListItemViewModel> Books { get; set; } = new();

    public int TotalPages =>
        Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public class AdminBookListItemViewModel
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ISBN { get; set; }
    public string? Publisher { get; set; }
    public short? PublicationYear { get; set; }
    public string GenreName { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public bool IsArchived { get; set; }
}

public class BookCopyManagementViewModel
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public List<BookCopyItemViewModel> Copies { get; set; } = new();
}

public class BookCopyItemViewModel
{
    public int CopyId { get; set; }
    public string InventoryNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public string? Location { get; set; }
    public int LoanCount { get; set; }
    public bool CanDelete { get; set; }
}

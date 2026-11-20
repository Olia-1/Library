using System.ComponentModel.DataAnnotations;
using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class AdminBookViewModel
{
    public int BookId { get; set; }

    [Required, StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [StringLength(5000)]
    public string? Description { get; set; }

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

    [StringLength(1000)]
    [Display(Name = "Authors (one per line)")]
    public string? AuthorNames { get; set; }

    [Range(0, 1000)]
    [Display(Name = "Initial number of copies")]
    public int InitialCopies { get; set; } = 1;
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
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
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

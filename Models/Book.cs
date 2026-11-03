using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Book
{
    public int BookId { get; set; }

    [Required, StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(20)]
    public string? ISBN { get; set; }

    public short? PublicationYear { get; set; }

    [StringLength(255)]
    public string? Publisher { get; set; }

    [StringLength(100)]
    public string? Language { get; set; }

    [StringLength(2048)]
    public string? CoverImageUrl { get; set; }

    public int? GenreId { get; set; }
    public Genre? Genre { get; set; }

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}

using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Book
{
    [Key]
    public int BookId { get; set; }

    [Required, StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, 3000)]
    public int PublicationYear { get; set; }

    public int GenreId { get; set; }
    public Genre? Genre { get; set; }

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Genre
{
    public int GenreId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

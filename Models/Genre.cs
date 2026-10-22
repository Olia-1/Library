using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Genre
{
    [Key]
    public int GenreId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public ICollection<Book> Books { get; set; } = new List<Book>();
}

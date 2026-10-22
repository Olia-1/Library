using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class BookCopy
{
    [Key]
    public int CopyId { get; set; }

    public int BookId { get; set; }
    public Book? Book { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "available";

    [StringLength(100)]
    public string? Condition { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}

using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class BookCopy
{
    public int CopyId { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    [Required, StringLength(50)]
    public string InventoryNumber { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Status { get; set; } = "Available";

    [StringLength(50)]
    public string? Condition { get; set; }

    [StringLength(255)]
    public string? Location { get; set; }

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}

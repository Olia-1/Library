using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Loan
{
    [Key]
    public int LoanId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int CopyId { get; set; }
    public BookCopy? Copy { get; set; }

    public int LibrarianId { get; set; }
    public User? Librarian { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "active";
}

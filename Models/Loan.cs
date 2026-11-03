using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Loan
{
    public int LoanId { get; set; }

    public int CopyId { get; set; }
    public BookCopy Copy { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int LibrarianId { get; set; }
    public User Librarian { get; set; } = null!;

    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Active";
}

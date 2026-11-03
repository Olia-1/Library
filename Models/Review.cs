using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Review
{
    public int ReviewId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    [Range(1, 5)]
    public byte Rating { get; set; }

    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required, StringLength(20)]
    public string ModerationStatus { get; set; } = "Pending";
}

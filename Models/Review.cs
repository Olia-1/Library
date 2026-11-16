using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Review
{
    public int ReviewId { get; set; }

    [Required]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    [Required, MinLength(10), MaxLength(5000)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [Required, StringLength(20)]
    public string ModerationStatus { get; set; } = "Pending";
}

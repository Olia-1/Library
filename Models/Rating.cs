using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Rating
{
    public int RatingId { get; set; }

    [Required]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    [Range(1, 5)]
    public byte Value { get; set; }

    [StringLength(30)]
    public string? Text { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required, StringLength(20)]
    public string ModerationStatus { get; set; } = "Pending";
}

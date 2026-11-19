namespace PrivateLibrary.Models;

/// <summary>A reader's like on a published review. One like per reader and review.</summary>
public class ReviewLike
{
    public int ReviewId { get; set; }
    public Review Review { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

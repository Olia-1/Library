using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    [StringLength(30)]
    public string Type { get; set; } = "General";

    [StringLength(30)]
    public string? RelatedEntityType { get; set; }

    [StringLength(2048)]
    public string? ActionUrl { get; set; }

    public int? RelatedEntityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

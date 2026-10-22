using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Notification
{
    [Key]
    public int NotificationId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    [Required, StringLength(50)]
    public string Type { get; set; } = "general";

    [Required, StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }

    public bool SendByEmail { get; set; }
    [StringLength(20)]
    public string EmailStatus { get; set; } = "NotRequired";
    public DateTime? EmailSentAt { get; set; }
    [StringLength(1000)]
    public string? EmailError { get; set; }
}

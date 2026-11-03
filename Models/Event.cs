using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Event
{
    public int EventId { get; set; }

    public int CreatedBy { get; set; }
    public User Creator { get; set; } = null!;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public DateTime EventDate { get; set; }

    [StringLength(255)]
    public string? Location { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxParticipants { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required, StringLength(20)]
    public string Status { get; set; } = "Planned";

    [Required, StringLength(20)]
    public string ModerationStatus { get; set; } = "Pending";

    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
}

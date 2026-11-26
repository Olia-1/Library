using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    [StringLength(2048)]
    public string? ConferenceUrl { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxParticipants { get; set; }

    [Range(30, 1440)]
    public int DurationMinutes { get; set; } = 60;

    [NotMapped]
    public string DurationLabel
    {
        get
        {
            var hours = DurationMinutes / 60;
            var minutes = DurationMinutes % 60;
            if (hours == 0) return $"{minutes} min";
            if (minutes == 0) return hours == 1 ? "1 hour" : $"{hours} hours";
            return $"{hours}h {minutes}min";
        }
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required, StringLength(20)]
    public string Status { get; set; } = "Planned";

    [StringLength(20)]
    public string? ModerationStatus { get; set; } = "Pending";

    public bool IsVisible { get; set; } = true;

    public ICollection<EventParticipant> Participants { get; set; }
        = new List<EventParticipant>();
}

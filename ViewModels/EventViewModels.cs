using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.ViewModels;

public class EventsIndexViewModel
{
    public List<EventListItemViewModel> Events { get; set; } = new();
    public string Filter { get; set; } = "upcoming";
    public bool CanCreateEvents { get; set; }
    public string? CreateEventMessage { get; set; }
}

public class EventListItemViewModel
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public int DurationMinutes { get; set; }
    public string DurationLabel
    {
        get
        {
            var hours = DurationMinutes / 60;
            var minutes = DurationMinutes % 60;
            if (hours == 0) return $"{minutes} min";
            if (minutes == 0) return $"{hours} hr";
            return $"{hours} hr {minutes} min";
        }
    }
    public string? Location { get; set; }
    public int MaxParticipants { get; set; }
    public int RegisteredCount { get; set; }
    public bool IsRegistered { get; set; }
    public bool IsOrganizer { get; set; }
    public bool CanJoinEvents { get; set; }
    public string? JoinEventMessage { get; set; }
    public string ModerationStatus { get; set; } = "Approved";
    public string OrganizerName { get; set; } = "Library member";
    public int AvailablePlaces => Math.Max(0, MaxParticipants - RegisteredCount);
}

public class EventDetailsViewModel : EventListItemViewModel
{
}


public class CreateEventViewModel
{
    [Required(ErrorMessage = "Event title is required.")]
    [StringLength(200)]
    [Display(Name = "Event title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Event date and time are required.")]
    [Display(Name = "Date and time")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Choose where the event will take place.")]
    [StringLength(20)]
    [RegularExpression("^(In Library|Online)$", ErrorMessage = "Choose In Library or Online.")]
    [Display(Name = "Location")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Event duration is required.")]
    [Range(30, 1440, ErrorMessage = "Duration must be between 30 minutes and 24 hours.")]
    [Display(Name = "Duration")]
    public int? DurationMinutes { get; set; }

    [Range(1, 10000, ErrorMessage = "Maximum participants must be between 1 and 10,000.") ]
    [Display(Name = "Maximum participants")]
    public int MaxParticipants { get; set; } = 20;
}

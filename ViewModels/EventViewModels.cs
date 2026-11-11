namespace PrivateLibrary.ViewModels;

public class EventsIndexViewModel
{
    public List<EventListItemViewModel> Events { get; set; } = new();
    public string Filter { get; set; } = "upcoming";
}

public class EventListItemViewModel
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string? Location { get; set; }
    public int MaxParticipants { get; set; }
    public int RegisteredCount { get; set; }
    public bool IsRegistered { get; set; }
    public bool IsOrganizer { get; set; }
    public string ModerationStatus { get; set; } = "Approved";
    public int AvailablePlaces => Math.Max(0, MaxParticipants - RegisteredCount);
}

public class EventDetailsViewModel : EventListItemViewModel
{
}

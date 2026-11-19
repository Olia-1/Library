using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class NotificationsViewModel
{
    public List<Notification> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
    public int NextSkip { get; set; }
}

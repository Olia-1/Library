using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class User : IdentityUser<int>
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public bool IsFavoritesPublic { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public ICollection<EventParticipant> EventParticipations { get; set; } = new List<EventParticipant>();

    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();
  }

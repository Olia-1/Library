using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Reservation
{
    public int ReservationId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public DateTime ReservationDate { get; set; } = DateTime.UtcNow;
    public int? QueuePosition { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime? AvailableFrom { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? CompletedAt { get; set; }
}

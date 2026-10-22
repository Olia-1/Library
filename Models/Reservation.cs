using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Reservation
{
    [Key]
    public int ReservationId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int BookId { get; set; }
    public Book? Book { get; set; }

    public DateTime ReservationDate { get; set; } = DateTime.UtcNow;

    [Required, StringLength(20)]
    public string Status { get; set; } = "active";
}

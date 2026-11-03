using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrivateLibrary.Models;

public class Payment
{
    public int PaymentId { get; set; }

    public int SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;

    [Column(TypeName = "decimal(10,2)")]
    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    public string? PaymentMethod { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";

    [StringLength(255)]
    public string? TransactionReference { get; set; }
}

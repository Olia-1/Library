using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Subscription
{
    public int SubscriptionId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int PlanId { get; set; }
    public MembershipPlan MembershipPlan { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Active";

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

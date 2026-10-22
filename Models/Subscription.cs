using System.ComponentModel.DataAnnotations;

namespace PrivateLibrary.Models;

public class Subscription
{
    [Key]
    public int SubscriptionId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int PlanId { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Active";

    public bool IsTrial { get; set; }
}

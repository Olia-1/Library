using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class MembershipPlansViewModel
{
    public List<MembershipPlan> Plans { get; set; } = new();
    public int TrialPlanId { get; set; }
    public bool CanStartTrial { get; set; }
    public Subscription? ActiveSubscription { get; set; }
    public List<Payment> RecentPayments { get; set; } = new();
    public List<Payment> PurchasedMemberships { get; set; } = new();
    public int TotalPurchasedDays { get; set; }
    public DateTime? MembershipValidUntil { get; set; }
}

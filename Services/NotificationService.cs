using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;

namespace PrivateLibrary.Services;

/// <summary>Creates in-app notifications exclusively for Reader accounts.</summary>
public class NotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task NotifyUserAsync(int userId, string title, string message, string type,
        string? relatedEntityType = null, int? relatedEntityId = null, string? deduplicationKey = null)
    {
        var isReader = await (from user in _context.Users
                              join userRole in _context.UserRoles on user.Id equals userRole.UserId
                              join role in _context.Roles on userRole.RoleId equals role.Id
                              where user.Id == userId && user.IsActive && role.Name == "Reader"
                              select user.Id).AnyAsync();
        if (!isReader) return;

        if (deduplicationKey is not null)
        {
            var exists = await _context.Notifications.AnyAsync(n => n.UserId == userId &&
                n.Type == type && n.RelatedEntityType == relatedEntityType &&
                n.RelatedEntityId == relatedEntityId && n.Message == message);
            if (exists) return;
        }

        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
        await _context.SaveChangesAsync();
    }

    public async Task NotifyEventParticipantsAsync(int eventId, string title, string message)
    {
        var participantIds = await (from participant in _context.EventParticipants
                                    join user in _context.Users on participant.UserId equals user.Id
                                    join userRole in _context.UserRoles on user.Id equals userRole.UserId
                                    join role in _context.Roles on userRole.RoleId equals role.Id
                                    where participant.EventId == eventId &&
                                          participant.Status == "Registered" &&
                                          user.IsActive && role.Name == "Reader"
                                    select user.Id).Distinct().ToListAsync();
        foreach (var userId in participantIds)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = "Event",
                RelatedEntityType = "Event",
                RelatedEntityId = eventId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }
        if (participantIds.Count > 0) await _context.SaveChangesAsync();
    }
    /// <summary>
    /// Creates a Reader notification after a subscription payment has been confirmed.
    /// Call this only after the payment status has been saved as successful.
    /// </summary>
    public async Task NotifySubscriptionPaymentSucceededAsync(int subscriptionId)
    {
        var subscription = await _context.Subscriptions
            .AsNoTracking()
            .Include(s => s.MembershipPlan)
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

        if (subscription is null) return;

        var payment = await _context.Payments
            .AsNoTracking()
            .Where(p => p.SubscriptionId == subscriptionId &&
                        (p.Status == "Paid" || p.Status == "Succeeded" || p.Status == "Completed"))
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefaultAsync();

        if (payment is null) return;

        await NotifyUserAsync(
            subscription.UserId,
            "Membership payment successful",
            $"Your payment for the {subscription.MembershipPlan.Name} membership plan was successful. Your membership is active until {subscription.EndDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB"))}.",
            "Membership",
            "Subscription",
            subscription.SubscriptionId,
            $"MembershipPayment:{payment.PaymentId}");
    }

}

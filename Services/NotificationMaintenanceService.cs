using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;

namespace PrivateLibrary.Services;

/// <summary>Creates time-based reminders once per loan/subscription.</summary>
public class NotificationMaintenanceService
{
    private readonly ApplicationDbContext _context;

    public NotificationMaintenanceService(ApplicationDbContext context) => _context = context;

    public async Task CreateDailyRemindersAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var reminderLimit = today.AddDays(3);

        // Only active Reader accounts receive personal notifications.
        var readerIds = from user in _context.Users
                        join userRole in _context.UserRoles on user.Id equals userRole.UserId
                        join role in _context.Roles on userRole.RoleId equals role.Id
                        where user.IsActive && role.Name == "Reader"
                        select user.Id;

        var loans = await _context.Loans.AsNoTracking()
            .Include(l => l.Copy).ThenInclude(c => c.Book)
            .Where(l => readerIds.Contains(l.UserId) &&
                        l.ReturnDate == null && l.Status == "Active" &&
                        l.DueDate < reminderLimit.AddDays(1))
            .ToListAsync(cancellationToken);

        var englishCulture = CultureInfo.GetCultureInfo("en-GB");

        foreach (var loan in loans)
        {
            var overdue = loan.DueDate.Date < today;
            var dueToday = loan.DueDate.Date == today;
            var dueInThreeDays = loan.DueDate.Date == today.AddDays(3);
            if (!overdue && !dueToday && !dueInThreeDays) continue;

            var type = overdue ? "LoanOverdue" : dueToday ? "LoanDueToday" : "LoanDueSoon";
            var title = overdue ? "Book return overdue"
                : dueToday ? "Book return due today"
                : "Book return reminder";

            var exists = await _context.Notifications.AnyAsync(n =>
                n.UserId == loan.UserId &&
                n.Type == type &&
                n.RelatedEntityType == "Loan" &&
                n.RelatedEntityId == loan.LoanId,
                cancellationToken);
            if (exists) continue;

            var message = overdue
                ? $"The return date for “{loan.Copy.Book.Title}” was {loan.DueDate.ToString("dd MMM yyyy", englishCulture)}. Please return it as soon as possible."
                : dueToday
                    ? $"Today is the return date for “{loan.Copy.Book.Title}”. Please return the book by the end of the day."
                    : $"The book “{loan.Copy.Book.Title}” is due in 3 days, on {loan.DueDate.ToString("dd MMM yyyy", englishCulture)}. Please return it on time.";

            _context.Notifications.Add(new Notification
            {
                UserId = loan.UserId,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityType = "Loan",
                RelatedEntityId = loan.LoanId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        var subscriptions = await _context.Subscriptions.AsNoTracking()
            .Include(s => s.MembershipPlan)
            .Where(s => readerIds.Contains(s.UserId) &&
                        s.Status == "Active" &&
                        s.EndDate > DateTime.UtcNow &&
                        s.EndDate <= DateTime.UtcNow.AddDays(3))
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            var exists = await _context.Notifications.AnyAsync(n =>
                n.UserId == subscription.UserId &&
                n.Type == "MembershipExpiring" &&
                n.RelatedEntityType == "Subscription" &&
                n.RelatedEntityId == subscription.SubscriptionId,
                cancellationToken);
            if (exists) continue;

            _context.Notifications.Add(new Notification
            {
                UserId = subscription.UserId,
                Title = "Membership expiring soon",
                Message = $"Your {subscription.MembershipPlan.Name} membership expires on {subscription.EndDate.ToString("dd MMM yyyy", englishCulture)}. Renew it to continue using membership features.",
                Type = "MembershipExpiring",
                RelatedEntityType = "Subscription",
                RelatedEntityId = subscription.SubscriptionId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(cancellationToken);
    }
}

public class NotificationReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationReminderWorker> _logger;

    public NotificationReminderWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<NotificationMaintenanceService>();
                await maintenance.CreateDailyRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Failed to create scheduled library notifications."); }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}

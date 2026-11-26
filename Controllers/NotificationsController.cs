using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Reader")]
public class NotificationsController : Controller
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _context;

    public NotificationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string category = "all", string status = "all")
    {
        category = NormalizeCategory(category);
        status = NormalizeStatus(status);
        var userId = GetCurrentUserId();
        var query = BuildQuery(userId, category, status);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Take(PageSize + 1)
            .ToListAsync();

        var hasMore = notifications.Count > PageSize;
        if (hasMore) notifications.RemoveAt(notifications.Count - 1);

        var model = new NotificationsViewModel
        {
            Notifications = notifications,
            UnreadCount = await BuildQuery(userId, category, "unread").CountAsync(),
            TotalCount = await BuildQuery(userId, category, status).CountAsync(),
            HasMore = hasMore,
            NextSkip = notifications.Count
        };

        ViewBag.Category = category;
        ViewBag.Status = status;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LoadMore(int skip, string category = "all", string status = "all")
    {
        if (skip < 0) return BadRequest();
        category = NormalizeCategory(category);
        status = NormalizeStatus(status);
        var userId = GetCurrentUserId();
        var notifications = await BuildQuery(userId, category, status)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip(skip)
            .Take(PageSize + 1)
            .ToListAsync();

        var hasMore = notifications.Count > PageSize;
        if (hasMore) notifications.RemoveAt(notifications.Count - 1);

        return PartialView("_NotificationItems", new NotificationsViewModel
        {
            Notifications = notifications,
            HasMore = hasMore,
            NextSkip = skip + notifications.Count
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, string category = "all", string status = "all")
    {
        var userId = GetCurrentUserId();
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

        if (notification is null) return NotFound();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        if (notification.RelatedEntityType == "Loan" || notification.Type.StartsWith("Loan", StringComparison.OrdinalIgnoreCase))
            return Redirect((Url.Action("Index", "Account") ?? "/Account") + "#my-loans");

        if (notification.RelatedEntityType == "Subscription")
            return Redirect((Url.Action("Index", "Account") ?? "/Account") + "#profile");

        var destination = GetSafeDestination(notification);
        if (destination is not null)
            return RedirectToAction(destination.Value.Action, destination.Value.Controller, destination.Value.RouteValues);

        return RedirectToAction(nameof(Index), new { category = NormalizeCategory(category), status = NormalizeStatus(status) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(string category = "all", string status = "all")
    {
        category = NormalizeCategory(category);
        var userId = GetCurrentUserId();
        var unread = await BuildQuery(userId, category, "unread").ToListAsync();

        foreach (var notification in unread)
            notification.IsRead = true;

        if (unread.Count > 0)
            await _context.SaveChangesAsync();

        TempData["Success"] = "All notifications have been marked as read.";
        return RedirectToAction(nameof(Index), new { category, status = NormalizeStatus(status) });
    }

    private IQueryable<Notification> BuildQuery(int userId, string category, string status)
    {
        var query = _context.Notifications.Where(n => n.UserId == userId);

        query = category switch
        {
            "reviews" => query.Where(n => n.Type == "Review" || n.Type == "Rating"),
            "events" => query.Where(n => n.Type == "Event"),
            "reservations" => query.Where(n =>
                n.Type == "Reservation" ||
                n.Type == "Loan" ||
                n.Type == "LoanDueSoon" ||
                n.Type == "LoanDueToday" ||
                n.Type == "LoanOverdue" ||
                n.Type == "LoanReturned"),
            "membership" => query.Where(n => n.Type == "Membership" || n.Type == "MembershipExpiring" || n.Type == "Payment"),
            "account" => query.Where(n => n.Type == "Account" || n.Type == "Moderation"),
            _ => query
        };

        query = status switch
        {
            "unread" => query.Where(n => !n.IsRead),
            "read" => query.Where(n => n.IsRead),
            _ => query
        };

        return query;
    }

    private static string NormalizeCategory(string? category)
    {
        var value = category?.Trim().ToLowerInvariant();
        return value is "reviews" or "events" or "reservations" or "membership" or "account" ? value : "all";
    }

    private static string NormalizeStatus(string? status)
    {
        var value = status?.Trim().ToLowerInvariant();
        return value is "unread" or "read" ? value : "all";
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private (string Controller, string Action, object? RouteValues)? GetSafeDestination(Notification notification)
    {
        if (notification.RelatedEntityId is not int id || id <= 0)
            return null;

        return notification.Type switch
        {
            _ => notification.RelatedEntityType switch
            {
                "Event" => ("Events", "Details", new { id }),
                "Book" => ("Books", "Details", new { id }),
                "Reservation" => ("Reservations", "Index", null),
                "Loan" or "Subscription" => ("Account", "Index", null),
                _ => null
            }
        };
    }
}

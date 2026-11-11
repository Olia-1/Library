using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public EventsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // Readers can browse approved events without signing in.
    [AllowAnonymous]
    public async Task<IActionResult> Index(string filter = "upcoming")
    {
        var now = DateTime.Now;
        var user = await _userManager.GetUserAsync(User);
        var currentUserId = user?.Id;

        // Everyone can see approved events. A signed-in user can also see
        // their own events while they are awaiting moderation.
        var query = _context.Events
            .AsNoTracking()
            .Where(e => e.Status != "Cancelled" &&
                (e.ModerationStatus == "Approved" ||
                 (currentUserId.HasValue && e.CreatedBy == currentUserId.Value)));

        query = filter == "past"
            ? query.Where(e => e.EventDate < now).OrderByDescending(e => e.EventDate)
            : query.Where(e => e.EventDate >= now).OrderBy(e => e.EventDate);

        var events = await query
            .Select(e => new EventListItemViewModel
            {
                EventId = e.EventId,
                Title = e.Title,
                Description = e.Description,
                EventDate = e.EventDate,
                Location = e.Location,
                MaxParticipants = e.MaxParticipants,
                RegisteredCount = e.Participants.Count(p => p.Status == "Registered"),
                IsOrganizer = currentUserId.HasValue && e.CreatedBy == currentUserId.Value,
                ModerationStatus = e.ModerationStatus
            })
            .ToListAsync();

        if (user != null)
        {
            var ids = await _context.EventParticipants
                .Where(p => p.UserId == user.Id && p.Status == "Registered")
                .Select(p => p.EventId)
                .ToListAsync();
            foreach (var item in events)
                item.IsRegistered = ids.Contains(item.EventId);
        }

        return View(new EventsIndexViewModel { Events = events, Filter = filter });
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var currentUserId = user?.Id;
        var item = await _context.Events
            .AsNoTracking()
            .Where(e => e.EventId == id && e.Status != "Cancelled" &&
                        (e.ModerationStatus == "Approved" ||
                         (currentUserId.HasValue && e.CreatedBy == currentUserId.Value)))
            .Select(e => new EventDetailsViewModel
            {
                EventId = e.EventId,
                Title = e.Title,
                Description = e.Description,
                EventDate = e.EventDate,
                Location = e.Location,
                MaxParticipants = e.MaxParticipants,
                RegisteredCount = e.Participants.Count(p => p.Status == "Registered"),
                IsRegistered = user != null && e.Participants.Any(p =>
                    p.UserId == user.Id && p.Status == "Registered"),
                IsOrganizer = currentUserId.HasValue && e.CreatedBy == currentUserId.Value,
                ModerationStatus = e.ModerationStatus
            })
            .FirstOrDefaultAsync();

        if (item == null) return NotFound();
        return View(item);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var libraryEvent = await _context.Events
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.EventId == id &&
                e.ModerationStatus == "Approved" &&
                e.Status != "Cancelled");

        if (libraryEvent == null) return NotFound();

        // Organizers manage their own events and cannot register as participants.
        if (libraryEvent.CreatedBy == user.Id)
            return RedirectToAction(nameof(Details), new { id });

        if (libraryEvent.EventDate <= DateTime.Now)
            return RedirectToAction(nameof(Details), new { id, message = "closed" });

        var existing = libraryEvent.Participants
            .FirstOrDefault(p => p.UserId == user.Id);

        if (existing?.Status == "Registered")
            return RedirectToAction(nameof(Details), new { id, message = "already" });

        var registeredCount = libraryEvent.Participants.Count(p => p.Status == "Registered");
        if (registeredCount >= libraryEvent.MaxParticipants)
            return RedirectToAction(nameof(Details), new { id, message = "full" });

        if (existing != null)
        {
            existing.Status = "Registered";
            existing.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            _context.EventParticipants.Add(new EventParticipant
            {
                EventId = id,
                UserId = user.Id,
                JoinedAt = DateTime.UtcNow,
                Status = "Registered"
            });
        }

        await _context.SaveChangesAsync();
        TempData["EventMessage"] = "You have successfully registered for this event.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var libraryEvent = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == id);
        if (libraryEvent == null) return NotFound();

        // An event organizer is not a participant and cannot cancel participation.
        if (libraryEvent.CreatedBy == user.Id)
            return RedirectToAction(nameof(Details), new { id });

        var participation = await _context.EventParticipants
            .FirstOrDefaultAsync(p => p.EventId == id && p.UserId == user.Id);

        if (participation == null || participation.Status != "Registered")
            return RedirectToAction(nameof(Details), new { id });

        participation.Status = "Cancelled";
        await _context.SaveChangesAsync();

        TempData["EventMessage"] = "Your registration has been cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize]
    public async Task<IActionResult> MyEvents()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Show both events created by the current user and events they joined.
        // The OR condition prevents an event from appearing twice.
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.Status != "Cancelled" &&
                (e.CreatedBy == user.Id || e.Participants.Any(p =>
                    p.UserId == user.Id && p.Status == "Registered")))
            .Select(e => new EventListItemViewModel
            {
                EventId = e.EventId,
                Title = e.Title,
                Description = e.Description,
                EventDate = e.EventDate,
                Location = e.Location,
                MaxParticipants = e.MaxParticipants,
                RegisteredCount = e.Participants.Count(x => x.Status == "Registered"),
                IsRegistered = e.Participants.Any(x =>
                    x.UserId == user.Id && x.Status == "Registered"),
                IsOrganizer = e.CreatedBy == user.Id,
                ModerationStatus = e.ModerationStatus
            })
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return View(events);
    }

}

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

    [AllowAnonymous]
    public async Task<IActionResult> Index(string filter = "upcoming")
    {
        var now = DateTime.Now;
        var user = await _userManager.GetUserAsync(User);
        var currentUserId = user?.Id;
        var (canCreateEvents, createEventMessage, _) = await GetEventCreationAccessAsync(user);

        var query = _context.Events
            .AsNoTracking()
            .Where(e => e.Status != "Cancelled" &&
                ((e.IsVisible && (e.ModerationStatus == "Approved" || e.ModerationStatus == null)) ||
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
                DurationMinutes = e.DurationMinutes,
                Location = e.Location ?? "To be announced",
                MaxParticipants = e.MaxParticipants,
                RegisteredCount = e.Participants.Count(p => p.Status == "Registered"),
                IsOrganizer = currentUserId.HasValue && e.CreatedBy == currentUserId.Value,
                ModerationStatus = e.ModerationStatus ?? "Approved",
                OrganizerName = e.Creator.UserName ?? "Library member"
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

        return View(new EventsIndexViewModel
        {
            Events = events,
            Filter = filter,
            CanCreateEvents = canCreateEvents,
            CreateEventMessage = createEventMessage
        });
    }

    [HttpGet]
    public async Task<IActionResult> AvailableTimes(DateTime date, int durationMinutes, string location = "In Library")
    {
        if (durationMinutes < 30 || durationMinutes > 1440 || durationMinutes % 30 != 0 ||
            (durationMinutes > 180 && location != "Online"))
            return BadRequest();

        var dayStart = date.Date;
        var now = DateTime.Now;
        var slots = new List<string>();

        if (location == "Online")
        {
            for (var time = TimeSpan.Zero; time < TimeSpan.FromDays(1); time += TimeSpan.FromMinutes(30))
            {
                var start = dayStart.Add(time);
                if (start > now)
                    slots.Add(start.ToString("HH:mm"));
            }
            return Json(slots);
        }

        if (location != "In Library")
            return BadRequest();

        var opening = dayStart.AddHours(9);
        var closing = dayStart.AddHours(20);
        var existingEvents = await _context.Events
            .AsNoTracking()
            .Where(e => e.Location == "In Library" &&
                        e.Status != "Cancelled" &&
                        (e.ModerationStatus == "Approved" || e.ModerationStatus == null) &&
                        e.EventDate >= opening && e.EventDate < closing)
            .Select(e => new { e.EventDate, e.DurationMinutes })
            .ToListAsync();

        for (var start = opening; start.AddMinutes(durationMinutes) <= closing; start = start.AddMinutes(30))
        {
            if (start <= now)
                continue;

            var end = start.AddMinutes(durationMinutes);
            var overlaps = existingEvents.Any(e =>
                start < e.EventDate.AddMinutes(e.DurationMinutes) &&
                end > e.EventDate);

            if (!overlaps)
                slots.Add(start.ToString("HH:mm"));
        }

        return Json(slots);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var (allowed, message, _) = await GetEventCreationAccessAsync(user);
        if (!allowed)
        {
            TempData["Error"] = message;
            if (message?.Contains("email address", StringComparison.OrdinalIgnoreCase) == true)
                return RedirectToAction("EditProfile", "Account");
            return RedirectToAction(nameof(Index));
        }

        return View(new CreateEventViewModel
        {
            EventDate = DateTime.Today.AddDays(1).AddHours(10),
            DurationMinutes = 60,
            Location = "In Library",
            MaxParticipants = 20
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEventViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var (allowed, message, autoApprove) = await GetEventCreationAccessAsync(user);
        if (!allowed)
        {
            TempData["Error"] = message;
            if (message?.Contains("email address", StringComparison.OrdinalIgnoreCase) == true)
                return RedirectToAction("EditProfile", "Account");
            return RedirectToAction(nameof(Index));
        }

        var creatorRoles = await _userManager.GetRolesAsync(user);
        var isReaderCreator = creatorRoles.Contains("Reader", StringComparer.OrdinalIgnoreCase);
        if (isReaderCreator && model.Location == "In Library" && string.IsNullOrWhiteSpace(user.Email))
        {
            TempData["Error"] = "Please add an email address in My Profile → Edit Profile before creating an in-library event.";
            return RedirectToAction("EditProfile", "Account");
        }

        if (model.Location == "In Library" && model.MaxParticipants > 20)
            ModelState.AddModelError(nameof(model.MaxParticipants), "Library events can have no more than 20 participants.");

        if (!model.EventDate.HasValue || model.EventDate.Value <= DateTime.Now)
            ModelState.AddModelError(nameof(model.EventDate), "Choose a date and time in the future.");

        if (model.EventDate.HasValue &&
            (model.EventDate.Value.Minute is not (0 or 30) || model.EventDate.Value.Second != 0))
            ModelState.AddModelError(nameof(model.EventDate), "Choose a start time on the hour or half-hour (for example, 09:00 or 09:30).");

        var durationMinutes = model.DurationMinutes ?? 0;
        var validDuration = durationMinutes >= 30 && durationMinutes <= 1440 &&
                            durationMinutes % 30 == 0;
        if (!validDuration || (model.Location == "In Library" && durationMinutes > 180))
            ModelState.AddModelError(nameof(model.DurationMinutes),
                "Choose 30 minutes to 3 hours for library events. Online events may last longer in 30-minute increments, up to 24 hours.");

        if (model.Location == "In Library" && model.EventDate.HasValue)
        {
            var start = model.EventDate.Value.TimeOfDay;
            var opening = new TimeSpan(9, 0, 0);
            var closing = new TimeSpan(20, 0, 0);

            if (start < opening || start >= closing)
                ModelState.AddModelError(nameof(model.EventDate), "Library events must start between 09:00 and 20:00.");

            if (durationMinutes > 180)
            {
                ModelState.AddModelError(nameof(model.DurationMinutes), "Events in the library can last no longer than 3 hours.");
            }
            else if (durationMinutes >= 30 &&
                     model.EventDate.Value.AddMinutes(durationMinutes).TimeOfDay > closing)
            {
                ModelState.AddModelError(nameof(model.DurationMinutes), "A library event must finish by 20:00.");
            }
        }

        if (model.Location == "In Library" && model.EventDate.HasValue &&
            ModelState.IsValid)
        {
            var proposedStart = model.EventDate.Value;
            var proposedEnd = proposedStart.AddMinutes(durationMinutes);
            var sameDayEvents = await _context.Events
                .AsNoTracking()
                .Where(e => e.Location == "In Library" &&
                            e.Status != "Cancelled" &&
                            (e.ModerationStatus == "Approved" || e.ModerationStatus == null) &&
                            e.EventDate >= proposedStart.Date.AddHours(9) &&
                            e.EventDate < proposedStart.Date.AddHours(20))
                .Select(e => new { e.EventDate, e.DurationMinutes })
                .ToListAsync();

            var conflict = sameDayEvents.Any(e =>
                proposedStart < e.EventDate.AddMinutes(e.DurationMinutes) &&
                proposedEnd > e.EventDate);

            if (conflict)
                ModelState.AddModelError(nameof(model.EventDate),
                    "This time is no longer available. Please choose another available time.");
        }

        if (!ModelState.IsValid) return View(model);

        var libraryEvent = new Event
        {
            CreatedBy = user.Id,
            Title = model.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            EventDate = model.EventDate!.Value,
            DurationMinutes = model.DurationMinutes!.Value,
            Location = model.Location,
            MaxParticipants = model.MaxParticipants,
            CreatedAt = DateTime.UtcNow,
            Status = "Planned",
            ModerationStatus = autoApprove ? "Approved" : "Pending"
        };

        _context.Events.Add(libraryEvent);
        await _context.SaveChangesAsync();

        var rolesForCreator = await _userManager.GetRolesAsync(user);
        var isReader = rolesForCreator.Contains("Reader", StringComparer.OrdinalIgnoreCase);
        var coordinationMessage = "To coordinate your library event, please contact PrivatLibrary@gmail.com at least 3 days before the event.";

        if (isReader && model.Location == "In Library")
        {
            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = "Library event coordination required",
                Message = $"Your event ‘{libraryEvent.Title}’ has been submitted. {coordinationMessage}",
                Type = "Event",
                RelatedEntityType = "Event",
                RelatedEntityId = libraryEvent.EventId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
            await _context.SaveChangesAsync();
        }

        var creationMessage = autoApprove
            ? "Your event has been created and is now visible to readers."
            : "Your event has been submitted and is awaiting administrator approval.";
        TempData["EventMessage"] = isReader && model.Location == "In Library"
            ? $"{creationMessage} {coordinationMessage}"
            : creationMessage;

        return RedirectToAction(nameof(Details), new { id = libraryEvent.EventId });
    }

    private async Task<(bool Allowed, string? Message, bool AutoApprove)> GetEventCreationAccessAsync(User? user)
    {
        if (user == null)
            return (false, "Please sign in to your account. An active library membership is required to create an event.", false);

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
            roles.Contains("Librarian", StringComparer.OrdinalIgnoreCase))
            return (true, null, true);

        if (!roles.Contains("Reader", StringComparer.OrdinalIgnoreCase))
            return (false, "Your account does not have permission to create events.", false);

        var now = DateTime.UtcNow;
        var hasActiveSubscription = await _context.Subscriptions.AnyAsync(s =>
            s.UserId == user.Id && s.Status == "Active" &&
            s.StartDate <= now && s.EndDate > now && s.MembershipPlan.IsActive);

        if (!hasActiveSubscription)
            return (false, "An active library membership is required to create an event. Please choose a membership plan in your account.", false);

        return (true, null, false);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var currentUserId = user?.Id;
        var item = await _context.Events
            .AsNoTracking()
            .Where(e => e.EventId == id && e.Status != "Cancelled" &&
                        ((e.IsVisible && (e.ModerationStatus == "Approved" || e.ModerationStatus == null)) ||
                         (currentUserId.HasValue && (e.CreatedBy == currentUserId.Value ||
                          e.Participants.Any(p => p.UserId == currentUserId.Value)))))
            .Select(e => new EventDetailsViewModel
            {
                EventId = e.EventId,
                Title = e.Title,
                Description = e.Description,
                EventDate = e.EventDate,
                DurationMinutes = e.DurationMinutes,
                Location = e.Location ?? "To be announced",
                MaxParticipants = e.MaxParticipants,
                RegisteredCount = e.Participants.Count(p => p.Status == "Registered"),
                IsRegistered = user != null && e.Participants.Any(p =>
                    p.UserId == user.Id && p.Status == "Registered"),
                IsOrganizer = currentUserId.HasValue && e.CreatedBy == currentUserId.Value,
                ModerationStatus = e.ModerationStatus ?? "Approved",
                OrganizerName = e.Creator.UserName ?? "Library member"
            })
            .FirstOrDefaultAsync();

        if (item == null) return NotFound();

        var (canJoin, joinMessage) = await GetEventParticipationAccessAsync(user);
        item.CanJoinEvents = canJoin;
        item.JoinEventMessage = joinMessage;

        return View(item);
    }

    private async Task<(bool Allowed, string? Message)> GetEventParticipationAccessAsync(User? user)
    {
        if (user == null)
            return (false, "Please sign in to your account. An active library membership is required to register for this event.");

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
            roles.Contains("Librarian", StringComparer.OrdinalIgnoreCase))
            return (true, null);

        if (!roles.Contains("Reader", StringComparer.OrdinalIgnoreCase))
            return (false, "Your account does not have permission to register for events.");

        var now = DateTime.UtcNow;
        var hasActiveSubscription = await _context.Subscriptions.AnyAsync(s =>
            s.UserId == user.Id && s.Status == "Active" &&
            s.StartDate <= now && s.EndDate > now && s.MembershipPlan.IsActive);

        return hasActiveSubscription
            ? (true, null)
            : (false, "An active library membership is required to register for this event. Please choose a membership plan.");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var (canJoin, accessMessage) = await GetEventParticipationAccessAsync(user);
        if (!canJoin)
        {
            TempData["Error"] = accessMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        var libraryEvent = await _context.Events
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.EventId == id &&
                (e.ModerationStatus == "Approved" || e.ModerationStatus == null) &&
                e.IsVisible && e.Status != "Cancelled");

        if (libraryEvent == null) return NotFound();

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

        var (canCreateEvents, createEventMessage, _) = await GetEventCreationAccessAsync(user);
        ViewBag.CanCreateEvents = canCreateEvents;
        ViewBag.CreateEventMessage = createEventMessage;

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
                DurationMinutes = e.DurationMinutes,
                Location = e.Location ?? "To be announced",
                MaxParticipants = e.MaxParticipants,
                RegisteredCount = e.Participants.Count(x => x.Status == "Registered"),
                IsRegistered = e.Participants.Any(x =>
                    x.UserId == user.Id && x.Status == "Registered"),
                IsOrganizer = e.CreatedBy == user.Id,
                ModerationStatus = e.ModerationStatus ?? "Approved",
                OrganizerName = e.Creator.UserName ?? "Library member"
            })
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return View(events);
    }

}

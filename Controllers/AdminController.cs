using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private static readonly string[] AllowedRoles = ["Reader", "Librarian", "Admin"];
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public AdminController(ApplicationDbContext context, UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index()
    {
        var model = new AdminDashboardViewModel
        {
            TotalUsers = await _userManager.Users.CountAsync(),
            TotalBooks = await _context.Books.CountAsync(),
            TotalCopies = await _context.BookCopies.CountAsync(),
            ActiveLoans = await _context.Loans.CountAsync(l => l.Status == "Active" && l.ReturnDate == null),
            PendingReviews = await _context.Reviews.CountAsync(r => r.ModerationStatus == "Pending"),
            PendingRatings = await _context.Ratings.CountAsync(r => r.ModerationStatus == "Pending"),
            PendingEvents = await _context.Events.CountAsync(e => e.ModerationStatus == "Pending"),
            PendingReservations = await _context.Reservations.CountAsync(r => r.Status == "Pending")
        };

        var reservations = await _context.Reservations.AsNoTracking()
            .Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.ReservationDate).Take(5)
            .Select(r => new AdminActivityViewModel { Date = r.ReservationDate, Type = "Reservation", Description = $"Reservation for \"{r.Book.Title}\"", UserName = r.User.UserName ?? "Unknown", Status = r.Status })
            .ToListAsync();
        var reviews = await _context.Reviews.AsNoTracking()
            .Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).Take(5)
            .Select(r => new AdminActivityViewModel { Date = r.CreatedAt, Type = "Review", Description = $"Review on \"{r.Book.Title}\"", UserName = r.User.UserName ?? "Unknown", Status = r.ModerationStatus })
            .ToListAsync();
        var events = await _context.Events.AsNoTracking().Include(e => e.Creator)
            .OrderByDescending(e => e.CreatedAt).Take(5)
            .Select(e => new AdminActivityViewModel { Date = e.CreatedAt, Type = "Event", Description = e.Title, UserName = e.Creator.UserName ?? "Unknown", Status = e.ModerationStatus })
            .ToListAsync();
        model.RecentActivities = reservations.Concat(reviews).Concat(events)
            .OrderByDescending(x => x.Date).Take(8).ToList();

        model.PendingReviewItems = await _context.Reviews.AsNoTracking()
            .Where(r => r.ModerationStatus == "Pending").Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).Take(3)
            .Select(r => new AdminModerationItemViewModel { Id = r.ReviewId, Type = "Review", Title = $"Review on \"{r.Book.Title}\"", CreatedBy = r.User.UserName ?? "Unknown", CreatedAt = r.CreatedAt, Description = r.Text })
            .ToListAsync();
        model.PendingEventItems = await _context.Events.AsNoTracking()
            .Where(e => e.ModerationStatus == "Pending").Include(e => e.Creator)
            .OrderByDescending(e => e.CreatedAt).Take(3)
            .Select(e => new AdminModerationItemViewModel { Id = e.EventId, Type = "Event", Title = e.Title, CreatedBy = e.Creator.UserName ?? "Unknown", CreatedAt = e.CreatedAt, Description = e.Description ?? e.Location ?? "No description provided" })
            .ToListAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        var model = new List<AdminUserViewModel>();
        foreach (var user in users)
            model.Add(new AdminUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                Email = user.Email,
                Roles = string.Join(", ", await _userManager.GetRolesAsync(user)),
                IsActive = user.IsActive
            });
        return View(model);
    }

    [HttpGet]
    public IActionResult CreateUser() => View(new AdminCreateUserViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
    {
        if (!AllowedRoles.Contains(model.Role, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");

        if (!ModelState.IsValid) return View(model);

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "This role does not exist in the Identity database.");
            return View(model);
        }

        var user = new User
        {
            UserName = model.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
            FirstName = string.Empty,
            LastName = string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        TempData["AdminMessage"] = $"Account '{user.UserName}' was created with the {model.Role} role.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var currentAdminId = _userManager.GetUserId(User);
        if (user.Id.ToString() == currentAdminId)
        {
            TempData["AdminError"] = "You cannot deactivate the account you are currently using.";
            return RedirectToAction(nameof(Users));
        }

        if (!user.IsActive)
        {
            TempData["AdminMessage"] = $"Account '{user.UserName}' is already deactivated.";
            return RedirectToAction(nameof(Users));
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            var activeAdminCount = await _userManager.GetUsersInRoleAsync("Admin");
            if (activeAdminCount.Count(admin => admin.IsActive) <= 1)
            {
                TempData["AdminError"] = "The last active administrator account cannot be deactivated.";
                return RedirectToAction(nameof(Users));
            }
        }

        user.IsActive = false;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["AdminError"] = string.Join(" ", updateResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Users));
        }

        // Refresh the security stamp so existing sign-in cookies are invalidated.
        await _userManager.UpdateSecurityStampAsync(user);

        var notification = new Notification
        {
            UserId = user.Id,
            Title = "Your account has been deactivated",
            Message = "After reviewing the information provided during registration, we determined that it does not comply with the Private Library Community Guidelines. As a result, your account has been deactivated and access to your library account is no longer available. If you believe this decision was made in error, please contact the library administration for further clarification.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = $"Account '{user.UserName}' was deactivated. The user will see the notice if they attempt to sign in.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public IActionResult Moderation() => View();

    [HttpGet]
    public async Task<IActionResult> Reviews()
    {
        var reviews = await _context.Reviews.AsNoTracking().Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(reviews);
    }

    [HttpGet]
    public async Task<IActionResult> Ratings()
    {
        var ratings = await _context.Ratings.AsNoTracking()
            .Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(ratings);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RatingDecision(int id, string decision)
    {
        if (decision is not ("Approved" or "Rejected")) return BadRequest();
        var rating = await _context.Ratings
            .Include(r => r.User)
            .Include(r => r.Book)
            .FirstOrDefaultAsync(r => r.RatingId == id);
        if (rating == null) return NotFound();

        rating.ModerationStatus = decision;
        await _context.SaveChangesAsync();

        var author = !string.IsNullOrWhiteSpace(rating.User.FirstName)
            ? rating.User.FirstName
            : rating.User.UserName ?? "A reader";
        var result = decision == "Approved" ? "was approved" : "was not approved";
        TempData["AdminMessage"] = $"Rating by {author} for “{rating.Book.Title}” {result}.";
        return RedirectToAction(nameof(Ratings));
    }

    [HttpGet]
    public async Task<IActionResult> Events()
    {
        var events = await _context.Events.AsNoTracking().Include(e => e.Creator)
            .OrderByDescending(e => e.CreatedAt).ToListAsync();
        return View(events);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewDecision(int id, string decision)
    {
        if (decision is not ("Approved" or "Rejected")) return BadRequest();
        var review = await _context.Reviews
            .Include(r => r.User)
            .Include(r => r.Book)
            .FirstOrDefaultAsync(r => r.ReviewId == id);
        if (review == null) return NotFound();

        review.ModerationStatus = decision;
        await _context.SaveChangesAsync();

        var author = !string.IsNullOrWhiteSpace(review.User.FirstName)
            ? review.User.FirstName
            : review.User.UserName ?? "A reader";
        var result = decision == "Approved" ? "was approved" : "was not approved";
        TempData["AdminMessage"] = $"Review by {author} for “{review.Book.Title}” {result}.";
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EventDecision(int id, string decision)
    {
        if (decision is not ("Approved" or "Rejected")) return BadRequest();
        var item = await _context.Events.FindAsync(id);
        if (item == null) return NotFound();
        if (decision == "Approved" && item.Location == "In Library")
        {
            var start = item.EventDate;
            var end = start.AddMinutes(item.DurationMinutes);
            var sameDayEvents = await _context.Events
                .AsNoTracking()
                .Where(e => e.EventId != item.EventId &&
                            e.Location == "In Library" &&
                            e.Status != "Cancelled" &&
                            (e.ModerationStatus == "Approved" || e.ModerationStatus == null) &&
                            e.EventDate >= start.Date.AddHours(9) &&
                            e.EventDate < start.Date.AddHours(20))
                .Select(e => new { e.EventDate, e.DurationMinutes })
                .ToListAsync();

            var conflict = sameDayEvents.Any(e =>
                start < e.EventDate.AddMinutes(e.DurationMinutes) &&
                end > e.EventDate);

            if (conflict)
            {
                TempData["AdminMessage"] = "This event cannot be approved because the library is already booked for an overlapping time.";
                return RedirectToAction(nameof(Events));
            }
        }

        item.ModerationStatus = decision;
        await _context.SaveChangesAsync();
        TempData["AdminMessage"] = $"Event {decision.ToLowerInvariant()}.";
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEventVisibility(int id)
    {
        var item = await _context.Events.FindAsync(id);
        if (item == null) return NotFound();

        item.IsVisible = !item.IsVisible;
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = item.IsVisible
            ? $"The event “{item.Title}” is now visible in the public events list."
            : $"The event “{item.Title}” has been hidden from the public events list.";
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var item = await _context.Events
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (item == null) return NotFound();

        // Remove registrations first so no orphaned participant records remain.
        _context.EventParticipants.RemoveRange(item.Participants);
        _context.Events.Remove(item);
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = $"The event “{item.Title}” and its registrations have been deleted.";
        return RedirectToAction(nameof(Events));
    }

}

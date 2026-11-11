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
            .Select(r => new AdminModerationItemViewModel { Id = r.ReviewId, Type = "Review", Title = $"Review on \"{r.Book.Title}\"", CreatedBy = r.User.UserName ?? "Unknown", CreatedAt = r.CreatedAt, Description = r.Comment ?? $"Rating: {r.Rating}/5" })
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
            model.Add(new AdminUserViewModel { Id = user.Id, UserName = user.UserName ?? "", Email = user.Email, Roles = string.Join(", ", await _userManager.GetRolesAsync(user)) });
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
            CreatedAt = DateTime.UtcNow
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

    [HttpGet]
    public IActionResult Moderation() => RedirectToAction(nameof(Reviews));

    [HttpGet]
    public async Task<IActionResult> Reviews()
    {
        var reviews = await _context.Reviews.AsNoTracking().Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(reviews);
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
        var review = await _context.Reviews.FindAsync(id);
        if (review == null) return NotFound();
        review.ModerationStatus = decision;
        await _context.SaveChangesAsync();
        TempData["AdminMessage"] = $"Review {decision.ToLowerInvariant()}.";
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EventDecision(int id, string decision)
    {
        if (decision is not ("Approved" or "Rejected")) return BadRequest();
        var item = await _context.Events.FindAsync(id);
        if (item == null) return NotFound();
        item.ModerationStatus = decision;
        await _context.SaveChangesAsync();
        TempData["AdminMessage"] = $"Event {decision.ToLowerInvariant()}.";
        return RedirectToAction(nameof(Events));
    }

}

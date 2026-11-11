using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using Microsoft.AspNetCore.Mvc;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ApplicationDbContext _context;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var activeReservationStatuses = new[] { "Pending", "Ready" };
        var model = new AccountDashboardViewModel
        {
            User = user,
            ActiveReservationsCount = await _context.Reservations.CountAsync(r =>
                r.UserId == user.Id && activeReservationStatuses.Contains(r.Status)),
            FavoritesCount = await _context.Favorites.CountAsync(f => f.UserId == user.Id),
            ReviewsCount = await _context.Reviews.CountAsync(r => r.UserId == user.Id),
            ActiveLoansCount = await _context.Loans.CountAsync(l =>
                l.UserId == user.Id && l.ReturnDate == null && l.Status == "Active"),
            EventsCount = await _context.EventParticipants.CountAsync(ep =>
                ep.UserId == user.Id && ep.Status == "Registered" && ep.Event.EventDate >= DateTime.UtcNow),
            UnreadNotificationsCount = await _context.Notifications.CountAsync(n =>
                n.UserId == user.Id && !n.IsRead),
            ActiveSubscription = await _context.Subscriptions
                .Where(s => s.UserId == user.Id && s.Status == "Active" && s.EndDate > DateTime.UtcNow)
                .Include(s => s.MembershipPlan)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync(),
            RecentReservations = await _context.Reservations
                .Where(r => r.UserId == user.Id)
                .Include(r => r.Book)
                .OrderByDescending(r => r.ReservationDate)
                .Take(5)
                .ToListAsync(),
            FavoriteBooks = await _context.Favorites
                .Where(f => f.UserId == user.Id)
                .Include(f => f.Book)
                .OrderByDescending(f => f.AddedAt)
                .Take(4)
                .ToListAsync(),
            RecentReviews = await _context.Reviews
                .Where(r => r.UserId == user.Id)
                .Include(r => r.Book)
                .OrderByDescending(r => r.CreatedAt)
                .Take(4)
                .ToListAsync(),
            ActiveLoans = await _context.Loans
                .Where(l => l.UserId == user.Id && l.ReturnDate == null && l.Status == "Active")
                .Include(l => l.Copy)
                .ThenInclude(c => c.Book)
                .OrderBy(l => l.DueDate)
                .Take(4)
                .ToListAsync(),
            UpcomingEvents = await _context.EventParticipants
                .Where(ep => ep.UserId == user.Id && ep.Status == "Registered" && ep.Event.EventDate >= DateTime.UtcNow)
                .Include(ep => ep.Event)
                .OrderBy(ep => ep.Event.EventDate)
                .Take(4)
                .ToListAsync(),
            RecentNotifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(4)
                .ToListAsync()
        };

        return View(model);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> EditProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(new EditProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            IsFavoritesPublic = user.IsFavoritesPublic
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(EditProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (!ModelState.IsValid) return View(model);

        user.FirstName = model.FirstName?.Trim() ?? string.Empty;
        user.LastName = model.LastName?.Trim() ?? string.Empty;
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
            ? null
            : model.PhoneNumber.Trim();
        user.IsFavoritesPublic = model.IsFavoritesPublic;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        TempData["AccountSuccess"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var userName = model.UserName.Trim();
        var email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        if (await _userManager.FindByNameAsync(userName) is not null)
        {
            ModelState.AddModelError(nameof(model.UserName), "This username is already taken.");
            return View(model);
        }

        if (email is not null && await _userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "This email address is already in use.");
            return View(model);
        }

        var user = new User
        {
            UserName = userName,
            Email = email,
            FirstName = string.Empty,
            LastName = string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsFavoritesPublic = false
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            if (!await _userManager.IsInRoleAsync(user, "Reader"))
                await _userManager.AddToRoleAsync(user, "Reader");

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Books");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var login = model.UserName.Trim();
        var user = await _userManager.FindByNameAsync(login);

        if (user is not null)
        {
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Books");
            }
        }

        ModelState.AddModelError(string.Empty, "Login failed. Check your username and password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Books");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}

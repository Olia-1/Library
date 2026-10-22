using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Reader")]
public class ReservationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public ReservationsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reservations = await _context.Reservations
            .Include(r => r.Book)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();

        return View(reservations);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int bookId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (string.IsNullOrWhiteSpace(user.FirstName) ||
            string.IsNullOrWhiteSpace(user.LastName) ||
            string.IsNullOrWhiteSpace(user.Phone))
        {
            TempData["Error"] = "Заповніть ім'я, прізвище та номер телефону в обліковому записі перед бронюванням.";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var now = DateTime.UtcNow;
        var hasActiveSubscription = await _context.Subscriptions.AnyAsync(s =>
            s.UserId == user.Id &&
            s.Status == "Active" &&
            s.StartDate <= now &&
            s.EndDate >= now);

        if (!hasActiveSubscription)
        {
            TempData["Error"] = "Для бронювання потрібен активний абонемент.";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var bookExists = await _context.Books.AnyAsync(b => b.BookId == bookId);
        if (!bookExists) return NotFound();

        var activeStatuses = new[] { "active" };
        var userActiveCount = await _context.Reservations.CountAsync(r =>
            r.UserId == user.Id && activeStatuses.Contains(r.Status));

        if (userActiveCount >= 7)
        {
            TempData["Error"] = "Ви вже маєте максимальну кількість активних бронювань (7).";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var bookActiveCount = await _context.Reservations.CountAsync(r =>
            r.BookId == bookId && activeStatuses.Contains(r.Status));

        if (bookActiveCount >= 3)
        {
            TempData["Error"] = "Для цієї книги вже досягнуто ліміт активних бронювань (3).";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        // Намір ТЗ: можна бронювати як вільну, так і видану книгу.
        // Тому наявність вільного примірника тут навмисно не є умовою.
        var reservation = new Reservation
        {
            UserId = user.Id,
            BookId = bookId,
            ReservationDate = now,
            Status = "active"
        };

        _context.Reservations.Add(reservation);
        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Type = "reservation",
            Message = $"Ваше бронювання книги успішно створено (ID: {bookId}).",
            CreatedAt = now,
            IsRead = false,
            SendByEmail = true,
            EmailStatus = "Pending"
        });

        await _context.SaveChangesAsync();

        TempData["Success"] = "Бронювання створено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == id && r.UserId == userId);

        if (reservation is null) return NotFound();

        if (reservation.Status == "active")
        {
            reservation.Status = "canceled";
            await _context.SaveChangesAsync();
            TempData["Success"] = "Бронювання скасовано.";
        }

        return RedirectToAction(nameof(Index));
    }
}

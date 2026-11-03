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
    private const int MaxActiveReservationsPerReader = 7;
    private const int MaxWaitingQueuePerBook = 3;

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

        if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
        {
            TempData["Error"] = "Заповніть ім’я та прізвище в обліковому записі перед бронюванням.";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var bookExists = await _context.Books.AnyAsync(b => b.BookId == bookId);
        if (!bookExists) return NotFound();

        var activeStatuses = new[] { "Pending", "Ready" };
        var userActiveCount = await _context.Reservations.CountAsync(r =>
            r.UserId == user.Id && activeStatuses.Contains(r.Status));

        if (userActiveCount >= MaxActiveReservationsPerReader)
        {
            TempData["Error"] = $"Можна мати не більше {MaxActiveReservationsPerReader} активних бронювань.";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var alreadyHasActiveForBook = await _context.Reservations.AnyAsync(r =>
            r.UserId == user.Id && r.BookId == bookId && activeStatuses.Contains(r.Status));
        if (alreadyHasActiveForBook)
        {
            TempData["Error"] = "У вас уже є активне бронювання цієї книги.";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var now = DateTime.UtcNow;
        var availableCopy = await _context.BookCopies
            .Where(c => c.BookId == bookId && c.Status == "Available")
            .OrderBy(c => c.CopyId)
            .FirstOrDefaultAsync();

        var reservation = new Reservation
        {
            UserId = user.Id,
            BookId = bookId,
            ReservationDate = now
        };

        if (availableCopy is not null)
        {
            availableCopy.Status = "Reserved";
            reservation.Status = "Ready";
            reservation.AvailableFrom = now;
            reservation.ExpirationDate = now.AddDays(7);
            reservation.QueuePosition = null;
        }
        else
        {
            var waitingCount = await _context.Reservations.CountAsync(r =>
                r.BookId == bookId && r.Status == "Pending");

            if (waitingCount >= MaxWaitingQueuePerBook)
            {
                TempData["Error"] = $"Черга очікування вже заповнена (максимум {MaxWaitingQueuePerBook} читачі).";
                return RedirectToAction("Details", "Books", new { id = bookId });
            }

            var maxPosition = await _context.Reservations
                .Where(r => r.BookId == bookId && r.Status == "Pending")
                .MaxAsync(r => (int?)r.QueuePosition) ?? 0;

            reservation.Status = "Pending";
            reservation.QueuePosition = maxPosition + 1;
        }

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        _context.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Бронювання книги",
            Message = reservation.Status == "Ready"
                ? $"Книга доступна для отримання протягом 7 днів. Бронювання №{reservation.ReservationId}."
                : $"Вас додано до черги очікування книги. Позиція: {reservation.QueuePosition}.",
            CreatedAt = now,
            IsRead = false
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = reservation.Status == "Ready"
            ? "Книгу заброньовано. Заберіть її протягом 7 днів."
            : $"Вас додано до черги очікування. Ваша позиція: {reservation.QueuePosition}.";
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

        if (reservation.Status is "Pending" or "Ready")
        {
            var wasReady = reservation.Status == "Ready";
            reservation.Status = "Canceled";
            reservation.CompletedAt = DateTime.UtcNow;

            BookCopy? releasedCopy = null;
            if (wasReady)
            {
                releasedCopy = await _context.BookCopies
                    .Where(c => c.BookId == reservation.BookId && c.Status == "Reserved")
                    .OrderBy(c => c.CopyId)
                    .FirstOrDefaultAsync();

                if (releasedCopy is not null)
                    releasedCopy.Status = "Available";
            }

            await _context.SaveChangesAsync();

            if (reservation.Status == "Canceled")
            {
                var waiting = await _context.Reservations
                    .Where(r => r.BookId == reservation.BookId && r.Status == "Pending")
                    .OrderBy(r => r.QueuePosition)
                    .ThenBy(r => r.ReservationDate)
                    .FirstOrDefaultAsync();

                if (releasedCopy is not null && waiting is not null)
                {
                    releasedCopy.Status = "Reserved";
                    waiting.Status = "Ready";
                    waiting.QueuePosition = null;
                    waiting.AvailableFrom = DateTime.UtcNow;
                    waiting.ExpirationDate = DateTime.UtcNow.AddDays(7);

                    _context.Notifications.Add(new Notification
                    {
                        UserId = waiting.UserId,
                        Title = "Книга стала доступною",
                        Message = $"Звільнився примірник книги «{(await _context.Books.FindAsync(reservation.BookId))?.Title}». Заберіть його протягом 7 днів.",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                    await _context.SaveChangesAsync();
                }

                var remainingQueue = await _context.Reservations
                    .Where(r => r.BookId == reservation.BookId && r.Status == "Pending")
                    .OrderBy(r => r.QueuePosition)
                    .ThenBy(r => r.ReservationDate)
                    .ToListAsync();

                for (var i = 0; i < remainingQueue.Count; i++)
                    remainingQueue[i].QueuePosition = i + 1;

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Бронювання скасовано.";
        }

        return RedirectToAction(nameof(Index));
    }
}

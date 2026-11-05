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
            TempData["Error"] = "Please add your first and last name to your account before making a reservation.";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var bookExists = await _context.Books.AnyAsync(b => b.BookId == bookId);
        if (!bookExists) return NotFound();

        var activeStatuses = new[] { "Pending", "Ready" };
        var userActiveCount = await _context.Reservations.CountAsync(r =>
            r.UserId == user.Id && activeStatuses.Contains(r.Status));

        if (userActiveCount >= MaxActiveReservationsPerReader)
        {
            TempData["Error"] = $"You can have no more than {MaxActiveReservationsPerReader} active reservations.";
            return RedirectToAction("Details", "Books", new { id = bookId });
        }

        var alreadyHasActiveForBook = await _context.Reservations.AnyAsync(r =>
            r.UserId == user.Id && r.BookId == bookId && activeStatuses.Contains(r.Status));
        if (alreadyHasActiveForBook)
        {
            TempData["Error"] = "You already have an active reservation for this book.";
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
                TempData["Error"] = $"The waiting list is full (maximum {MaxWaitingQueuePerBook} readers).";
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
            Title = "Book reservation",
            Message = reservation.Status == "Ready"
                ? $"The book is available for pickup for 7 days. Reservation #{reservation.ReservationId}."
                : $"You have been added to the book waiting list. Position: {reservation.QueuePosition}.",
            CreatedAt = now,
            IsRead = false
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = reservation.Status == "Ready"
            ? "The book has been reserved. Please collect it within 7 days."
            : $"You have been added to the waiting list. Your position: {reservation.QueuePosition}.";
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

                    var book = await _context.Books.FindAsync(reservation.BookId);
                    _context.Notifications.Add(new Notification
                    {
                        UserId = waiting.UserId,
                        Title = "Book available",
                       Message = $"A copy of the book '{book?.Title}' is now available. Please collect it within 7 days.",
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

            TempData["Success"] = "Reservation cancelled.";
        }

        return RedirectToAction(nameof(Index));
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;
using System.Globalization;

namespace PrivateLibrary.Controllers;

public class BooksController : Controller
{
    private readonly ApplicationDbContext _context;

    public BooksController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? genreId,
        int? yearFrom,
        int? yearTo,
        string? sort)
    {
        var query = _context.Books
            .AsNoTracking()
            .Where(b => !b.IsArchived)
            .Include(b => b.Genre).ThenInclude(g => g!.Translations)
            .Include(b => b.Translations)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author).ThenInclude(a => a.Translations)
            .Include(b => b.Copies)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(b =>
                b.Title.Contains(search) ||
                b.Translations.Any(t => t.LanguageCode == "uk" && t.Title.Contains(search)) ||
                (b.Publisher != null && b.Publisher.Contains(search)) ||
                (b.Language != null && b.Language.Contains(search)) ||
                b.BookAuthors.Any(ba =>
                    ba.Author.FirstName.Contains(search) ||
                    ba.Author.LastName.Contains(search) ||
                    ba.Author.Translations.Any(t => t.LanguageCode == "uk" && (t.FirstName.Contains(search) || t.LastName.Contains(search)))) ||
                (b.Genre != null && (b.Genre.Name.Contains(search) || b.Genre.Translations.Any(t => t.LanguageCode == "uk" && t.Name.Contains(search)))));
        }

        if (genreId.HasValue)
            query = query.Where(b => b.GenreId == genreId.Value);

        string? yearRangeError = null;
        var currentYear = DateTime.Today.Year;

        if (yearFrom is < 0 || yearFrom > currentYear ||
            yearTo is < 0 || yearTo > currentYear)
        {
            yearRangeError = $"Enter publication years between 0 and {currentYear}.";
        }
        else if (yearFrom.HasValue && yearTo.HasValue && yearFrom.Value > yearTo.Value)
        {
            yearRangeError = "The 'year from' value cannot be greater than the 'year to' value.";
        }

        if (yearRangeError is null)
        {
            if (yearFrom.HasValue)
                query = query.Where(b => b.PublicationYear >= yearFrom.Value);
            if (yearTo.HasValue)
                query = query.Where(b => b.PublicationYear <= yearTo.Value);
        }

        ViewData["YearRangeError"] = yearRangeError;

        query = sort switch
        {
            "title_desc" => query.OrderByDescending(b => b.Title),
            "year_asc" => query.OrderBy(b => b.PublicationYear).ThenBy(b => b.Title),
            "year_desc" => query.OrderByDescending(b => b.PublicationYear).ThenBy(b => b.Title),
            _ => query.OrderBy(b => b.Title)
        };

        ViewBag.Genres = await _context.Genres.AsNoTracking()
            .Include(g => g.Translations)
            .OrderBy(g => g.Name).ToListAsync();
        ViewData["Search"] = search;
        ViewData["GenreId"] = genreId;
        ViewData["YearFrom"] = yearFrom;
        ViewData["YearTo"] = yearTo;
        ViewData["Sort"] = sort ?? "title_asc";

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.FavoriteBookIds = int.TryParse(userIdValue, out var userId)
            ? await _context.Favorites.Where(f => f.UserId == userId)
                .Select(f => f.BookId).ToListAsync()
            : new List<int>();

        return View(await query.ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var book = await _context.Books.AsNoTracking()
            .Include(b => b.Genre).ThenInclude(g => g!.Translations)
            .Include(b => b.Translations)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author).ThenInclude(a => a.Translations)
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.BookId == id && !b.IsArchived);

        if (book is null)
            return NotFound();

        var model = new BookDetailsViewModel
        {
            Book = book,
            CanViewCommunityContent = User.Identity?.IsAuthenticated == true
        };

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdValue, out var userId))
        {
            ViewBag.IsFavorite = await _context.Favorites
                .AnyAsync(f => f.UserId == userId && f.BookId == id);
        }
        else
        {
            ViewBag.IsFavorite = false;
        }

        if (model.CanViewCommunityContent)
        {
            model.ApprovedReviews = await _context.Reviews.AsNoTracking()
                .Where(r => r.BookId == id && r.ModerationStatus == "Approved")
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            model.ApprovedRatings = await _context.Ratings.AsNoTracking()
                .Where(r => r.BookId == id && r.ModerationStatus == "Approved")
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            if (int.TryParse(userIdValue, out userId))
            {
                model.MyReview = await _context.Reviews.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.BookId == id && r.UserId == userId);
                model.MyRating = await _context.Ratings.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.BookId == id && r.UserId == userId);
            }
        }

        return View(model);
    }

    [Authorize(Roles = "Reader")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRating(int bookId, byte ratingValue, string? text)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await HasActiveMembershipAsync(userId))
        {
            TempData["Error"] = "An active library membership is required to submit a rating.";
            return RedirectToAction(nameof(Details), new { id = bookId });
        }

        if (ratingValue is < 1 or > 5)
        {
            TempData["Error"] = "Please choose a rating from 1 to 5.";
            return RedirectToAction(nameof(Details), new { id = bookId });
        }

        if (text?.Trim().Length > 30)
        {
            TempData["Error"] = "The rating note cannot exceed 30 characters.";
            return RedirectToAction(nameof(Details), new { id = bookId });
        }

        if (!await _context.Books.AnyAsync(b => b.BookId == bookId && !b.IsArchived))
            return NotFound();

        var rating = await _context.Ratings
            .FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);

        if (rating is null)
        {
            rating = new Rating { BookId = bookId, UserId = userId };
            _context.Ratings.Add(rating);
        }

        rating.Value = ratingValue;
        rating.Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        rating.CreatedAt = DateTime.UtcNow;
        rating.ModerationStatus = "Pending";

        await _context.SaveChangesAsync();
        TempData["Success"] = "Your rating has been submitted for moderation.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [Authorize(Roles = "Reader")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRating(int id, int bookId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var rating = await _context.Ratings
            .FirstOrDefaultAsync(r => r.RatingId == id && r.UserId == userId);

        if (rating is not null)
        {
            _context.Ratings.Remove(rating);
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Your rating has been deleted.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [Authorize(Roles = "Reader")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReview(int bookId, string? text)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await HasActiveMembershipAsync(userId))
        {
            TempData["Error"] = "An active library membership is required to submit a review.";
            return RedirectToAction(nameof(Details), new { id = bookId });
        }

        var normalizedText = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText) || normalizedText.Length < 10 || normalizedText.Length > 5000)
        {
            TempData["Error"] = "A review must contain between 10 and 5000 characters.";
            return RedirectToAction(nameof(Details), new { id = bookId });
        }

        if (!await _context.Books.AnyAsync(b => b.BookId == bookId && !b.IsArchived))
            return NotFound();

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.BookId == bookId && r.UserId == userId);

        if (review is null)
        {
            review = new Review
            {
                BookId = bookId,
                UserId = userId,
                Text = normalizedText,
                CreatedAt = DateTime.UtcNow,
                ModerationStatus = "Pending"
            };
            _context.Reviews.Add(review);
        }
        else
        {
            review.Text = normalizedText;
            review.UpdatedAt = DateTime.UtcNow;
            review.ModerationStatus = "Pending";
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Your review has been submitted for moderation.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [Authorize(Roles = "Reader")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id, int bookId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.ReviewId == id && r.UserId == userId);

        if (review is not null)
        {
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Your review has been deleted.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    private async Task<bool> HasActiveMembershipAsync(int userId)
    {
        var now = DateTime.UtcNow;

        return await _context.Subscriptions.AnyAsync(s =>
            s.UserId == userId &&
            s.Status == "Active" &&
            s.StartDate <= now &&
            s.EndDate > now &&
            s.MembershipPlan.IsActive);
    }
}

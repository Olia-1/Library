using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;
using PrivateLibrary.Services;

namespace PrivateLibrary.Controllers;

public class BooksController : Controller
{
    private readonly ApplicationDbContext _context;
    public BooksController(ApplicationDbContext context) { _context = context; }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? genreId, short? yearFrom, short? yearTo, string? sort)
    {
        var query = _context.Books.AsNoTracking()
            .Include(b => b.Genre)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.Copies).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(b => b.Title.Contains(search) ||
                (b.Publisher != null && b.Publisher.Contains(search)) ||
                (b.Language != null && b.Language.Contains(search)) ||
                b.BookAuthors.Any(ba => ba.Author.FirstName.Contains(search) || ba.Author.LastName.Contains(search)) ||
                (b.Genre != null && b.Genre.Name.Contains(search)));
        }
        if (genreId.HasValue) query = query.Where(b => b.GenreId == genreId.Value);
        if (yearFrom.HasValue) query = query.Where(b => b.PublicationYear >= yearFrom.Value);
        if (yearTo.HasValue) query = query.Where(b => b.PublicationYear <= yearTo.Value);

        query = sort switch
        {
            "title_desc" => query.OrderByDescending(b => b.Title),
            "year_asc" => query.OrderBy(b => b.PublicationYear).ThenBy(b => b.Title),
            "year_desc" => query.OrderByDescending(b => b.PublicationYear).ThenBy(b => b.Title),
            _ => query.OrderBy(b => b.Title)
        };

        ViewBag.Genres = await _context.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync();
        ViewData["Search"] = search;
        ViewData["GenreId"] = genreId;
        ViewData["YearFrom"] = yearFrom;
        ViewData["YearTo"] = yearTo;
        ViewData["Sort"] = sort ?? "title_asc";
        return View(await query.ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var book = await _context.Books.AsNoTracking()
            .Include(b => b.Genre)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.BookId == id);
        if (book is null) return NotFound();

        var model = new BookDetailsViewModel { Book = book, CanViewCommunityContent = User.Identity?.IsAuthenticated == true };
        if (model.CanViewCommunityContent)
        {
            model.ApprovedReviews = await _context.Reviews.AsNoTracking()
                .Where(r => r.BookId == id && r.ModerationStatus == "Approved")
                .Include(r => r.User).OrderByDescending(r => r.CreatedAt).ToListAsync();
            model.ApprovedRatings = await _context.Ratings.AsNoTracking()
                .Where(r => r.BookId == id && r.ModerationStatus == "Approved")
                .ToListAsync();
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            model.MyReview = await _context.Reviews.FirstOrDefaultAsync(r => r.BookId == id && r.UserId == userId);
            model.MyRating = await _context.Ratings.FirstOrDefaultAsync(r => r.BookId == id && r.UserId == userId);
        }
        return View(model);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRating(int bookId, byte ratingValue, string? text)
    {
        if (ratingValue is < 1 or > 5) return BadRequest("Rating must be between 1 and 5.");
        if (text?.Length > 500) ModelState.AddModelError(nameof(text), "Rating text cannot exceed 500 characters.");
        if (!ModelState.IsValid) return RedirectToAction(nameof(Details), new { id = bookId });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var rating = await _context.Ratings.FirstOrDefaultAsync(x => x.BookId == bookId && x.UserId == userId);
        if (rating is null)
        {
            rating = new Rating { BookId = bookId, UserId = userId };
            _context.Ratings.Add(rating);
        }
        rating.RatingValue = ratingValue;
        rating.Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        rating.CreatedAt = DateTime.UtcNow;
        rating.ModerationStatus = "Pending";
        await _context.SaveChangesAsync();
        TempData["Success"] = "Your rating has been submitted for moderation.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRating(int id, int bookId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var rating = await _context.Ratings.FirstOrDefaultAsync(x => x.RatingId == id && x.UserId == userId);
        if (rating != null) { _context.Ratings.Remove(rating); await _context.SaveChangesAsync(); }
        TempData["Success"] = "Your rating has been deleted.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReview(int bookId, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 10 || text.Trim().Length > 5000)
        {
            TempData["Error"] = "A review must contain between 10 and 5000 characters.";
            return RedirectToAction(nameof(Details), new { id = bookId });
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var review = await _context.Reviews.FirstOrDefaultAsync(x => x.BookId == bookId && x.UserId == userId);
        if (review is null)
        {
            review = new Review { BookId = bookId, UserId = userId, Text = text.Trim() };
            _context.Reviews.Add(review);
        }
        else
        {
            review.Text = text.Trim();
            review.UpdatedAt = DateTime.UtcNow;
            review.ModerationStatus = "Pending";
        }
        await _context.SaveChangesAsync();
        TempData["Success"] = "Your review has been submitted for moderation.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id, int bookId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var review = await _context.Reviews.FirstOrDefaultAsync(x => x.ReviewId == id && x.UserId == userId);
        if (review != null) { _context.Reviews.Remove(review); await _context.SaveChangesAsync(); }
        TempData["Success"] = "Your review has been deleted.";
        return RedirectToAction(nameof(Details), new { id = bookId });
    }
}

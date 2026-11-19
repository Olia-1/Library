using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

public class CommunityController : Controller
{
    private readonly ApplicationDbContext _context;

    public CommunityController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var isReader = User.Identity?.IsAuthenticated == true && User.IsInRole("Reader");
        int? currentUserId = isReader && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
            ? parsedId : null;

        var latestReviews = await _context.Reviews.AsNoTracking()
            .Where(r => r.ModerationStatus == "Approved")
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Take(20)
            .Select(r => new CommunityReviewItemViewModel
            {
                ReviewId = r.ReviewId,
                BookId = r.BookId,
                BookTitle = r.Book.Title,
                CoverImageUrl = r.Book.CoverImageUrl,
                UserId = r.UserId,
                UserName = r.User.UserName ?? "Reader",
                Text = r.Text,
                PublishedAt = r.UpdatedAt ?? r.CreatedAt,
                LikesCount = r.Likes.Count(),
                IsLikedByCurrentUser = currentUserId.HasValue && r.Likes.Any(l => l.UserId == currentUserId.Value)
            })
            .ToListAsync();

        var mostRatedBooks = await _context.Ratings.AsNoTracking()
            .Where(r => r.ModerationStatus == "Approved")
            .GroupBy(r => new { r.BookId, r.Book.Title, r.Book.CoverImageUrl })
            .Select(group => new
            {
                group.Key.BookId,
                group.Key.Title,
                group.Key.CoverImageUrl,
                RatingsCount = group.Count(),
                AverageRating = group.Average(r => (double)r.Value)
            })
            .OrderByDescending(x => x.RatingsCount)
            .ThenByDescending(x => x.AverageRating)
            .ThenBy(x => x.Title)
            .Take(10)
            .ToListAsync();

        var popularBookIds = mostRatedBooks.Select(x => x.BookId).ToList();
        var authorsByBook = await _context.BookAuthors.AsNoTracking()
            .Where(ba => popularBookIds.Contains(ba.BookId))
            .Select(ba => new { ba.BookId, AuthorName = ba.Author.FirstName + " " + ba.Author.LastName })
            .ToListAsync();

        var model = new CommunityViewModel
        {
            IsAuthenticatedReader = isReader,
            LatestReviews = latestReviews,
            MostRatedBooks = mostRatedBooks.Select(book => new CommunityPopularBookViewModel
            {
                BookId = book.BookId,
                Title = book.Title,
                CoverImageUrl = book.CoverImageUrl,
                RatingsCount = book.RatingsCount,
                AverageRating = Math.Round(book.AverageRating, 1),
                Authors = string.Join(", ", authorsByBook.Where(a => a.BookId == book.BookId).Select(a => a.AuthorName))
            }).ToList()
        };

        return View(model);
    }

    [Authorize(Roles = "Reader")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int reviewId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reviewExists = await _context.Reviews.AnyAsync(r => r.ReviewId == reviewId && r.ModerationStatus == "Approved");
        if (!reviewExists)
            return NotFound();

        var existingLike = await _context.ReviewLikes
            .FirstOrDefaultAsync(l => l.ReviewId == reviewId && l.UserId == userId);

        if (existingLike is null)
        {
            _context.ReviewLikes.Add(new ReviewLike { ReviewId = reviewId, UserId = userId, CreatedAt = DateTime.UtcNow });
        }
        else
        {
            _context.ReviewLikes.Remove(existingLike);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

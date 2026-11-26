using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Reader")]
public class ReviewsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReviewsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all")
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var query = _context.Reviews.AsNoTracking()
            .Where(r => r.UserId == userId)
            .Include(r => r.Book)
                .ThenInclude(b => b!.Translations)
            .Include(r => r.Book).ThenInclude(b => b!.BookAuthors).ThenInclude(ba => ba.Author).ThenInclude(a => a.Translations)
            .AsQueryable();

        query = filter.ToLowerInvariant() switch
        {
            "pending" => query.Where(r => r.ModerationStatus == "Pending"),
            "published" => query.Where(r => r.ModerationStatus == "Approved"),
            "rejected" => query.Where(r => r.ModerationStatus == "Rejected"),
            _ => query
        };

        var userReviews = _context.Reviews.AsNoTracking().Where(r => r.UserId == userId);
        var model = new MyReviewsViewModel
        {
            Filter = filter,
            TotalCount = await userReviews.CountAsync(),
            PendingCount = await userReviews.CountAsync(r => r.ModerationStatus == "Pending"),
            PublishedCount = await userReviews.CountAsync(r => r.ModerationStatus == "Approved"),
            RejectedCount = await userReviews.CountAsync(r => r.ModerationStatus == "Rejected"),
            Reviews = await query.OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt).ToListAsync()
        };

        return View(model);
    }
}

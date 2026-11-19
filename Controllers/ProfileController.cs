using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

[Route("Profile")]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProfileController(ApplicationDbContext context) => _context = context;

    // Public profiles show only community content that has passed moderation.
    // A reader's favorites are loaded only when the owner has made them public.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Index(int id)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.UserName,
                u.IsFavoritesPublic
            })
            .SingleOrDefaultAsync();

        if (user is null) return NotFound();

        var model = new PublicProfileViewModel
        {
            User = new User
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserName = user.UserName ?? string.Empty
            },
            FavoritesArePublic = user.IsFavoritesPublic
        };

        // The application keeps ratings and reviews available to signed-in readers only.
        if (User.Identity?.IsAuthenticated == true)
        {
            model.Reviews = await _context.Reviews.AsNoTracking()
                .Where(r => r.UserId == id && r.ModerationStatus == "Approved")
                .Include(r => r.Book)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            model.Ratings = await _context.Ratings.AsNoTracking()
                .Where(r => r.UserId == id && r.ModerationStatus == "Approved")
                .Include(r => r.Book)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        if (model.FavoritesArePublic)
        {
            model.Favorites = await _context.Favorites.AsNoTracking()
                .Where(f => f.UserId == id)
                .Include(f => f.Book)
                    .ThenInclude(b => b!.Genre)
                .Include(f => f.Book)
                    .ThenInclude(b => b!.BookAuthors)
                        .ThenInclude(ba => ba.Author)
                .OrderByDescending(f => f.AddedAt)
                .ToListAsync();
        }

        return View(model);
    }
}

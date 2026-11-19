using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Reader")]
public class FavoritesController : Controller
{
    private readonly ApplicationDbContext _context;

    public FavoritesController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        if (userId is null) return Challenge();

        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.IsFavoritesPublic })
            .SingleAsync();

        ViewBag.IsFavoritesPublic = user.IsFavoritesPublic;
        ViewBag.FavoritesCount = await _context.Favorites.CountAsync(f => f.UserId == userId.Value);

        var favorites = await GetFavoritesQuery(userId.Value).ToListAsync();
        return View(favorites);
    }

    // Public ranking of the 20 books most frequently added to readers' favorites.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Popular()
    {
        var ranking = await _context.Favorites.AsNoTracking()
            .GroupBy(f => f.BookId)
            .Select(group => new
            {
                BookId = group.Key,
                FavoritesCount = group.Count()
            })
            .OrderByDescending(item => item.FavoritesCount)
            .ThenBy(item => item.BookId)
            .Take(20)
            .ToListAsync();

        var bookIds = ranking.Select(item => item.BookId).ToList();
        var books = await _context.Books.AsNoTracking()
            .Where(book => bookIds.Contains(book.BookId))
            .Include(book => book.Genre)
            .Include(book => book.BookAuthors)
                .ThenInclude(bookAuthor => bookAuthor.Author)
            .ToDictionaryAsync(book => book.BookId);

        var model = ranking
            .Where(item => books.ContainsKey(item.BookId))
            .Select((item, index) => new PopularBookViewModel
            {
                Book = books[item.BookId],
                FavoritesCount = item.FavoritesCount,
                Position = index + 1
            })
            .ToList();

        return View(model);
    }

    // Public read-only collection. Only the owner can change visibility.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Public(int id)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == id && u.IsFavoritesPublic)
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .SingleOrDefaultAsync();

        if (user is null) return NotFound();

        ViewBag.OwnerName = $"{user.FirstName} {user.LastName}".Trim();
        var favorites = await GetFavoritesQuery(id).ToListAsync();
        return View("Public", favorites);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int bookId)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var bookExists = await _context.Books.AnyAsync(b => b.BookId == bookId);
        if (!bookExists) return NotFound();

        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.BookId == bookId);

        var isFavorite = favorite is null;
        if (isFavorite)
        {
            _context.Favorites.Add(new Favorite
            {
                UserId = userId.Value,
                BookId = bookId,
                AddedAt = DateTime.UtcNow
            });
        }
        else
        {
            _context.Favorites.Remove(favorite!);
        }

        await _context.SaveChangesAsync();

        var count = await _context.Favorites.CountAsync(f => f.UserId == userId.Value);
        return Json(new { success = true, isFavorite, count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetVisibility(bool isPublic)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null) return NotFound();

        user.IsFavoritesPublic = isPublic;
        await _context.SaveChangesAsync();

        var publicUrl = Url.Action(nameof(Public), "Favorites", new { id = userId.Value });
        return Json(new { success = true, isPublic, publicUrl });
    }

    private IQueryable<Favorite> GetFavoritesQuery(int userId) =>
        _context.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Include(f => f.Book)
                .ThenInclude(b => b!.Genre)
            .Include(f => f.Book)
                .ThenInclude(b => b!.BookAuthors)
                    .ThenInclude(ba => ba.Author)
            .OrderByDescending(f => f.AddedAt);

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}

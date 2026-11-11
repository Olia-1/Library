using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;

namespace PrivateLibrary.Controllers;

public class BooksController : Controller
{
    private readonly ApplicationDbContext _context;

    public BooksController(ApplicationDbContext context) => _context = context;

    // Catalog with search and filters (Lab 5).
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? genreId,
        short? yearFrom,
        short? yearTo,
        string? sort)
    {
        var query = _context.Books
            .AsNoTracking()
            .Include(b => b.Genre)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.Copies)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(b =>
                b.Title.Contains(search) ||
                (b.Publisher != null && b.Publisher.Contains(search)) ||
                (b.Language != null && b.Language.Contains(search)) ||
                b.BookAuthors.Any(ba =>
                    ba.Author.FirstName.Contains(search) ||
                    ba.Author.LastName.Contains(search)) ||
                (b.Genre != null && b.Genre.Name.Contains(search)));
        }

        if (genreId.HasValue)
            query = query.Where(b => b.GenreId == genreId.Value);

        if (yearFrom.HasValue)
            query = query.Where(b => b.PublicationYear >= yearFrom.Value);

        if (yearTo.HasValue)
            query = query.Where(b => b.PublicationYear <= yearTo.Value);

        query = sort switch
        {
            "title_desc" => query.OrderByDescending(b => b.Title),
            "year_asc" => query.OrderBy(b => b.PublicationYear).ThenBy(b => b.Title),
            "year_desc" => query.OrderByDescending(b => b.PublicationYear).ThenBy(b => b.Title),
            _ => query.OrderBy(b => b.Title)
        };

        ViewBag.Genres = await _context.Genres
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync();

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
        var book = await _context.Books
            .AsNoTracking()
            .Include(b => b.Genre)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.Copies)
            .Include(b => b.Reviews).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(b => b.BookId == id);

        return book is null ? NotFound() : View(book);
    }
}

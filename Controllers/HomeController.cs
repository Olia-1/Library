using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;

namespace PrivateLibrary.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Show only books that have at least one copy currently marked Available.
        var availableBooks = await _context.Books
            .AsNoTracking()
            .Include(b => b.Genre)
            .Include(b => b.BookAuthors)
                .ThenInclude(ba => ba.Author)
            .Include(b => b.Copies)
            .Where(b => b.Copies.Any(copy => copy.Status == "Available"))
            .OrderByDescending(b => b.Copies.Count(copy => copy.Status == "Available"))
            .ThenBy(b => b.Title)
            .Take(8)
            .ToListAsync();

        return View(availableBooks);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

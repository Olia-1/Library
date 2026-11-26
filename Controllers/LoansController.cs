using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Reader")]
public class LoansController : Controller
{
    private readonly ApplicationDbContext _context;

    public LoansController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all")
    {
        if (string.IsNullOrWhiteSpace(filter))
            filter = "all";

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var today = DateTime.Today;

        var query = _context.Loans.AsNoTracking()
            .Where(l => l.UserId == userId)
            .Include(l => l.Copy).ThenInclude(c => c.Book)
                .ThenInclude(b => b!.Translations)
                .Include(l => l.Copy).ThenInclude(c => c.Book)
                    .ThenInclude(b => b!.BookAuthors).ThenInclude(ba => ba.Author).ThenInclude(a => a.Translations)
            .AsQueryable();

        query = filter.ToLowerInvariant() switch
        {
            "overdue" => query.Where(l => l.ReturnDate == null && l.DueDate.Date < today),
            "returned" => query.Where(l => l.ReturnDate != null || l.Status == "Returned" || l.Status == "Completed"),
            "all" => query,
            _ => query.Where(l => l.ReturnDate == null && l.DueDate.Date >= today && l.Status == "Active")
        };

        var allLoans = _context.Loans.AsNoTracking().Where(l => l.UserId == userId);
        var model = new MyLoansViewModel
        {
            Filter = filter,
            TotalCount = await allLoans.CountAsync(),
            ActiveCount = await allLoans.CountAsync(l => l.ReturnDate == null && l.DueDate.Date >= today && l.Status == "Active"),
            OverdueCount = await allLoans.CountAsync(l => l.ReturnDate == null && l.DueDate.Date < today),
            ReturnedCount = await allLoans.CountAsync(l => l.ReturnDate != null || l.Status == "Returned" || l.Status == "Completed"),
            Loans = await query.OrderByDescending(l => l.IssueDate).ToListAsync()
        };

        return View(model);
    }
}

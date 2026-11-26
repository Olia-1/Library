using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;
using PrivateLibrary.Services;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Librarian")]
public class LibrarianController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly NotificationService _notificationService;
    private readonly BookCatalogService _bookCatalog;

    public LibrarianController(ApplicationDbContext context, UserManager<User> userManager, NotificationService notificationService, BookCatalogService bookCatalog)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
        _bookCatalog = bookCatalog;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var today = DateTime.UtcNow.Date;
        var model = new LibrarianDashboardViewModel
        {
            TotalBooks = await _context.Books.CountAsync(),
            TotalCopies = await _context.BookCopies.CountAsync(),
            AvailableCopies = await _context.BookCopies.CountAsync(c => c.Status == "Available"),
            ActiveLoans = await _context.Loans.CountAsync(l => l.ReturnDate == null && l.Status == "Active"),
            OverdueLoans = await _context.Loans.CountAsync(l =>
                l.ReturnDate == null && l.Status == "Active" && l.DueDate < today),
            PendingReservations = await _context.Reservations.CountAsync(r => r.Status == "Pending"),
            ReadyReservations = await _context.Reservations.CountAsync(r => r.Status == "Ready"),
            RecentLoans = await _context.Loans.AsNoTracking()
                .Include(l => l.User)
                .Include(l => l.Copy).ThenInclude(c => c.Book)
                .OrderByDescending(l => l.IssueDate)
                .Take(6)
                .Select(l => new LibrarianLoanItemViewModel
                {
                    LoanId = l.LoanId,
                    BookTitle = l.Copy.Book.Title,
                    ReaderName = (l.User.FirstName + " " + l.User.LastName).Trim(),
                    UserName = l.User.UserName ?? "",
                    CopyNumber = l.Copy.InventoryNumber,
                    IssueDate = l.IssueDate,
                    DueDate = l.DueDate,
                    Status = l.ReturnDate != null ? "Returned" :
                        l.DueDate < today ? "Overdue" : "On loan"
                }).ToListAsync()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> IssueReturn(
        string? readerSearch,
        string? bookSearch,
        string? loanSearch,
        int? selectedReaderId,
        int? selectedBookId,
        int? selectedCopyId,
        string? tab = "issue")
    {
        tab = string.Equals(tab, "return", StringComparison.OrdinalIgnoreCase) ? "return" : "issue";

        var readerOptions = new List<LibrarianReaderOption>();
        var bookOptions = new List<LibrarianBookOption>();

        if (tab == "issue")
        {
        var readers = await _userManager.GetUsersInRoleAsync("Reader");
        var readerQuery = readers.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(readerSearch))
        {
            var term = readerSearch.Trim();
            readerQuery = readerQuery.Where(u =>
                (u.UserName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.Id.ToString() == term);
        }

        readerOptions = readerQuery
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Take(30)
            .Select(u => new LibrarianReaderOption
            {
                UserId = u.Id,
                UserName = u.UserName ?? "",
                FullName = $"{u.FirstName} {u.LastName}".Trim()
            }).ToList();

        var booksQuery = _context.Books.AsNoTracking()
            .Include(b => b.Genre)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.Copies)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(bookSearch))
        {
            var term = bookSearch.Trim();
            booksQuery = booksQuery.Where(b =>
                b.Title.Contains(term) ||
                (b.ISBN != null && b.ISBN.Contains(term)) ||
                b.BookAuthors.Any(ba =>
                    ba.Author.FirstName.Contains(term) || ba.Author.LastName.Contains(term)));
        }

        var books = await booksQuery.OrderBy(b => b.Title).Take(30).ToListAsync();
        bookOptions = books.Select(b => new LibrarianBookOption
        {
            BookId = b.BookId,
            Title = b.Title,
            Authors = string.Join(", ", b.BookAuthors.Select(ba =>
                $"{ba.Author.FirstName} {ba.Author.LastName}")),
            Isbn = b.ISBN ?? "",
            Genre = b.Genre?.Name ?? "",
            CoverImageUrl = b.CoverImageUrl,
            Copies = b.Copies
                .Where(c => c.Status is "Available" or "Reserved")
                .OrderBy(c => c.InventoryNumber)
                .Select(c => new LibrarianCopyOption
                {
                    CopyId = c.CopyId,
                    InventoryNumber = c.InventoryNumber,
                    Condition = c.Condition ?? "Not specified",
                    Location = c.Location ?? "Not specified",
                    Status = c.Status
                }).ToList()
        }).ToList();
        }

        var loansQuery = _context.Loans.AsNoTracking()
            .Where(l => l.ReturnDate == null && l.Status == "Active")
            .Include(l => l.User)
            .Include(l => l.Copy).ThenInclude(c => c.Book)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(loanSearch))
        {
            var term = loanSearch.Trim();
            loansQuery = loansQuery.Where(l =>
                l.User.UserName!.Contains(term) ||
                l.User.FirstName.Contains(term) ||
                l.User.LastName.Contains(term) ||
                l.Copy.Book.Title.Contains(term) ||
                l.Copy.InventoryNumber.Contains(term));
        }

        var today = DateTime.UtcNow.Date;
        var activeLoans = await loansQuery.OrderBy(l => l.DueDate)
            .Take(100)
            .Select(l => new LibrarianLoanItemViewModel
            {
                LoanId = l.LoanId,
                BookTitle = l.Copy.Book.Title,
                ReaderName = (l.User.FirstName + " " + l.User.LastName).Trim(),
                UserName = l.User.UserName ?? "",
                CopyNumber = l.Copy.InventoryNumber,
                IssueDate = l.IssueDate,
                DueDate = l.DueDate,
                Status = l.DueDate < today ? "Overdue" : "On loan"
            }).ToListAsync();

        var model = new LibrarianIssueReturnViewModel
        {
            ReaderSearch = readerSearch ?? "",
            BookSearch = bookSearch ?? "",
            LoanSearch = loanSearch ?? "",
            ActiveTab = tab,
            SelectedReaderId = selectedReaderId,
            SelectedBookId = selectedBookId,
            SelectedCopyId = selectedCopyId,
            DueDate = DateTime.Today.AddDays(14),
            Readers = readerOptions,
            Books = bookOptions,
            ActiveLoans = activeLoans
        };

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueBook(int readerId, int copyId, DateTime dueDate)
    {
        var reader = await _userManager.FindByIdAsync(readerId.ToString());
        if (reader is null || !reader.IsActive || !await _userManager.IsInRoleAsync(reader, "Reader"))
        {
            TempData["Error"] = "Select an active reader account.";
            return RedirectToAction(nameof(IssueReturn), new { tab = "issue" });
        }

        var hasActiveMembership = await _context.Subscriptions.AnyAsync(s =>
            s.UserId == readerId && s.Status == "Active" && s.EndDate > DateTime.UtcNow);
        if (!hasActiveMembership)
        {
            TempData["Error"] = "The reader needs an active membership before borrowing books.";
            return RedirectToAction(nameof(IssueReturn), new { tab = "issue", selectedReaderId = readerId });
        }

        var copy = await _context.BookCopies
            .Include(c => c.Book)
            .FirstOrDefaultAsync(c => c.CopyId == copyId);
        if (copy is null || copy.Book.IsArchived)
        {
            TempData["Error"] = "The selected book copy could not be found in the active catalog.";
            return RedirectToAction(nameof(IssueReturn), new { tab = "issue", selectedReaderId = readerId });
        }

        if (dueDate.Date <= DateTime.UtcNow.Date)
        {
            TempData["Error"] = "The due date must be later than today.";
            return RedirectToAction(nameof(IssueReturn), new { tab = "issue", selectedReaderId = readerId, selectedBookId = copy.BookId });
        }

        Reservation? readyReservation = null;
        if (copy.Status == "Reserved")
        {
            readyReservation = await _context.Reservations.FirstOrDefaultAsync(r =>
                r.UserId == readerId && r.BookId == copy.BookId && r.Status == "Ready");
            if (readyReservation is null)
            {
                TempData["Error"] = "This copy is reserved for another reader.";
                return RedirectToAction(nameof(IssueReturn), new { tab = "issue", selectedReaderId = readerId, selectedBookId = copy.BookId });
            }
        }
        else if (copy.Status != "Available")
        {
            TempData["Error"] = "This book copy is not available for issue.";
            return RedirectToAction(nameof(IssueReturn), new { tab = "issue", selectedReaderId = readerId, selectedBookId = copy.BookId });
        }

        var librarianIdText = _userManager.GetUserId(User);
        if (!int.TryParse(librarianIdText, out var librarianId))
            return Forbid();

        var now = DateTime.UtcNow;
        copy.Status = "Loaned";
        if (readyReservation is not null)
        {
            readyReservation.Status = "Completed";
            readyReservation.CompletedAt = now;
        }

        var loan = new Loan
        {
            CopyId = copy.CopyId,
            UserId = readerId,
            LibrarianId = librarianId,
            IssueDate = now,
            DueDate = dueDate.Date.AddHours(23).AddMinutes(59),
            Status = "Active"
        };
        _context.Loans.Add(loan);

        await _context.SaveChangesAsync();

        var dueDateText = loan.DueDate.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
        await _notificationService.NotifyUserAsync(
            loan.UserId,
            "Book issued",
            $"The book “{copy.Book.Title}” has been issued to you. Please return it by {dueDateText}.",
            "Reservation",
            "Loan",
            loan.LoanId);

        TempData["Success"] = $"“{copy.Book.Title}” was issued to {reader.UserName}.";
        return RedirectToAction(nameof(IssueReturn), new { tab = "issue" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterReturn(int loanId)
    {
        var loan = await _context.Loans
            .Include(l => l.User)
            .Include(l => l.Copy).ThenInclude(c => c.Book)
            .FirstOrDefaultAsync(l => l.LoanId == loanId && l.ReturnDate == null && l.Status == "Active");

        if (loan is null)
        {
            TempData["Error"] = "This active loan could not be found.";
            return RedirectToAction(nameof(IssueReturn), new { tab = "return" });
        }

        var now = DateTime.UtcNow;
        loan.ReturnDate = now;
        loan.Status = "Returned";
        loan.Copy.Status = "Available";

        var nextReservation = await _context.Reservations
            .Where(r => r.BookId == loan.Copy.BookId && r.Status == "Pending")
            .OrderBy(r => r.QueuePosition)
            .ThenBy(r => r.ReservationDate)
            .FirstOrDefaultAsync();

        if (nextReservation is not null)
        {
            nextReservation.Status = "Ready";
            nextReservation.QueuePosition = null;
            nextReservation.AvailableFrom = now;
            nextReservation.ExpirationDate = now.AddDays(7);
            loan.Copy.Status = "Reserved";

        }

        await _context.SaveChangesAsync();

        await _notificationService.NotifyUserAsync(
            loan.UserId,
            "Book returned",
            $"The book “{loan.Copy.Book.Title}” has been successfully returned. Thank you!",
            "Reservation",
            "Loan",
            loan.LoanId);

        TempData["Success"] = $"Return of “{loan.Copy.Book.Title}” by {loan.User.UserName} was registered.";
        return RedirectToAction(nameof(IssueReturn), new { tab = "return" });
    }

    [HttpGet]
    public async Task<IActionResult> Reservations(string? search, string? status)
    {
        var activeReservations = _context.Reservations.AsNoTracking()
            .Where(r => r.Status == "Pending" || r.Status == "Ready");

        var pendingCount = await activeReservations.CountAsync(r => r.Status == "Pending");
        var readyCount = await activeReservations.CountAsync(r => r.Status == "Ready");
        var booksCount = await activeReservations.Select(r => r.BookId).Distinct().CountAsync();

        var query = activeReservations
            .Include(r => r.User)
            .Include(r => r.Book)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                r.Book.Title.Contains(term) ||
                (r.User.UserName != null && r.User.UserName.Contains(term)) ||
                r.User.FirstName.Contains(term) ||
                r.User.LastName.Contains(term));
        }

        if (status == "Pending" || status == "Ready")
            query = query.Where(r => r.Status == status);

        var reservations = await query
            .OrderBy(r => r.Book.Title)
            .ThenBy(r => r.Status == "Ready" ? 0 : 1)
            .ThenBy(r => r.QueuePosition)
            .ThenBy(r => r.ReservationDate)
            .Take(300)
            .ToListAsync();

        var bookGroups = reservations
            .GroupBy(r => new
            {
                r.BookId,
                r.Book.Title,
                r.Book.CoverImageUrl
            })
            .OrderBy(g => g.Key.Title)
            .Select(g =>
            {
                var ordered = g
                    .OrderBy(r => r.Status == "Ready" ? 0 : 1)
                    .ThenBy(r => r.QueuePosition)
                    .ThenBy(r => r.ReservationDate)
                    .ToList();

                var pendingPosition = 0;
                var items = ordered.Select(r =>
                {
                    int? position = null;
                    if (r.Status == "Pending")
                    {
                        pendingPosition++;
                        position = r.QueuePosition ?? pendingPosition;
                    }

                    return new LibrarianReservationItem
                    {
                        ReservationId = r.ReservationId,
                        UserId = r.UserId,
                        UserName = r.User.UserName ?? "",
                        ReaderName = $"{r.User.FirstName} {r.User.LastName}".Trim(),
                        ReservationDate = r.ReservationDate,
                        QueuePosition = position,
                        Status = r.Status,
                        ExpirationDate = r.ExpirationDate
                    };
                }).ToList();

                return new LibrarianReservationBookGroup
                {
                    BookId = g.Key.BookId,
                    BookTitle = g.Key.Title,
                    CoverImageUrl = g.Key.CoverImageUrl,
                    PendingCount = items.Count(x => x.Status == "Pending"),
                    ReadyCount = items.Count(x => x.Status == "Ready"),
                    Reservations = items
                };
            })
            .ToList();

        var model = new LibrarianReservationsViewModel
        {
            Search = search ?? "",
            Status = status ?? "",
            PendingCount = pendingCount,
            ReadyCount = readyCount,
            BooksCount = booksCount,
            Books = bookGroups
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Books(
        string? search,
        int? genreId = null,
        string sort = "title",
        int page = 1)
    {
        const int pageSize = 12;
        sort = sort is "title_desc" or "year_desc" or "year_asc" or "copies_desc" ? sort : "title";
        page = Math.Max(1, page);

        var query = _context.Books.AsNoTracking()
            .Where(b => !b.IsArchived)
            .Include(b => b.Genre)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.Copies)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(b =>
                b.Title.Contains(search) ||
                (b.ISBN != null && b.ISBN.Contains(search)) ||
                (b.Publisher != null && b.Publisher.Contains(search)) ||
                b.BookAuthors.Any(ba =>
                    ba.Author.FirstName.Contains(search) ||
                    ba.Author.LastName.Contains(search)));
        }

        if (genreId.HasValue)
            query = query.Where(b => b.GenreId == genreId.Value);

        query = sort switch
        {
            "title_desc" => query.OrderByDescending(b => b.Title),
            "year_asc" => query.OrderBy(b => b.PublicationYear).ThenBy(b => b.Title),
            "year_desc" => query.OrderByDescending(b => b.PublicationYear).ThenBy(b => b.Title),
            "copies_desc" => query.OrderByDescending(b => b.Copies.Count).ThenBy(b => b.Title),
            _ => query.OrderBy(b => b.Title)
        };

        var totalCount = await query.CountAsync();
        var bookEntities = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var books = bookEntities.Select(b => new AdminBookListItemViewModel
        {
            BookId = b.BookId,
            Title = b.Title,
            ISBN = b.ISBN,
            Publisher = b.Publisher,
            PublicationYear = b.PublicationYear,
            GenreName = b.Genre?.Name ?? "Uncategorized",
            Authors = string.Join(", ", b.BookAuthors.Select(ba => ba.Author.FirstName + " " + ba.Author.LastName)),
            TotalCopies = b.Copies.Count,
            AvailableCopies = b.Copies.Count(c => c.Status == "Available"),
            IsArchived = false
        }).ToList();

        var model = new AdminBookManagementViewModel
        {
            Search = search,
            Status = "active",
            GenreId = genreId,
            Sort = sort,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Books = books,
            Genres = await _context.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync(),
            ActiveCount = await _context.Books.CountAsync(b => !b.IsArchived),
            ArchivedCount = 0
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateBook()
    {
        ViewBag.BookGenres = await _bookCatalog.GetGenresAsync();
        return View(new AdminBookViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBook(AdminBookViewModel model)
    {
        if (model.InitialCopies < 0 || model.InitialCopies > 1000)
            ModelState.AddModelError(nameof(model.InitialCopies), "The number of copies must be between 0 and 1000.");
        if (!ModelState.IsValid)
        {
            ViewBag.BookGenres = await _bookCatalog.GetGenresAsync();
            return View(model);
        }
        if (model.GenreId.HasValue && !await _context.Genres.AnyAsync(g => g.GenreId == model.GenreId.Value))
        {
            ModelState.AddModelError(nameof(model.GenreId), "The selected genre does not exist.");
            ViewBag.BookGenres = await _bookCatalog.GetGenresAsync();
            return View(model);
        }
        var book = await _bookCatalog.CreateBookAsync(model);
        TempData["Success"] = $"Book '{book.Title}' was added with {model.InitialCopies} copy/copies.";
        return RedirectToAction(nameof(Books));
    }

    [HttpGet]
    
[HttpGet]
public async Task<IActionResult> EditBook(int id)
{
    var model = await _bookCatalog.GetBookForEditAsync(id);

    if (model is null)
        return NotFound();

    ViewBag.BookGenres = await _bookCatalog.GetGenresAsync();

    return View(model);
}

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBook(AdminBookViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.BookGenres = await _bookCatalog.GetGenresAsync();
            return View(model);
        }
        if (model.GenreId.HasValue && !await _context.Genres.AnyAsync(g => g.GenreId == model.GenreId.Value))
        {
            ModelState.AddModelError(nameof(model.GenreId), "The selected genre does not exist.");
            ViewBag.BookGenres = await _bookCatalog.GetGenresAsync();
            return View(model);
        }
        var book = await _bookCatalog.UpdateActiveBookAsync(model);
        if (book is null)
        {
            TempData["Error"] = "Archived books cannot be edited. Restore the book first.";
            return RedirectToAction(nameof(Books));
        }
        TempData["Success"] = $"Book '{book.Title}' was updated.";
        return RedirectToAction(nameof(Books));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBookCopies(int bookId, int targetCopyCount)
    {
        if (targetCopyCount < 0 || targetCopyCount > 1000)
        {
            TempData["Error"] = "The number of copies must be between 0 and 1000.";
            return RedirectToAction(nameof(Books));
        }
        var result = await _bookCatalog.IncreaseCopiesAsync(bookId, targetCopyCount);
        if (result.Book is null) return NotFound();
        if (result.WouldDecrease)
        {
            TempData["Error"] = "Librarians can add copies but cannot delete catalog records. Contact an administrator to remove a copy.";
            return RedirectToAction(nameof(ManageCopies), new { bookId });
        }
        TempData["Success"] = $"The copy count for '{result.Book.Title}' is now {result.CurrentCount}.";
        return RedirectToAction(nameof(Books));
    }

    [HttpGet]
    public async Task<IActionResult> ManageCopies(int bookId)
    {
        var book = await _context.Books
            .AsNoTracking()
            .Include(b => b.Copies)
                .ThenInclude(c => c.Loans)
            .FirstOrDefaultAsync(b => b.BookId == bookId && !b.IsArchived);

        if (book is null) return NotFound();

        var model = new BookCopyManagementViewModel
        {
            BookId = book.BookId,
            BookTitle = book.Title,
            IsArchived = false,
            Copies = book.Copies
                .OrderBy(c => c.InventoryNumber)
                .Select(c => new BookCopyItemViewModel
                {
                    CopyId = c.CopyId,
                    InventoryNumber = c.InventoryNumber,
                    Status = c.Status,
                    Condition = c.Condition,
                    Location = c.Location,
                    LoanCount = c.Loans.Count,
                    CanDelete = c.Status == "Available" && c.Loans.Count == 0
                }).ToList()
        };

        return View(model);
    }

}
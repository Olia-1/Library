using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;
using PrivateLibrary.Services;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private static readonly string[] AllowedRoles = ["Reader", "Librarian", "Admin"];
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly NotificationService _notifications;
    private readonly BookCatalogService _bookCatalog;

    public AdminController(ApplicationDbContext context, UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager, NotificationService notifications, BookCatalogService bookCatalog)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _notifications = notifications;
        _bookCatalog = bookCatalog;
    }

    public async Task<IActionResult> Index()
    {
        var model = new AdminDashboardViewModel
        {
            TotalUsers = await _userManager.Users.CountAsync(),
            TotalBooks = await _context.Books.CountAsync(),
            TotalCopies = await _context.BookCopies.CountAsync(),
            ActiveLoans = await _context.Loans.CountAsync(l => l.Status == "Active" && l.ReturnDate == null),
            PendingReviews = await _context.Reviews.CountAsync(r => r.ModerationStatus == "Pending"),
            PendingRatings = await _context.Ratings.CountAsync(r => r.ModerationStatus == "Pending"),
            PendingEvents = await _context.Events.CountAsync(e => e.ModerationStatus == "Pending"),
            PendingReservations = await _context.Reservations.CountAsync(r => r.Status == "Pending")
        };

        var reservations = await _context.Reservations.AsNoTracking()
            .Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.ReservationDate).Take(5)
            .Select(r => new AdminActivityViewModel { Date = r.ReservationDate, Type = "Reservation", Description = $"Reservation for \"{r.Book.Title}\"", UserName = r.User.UserName ?? "Unknown", Status = r.Status })
            .ToListAsync();
        var reviews = await _context.Reviews.AsNoTracking()
            .Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).Take(5)
            .Select(r => new AdminActivityViewModel { Date = r.CreatedAt, Type = "Review", Description = $"Review on \"{r.Book.Title}\"", UserName = r.User.UserName ?? "Unknown", Status = r.ModerationStatus })
            .ToListAsync();
        var events = await _context.Events.AsNoTracking().Include(e => e.Creator)
            .OrderByDescending(e => e.CreatedAt).Take(5)
            .Select(e => new AdminActivityViewModel { Date = e.CreatedAt, Type = "Event", Description = e.Title, UserName = e.Creator.UserName ?? "Unknown", Status = e.ModerationStatus ?? "Pending" })
            .ToListAsync();
        model.RecentActivities = reservations.Concat(reviews).Concat(events)
            .OrderByDescending(x => x.Date).Take(8).ToList();

        model.PendingReviewItems = await _context.Reviews.AsNoTracking()
            .Where(r => r.ModerationStatus == "Pending").Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).Take(3)
            .Select(r => new AdminModerationItemViewModel { Id = r.ReviewId, Type = "Review", Title = $"Review on \"{r.Book.Title}\"", CreatedBy = r.User.UserName ?? "Unknown", CreatedAt = r.CreatedAt, Description = r.Text })
            .ToListAsync();
        model.PendingEventItems = await _context.Events.AsNoTracking()
            .Where(e => e.ModerationStatus == "Pending").Include(e => e.Creator)
            .OrderByDescending(e => e.CreatedAt).Take(3)
            .Select(e => new AdminModerationItemViewModel { Id = e.EventId, Type = "Event", Title = e.Title, CreatedBy = e.Creator.UserName ?? "Unknown", CreatedAt = e.CreatedAt, Description = e.Description ?? e.Location ?? "No description provided" })
            .ToListAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        var model = new List<AdminUserViewModel>();
        foreach (var user in users)
            model.Add(new AdminUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                Email = user.Email,
                Roles = string.Join(", ", await _userManager.GetRolesAsync(user)),
                IsActive = user.IsActive
            });
        return View(model);
    }

    [HttpGet]
    public IActionResult CreateUser() => View(new AdminCreateUserViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
    {
        if (!AllowedRoles.Contains(model.Role, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");

        if (!ModelState.IsValid) return View(model);

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "This role does not exist in the Identity database.");
            return View(model);
        }

        var user = new User
        {
            UserName = model.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
            FirstName = string.Empty,
            LastName = string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        TempData["AdminMessage"] = $"Account '{user.UserName}' was created with the {model.Role} role.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var currentAdminId = _userManager.GetUserId(User);
        if (user.Id.ToString() == currentAdminId)
        {
            TempData["AdminError"] = "You cannot deactivate the account you are currently using.";
            return RedirectToAction(nameof(Users));
        }

        if (!user.IsActive)
        {
            TempData["AdminMessage"] = $"Account '{user.UserName}' is already deactivated.";
            return RedirectToAction(nameof(Users));
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            var activeAdminCount = await _userManager.GetUsersInRoleAsync("Admin");
            if (activeAdminCount.Count(admin => admin.IsActive) <= 1)
            {
                TempData["AdminError"] = "The last active administrator account cannot be deactivated.";
                return RedirectToAction(nameof(Users));
            }
        }

        user.IsActive = false;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["AdminError"] = string.Join(" ", updateResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Users));
        }

        // Refresh the security stamp so existing sign-in cookies are invalidated.
        await _userManager.UpdateSecurityStampAsync(user);

        if (await _userManager.IsInRoleAsync(user, "Reader"))
        {
            var notification = new Notification
            {
                UserId = user.Id,
                Title = "Your account has been deactivated",
                Type = "Account",
                Message = "After reviewing the information provided during registration, we determined that it does not comply with the Private Library Community Guidelines. As a result, your account has been deactivated and access to your library account is no longer available. If you believe this decision was made in error, please contact the library administration for further clarification.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        TempData["AdminMessage"] = $"Account '{user.UserName}' was deactivated. The user will see the notice if they attempt to sign in.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public IActionResult Moderation() => View();

    [HttpGet]
    public async Task<IActionResult> Reviews()
    {
        var reviews = await _context.Reviews.AsNoTracking().Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(reviews);
    }

    [HttpGet]
    public async Task<IActionResult> Ratings()
    {
        var ratings = await _context.Ratings.AsNoTracking()
            .Include(r => r.User).Include(r => r.Book)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(ratings);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RatingDecision(int id, string decision)
    {
        if (decision is not ("Approved" or "Rejected")) return BadRequest();
        var rating = await _context.Ratings
            .Include(r => r.User)
            .Include(r => r.Book)
            .FirstOrDefaultAsync(r => r.RatingId == id);
        if (rating == null) return NotFound();

        rating.ModerationStatus = decision;
        await _context.SaveChangesAsync();
        await _notifications.NotifyUserAsync(rating.UserId,
            decision == "Approved" ? "Your rating was approved" : "Your rating was not approved",
            decision == "Approved"
                ? $"Your rating for “{rating.Book.Title}” has been approved and is now visible."
                : $"Your rating for “{rating.Book.Title}” was not approved. You can update it and submit it again.",
            "Rating", "Book", rating.BookId);

        var author = !string.IsNullOrWhiteSpace(rating.User.FirstName)
            ? rating.User.FirstName
            : rating.User.UserName ?? "A reader";
        var result = decision == "Approved" ? "was approved" : "was not approved";
        TempData["AdminMessage"] = $"Rating by {author} for “{rating.Book.Title}” {result}.";
        return RedirectToAction(nameof(Ratings));
    }

    [HttpGet]
    public async Task<IActionResult> Events()
    {
        var events = await _context.Events.AsNoTracking().Include(e => e.Creator)
            .OrderByDescending(e => e.CreatedAt).ToListAsync();
        return View(events);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewDecision(int id, string decision)
    {
        if (decision is not ("Approved" or "Rejected")) return BadRequest();
        var review = await _context.Reviews
            .Include(r => r.User)
            .Include(r => r.Book)
            .FirstOrDefaultAsync(r => r.ReviewId == id);
        if (review == null) return NotFound();

        review.ModerationStatus = decision;
        await _context.SaveChangesAsync();
        await _notifications.NotifyUserAsync(review.UserId,
            decision == "Approved" ? "Your review was approved" : "Your review was not approved",
            decision == "Approved"
                ? $"Your review for “{review.Book.Title}” has been approved and is now visible."
                : $"Your review for “{review.Book.Title}” was not approved. You can edit it and submit it again.",
            "Review", "Book", review.BookId);

        var author = !string.IsNullOrWhiteSpace(review.User.FirstName)
            ? review.User.FirstName
            : review.User.UserName ?? "A reader";
        var result = decision == "Approved" ? "was approved" : "was not approved";
        TempData["AdminMessage"] = $"Review by {author} for “{review.Book.Title}” {result}.";
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EventDecision(int id, string decision)
    {
        if (decision is not ("Approved" or "Rejected")) return BadRequest();
        var item = await _context.Events.FindAsync(id);
        if (item == null) return NotFound();
        if (decision == "Approved" && item.Location == "In Library")
        {
            var start = item.EventDate;
            var end = start.AddMinutes(item.DurationMinutes);
            var sameDayEvents = await _context.Events
                .AsNoTracking()
                .Where(e => e.EventId != item.EventId &&
                            e.Location == "In Library" &&
                            e.Status != "Cancelled" &&
                            (e.ModerationStatus == "Approved" || e.ModerationStatus == null) &&
                            e.EventDate >= start.Date.AddHours(9) &&
                            e.EventDate < start.Date.AddHours(20))
                .Select(e => new { e.EventDate, e.DurationMinutes })
                .ToListAsync();

            var conflict = sameDayEvents.Any(e =>
                start < e.EventDate.AddMinutes(e.DurationMinutes) &&
                end > e.EventDate);

            if (conflict)
            {
                TempData["AdminMessage"] = "This event cannot be approved because the library is already booked for an overlapping time.";
                return RedirectToAction(nameof(Events));
            }
        }

        item.ModerationStatus = decision;
        await _context.SaveChangesAsync();
        await _notifications.NotifyUserAsync(item.CreatedBy,
            decision == "Approved" ? "Your event was approved" : "Your event was not approved",
            decision == "Approved"
                ? $"Your event “{item.Title}” has been approved and is now available to readers."
                : $"Your event “{item.Title}” was not approved. Please contact the library administration if you need clarification.",
            "Event", "Event", item.EventId);
        TempData["AdminMessage"] = $"Event {decision.ToLowerInvariant()}.";
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEventVisibility(int id)
    {
        var item = await _context.Events.FindAsync(id);
        if (item == null) return NotFound();

        item.IsVisible = !item.IsVisible;
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = item.IsVisible
            ? $"The event “{item.Title}” is now visible in the public events list."
            : $"The event “{item.Title}” has been hidden from the public events list.";
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var item = await _context.Events
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (item == null) return NotFound();

        // Notify registered participants before removing the event.
        await _notifications.NotifyEventParticipantsAsync(item.EventId, "Event cancelled",
            $"The event “{item.Title}” has been cancelled by the library administration.");

        // Remove registrations first so no orphaned participant records remain.
        _context.EventParticipants.RemoveRange(item.Participants);
        _context.Events.Remove(item);
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = $"The event “{item.Title}” and its registrations have been deleted.";
        return RedirectToAction(nameof(Events));
    }




    // Administrative catalog management
    [HttpGet]
    public async Task<IActionResult> Books(
        string? search,
        string status = "active",
        int? genreId = null,
        string sort = "title",
        int page = 1)
    {
        const int pageSize = 12;
        status = status is "all" or "archived" ? status : "active";
        sort = sort is "title_desc" or "year_desc" or "year_asc" or "copies_desc" ? sort : "title";
        page = Math.Max(1, page);

        var query = _context.Books.AsNoTracking()
            .Include(b => b.Genre)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.Copies)
            .AsQueryable();

        if (status == "active")
            query = query.Where(b => !b.IsArchived);
        else if (status == "archived")
            query = query.Where(b => b.IsArchived);

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
        var bookEntities = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var books = bookEntities.Select(b => new AdminBookListItemViewModel
        {
            BookId = b.BookId,
            Title = b.Title,
            ISBN = b.ISBN,
            Publisher = b.Publisher,
            PublicationYear = b.PublicationYear,
            GenreName = b.Genre?.Name ?? "Uncategorized",
            Authors = string.Join(", ", b.BookAuthors
                .Select(ba => ba.Author.FirstName + " " + ba.Author.LastName)),
            TotalCopies = b.Copies.Count,
            AvailableCopies = b.Copies.Count(c => c.Status == "Available"),
            IsArchived = b.IsArchived
        }).ToList();

        var model = new AdminBookManagementViewModel
        {
            Search = search,
            Status = status,
            GenreId = genreId,
            Sort = sort,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Books = books,
            Genres = await _context.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync(),
            ActiveCount = await _context.Books.CountAsync(b => !b.IsArchived),
            ArchivedCount = await _context.Books.CountAsync(b => b.IsArchived)
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
        TempData["AdminMessage"] = $"Book '{book.Title}' was added with {model.InitialCopies} copy/copies.";
        return RedirectToAction(nameof(Books));
    }

    
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
            TempData["AdminError"] = "Archived books cannot be edited. Restore the book first.";
            return RedirectToAction(nameof(Books), new { status = "archived" });
        }
        TempData["AdminMessage"] = $"Book '{book.Title}' was updated.";
        return RedirectToAction(nameof(Books));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBookCopies(int bookId, int targetCopyCount)
    {
        if (targetCopyCount < 0 || targetCopyCount > 1000)
        {
            TempData["AdminError"] = "The number of copies must be between 0 and 1000.";
            return RedirectToAction(nameof(Books));
        }
        var result = await _bookCatalog.IncreaseCopiesAsync(bookId, targetCopyCount);
        if (result.Book is null) return NotFound();
        if (result.WouldDecrease)
        {
            TempData["AdminError"] = "To reduce the number of copies, open Manage Copies and select the exact copy you want to remove.";
            return RedirectToAction(nameof(ManageCopies), new { bookId });
        }
        TempData["AdminMessage"] = $"The copy count for '{result.Book.Title}' is now {result.CurrentCount}.";
        return RedirectToAction(nameof(Books));
    }

    [HttpGet]
    public async Task<IActionResult> ManageCopies(int bookId)
    {
        var book = await _context.Books
            .AsNoTracking()
            .Include(b => b.Copies)
                .ThenInclude(c => c.Loans)
            .FirstOrDefaultAsync(b => b.BookId == bookId);

        if (book is null) return NotFound();

        var model = new BookCopyManagementViewModel
        {
            BookId = book.BookId,
            BookTitle = book.Title,
            IsArchived = book.IsArchived,
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
                    CanDelete = !book.IsArchived && c.Status == "Available" && c.Loans.Count == 0
                }).ToList()
        };

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBookCopy(int bookId, int copyId)
    {
        var copy = await _context.BookCopies
            .Include(c => c.Loans)
            .Include(c => c.Book)
            .FirstOrDefaultAsync(c => c.CopyId == copyId && c.BookId == bookId);

        if (copy is null)
        {
            TempData["AdminError"] = "The selected copy could not be found.";
            return RedirectToAction(nameof(ManageCopies), new { bookId });
        }
        if (copy.Book.IsArchived)
        {
            TempData["AdminError"] = "Archived books cannot be modified. Restore the book first.";
            return RedirectToAction(nameof(ManageCopies), new { bookId });
        }

        if (copy.Status != "Available" || copy.Loans.Count > 0)
        {
            TempData["AdminError"] =
                $"Copy {copy.InventoryNumber} cannot be deleted because it is not an unused available copy or it has loan history.";
            return RedirectToAction(nameof(ManageCopies), new { bookId });
        }

        var inventoryNumber = copy.InventoryNumber;
        _context.BookCopies.Remove(copy);

        try
        {
            await _context.SaveChangesAsync();
            TempData["AdminMessage"] = $"Copy {inventoryNumber} was deleted.";
        }
        catch (DbUpdateException)
        {
            TempData["AdminError"] =
                $"Copy {inventoryNumber} could not be deleted because another record refers to it. Its history has been preserved.";
        }

        return RedirectToAction(nameof(ManageCopies), new { bookId });
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveBook(int bookId)
    {
        var book = await _context.Books
            .AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new { b.BookId, b.Title, b.IsArchived })
            .FirstOrDefaultAsync();

        if (book is null)
            return NotFound();

        if (book.IsArchived)
        {
            TempData["AdminMessage"] = $"'{book.Title}' is already archived.";
            return RedirectToAction(nameof(Books), new { status = "archived" });
        }

        var affectedRows = await _context.Books
            .Where(b => b.BookId == bookId && !b.IsArchived)
            .ExecuteUpdateAsync(update => update
                .SetProperty(b => b.IsArchived, true));

        if (affectedRows != 1)
        {
            TempData["AdminError"] = $"Could not archive '{book.Title}'. Please refresh the catalog and try again.";
            return RedirectToAction(nameof(Books), new { status = "active" });
        }

        TempData["AdminMessage"] = $"'{book.Title}' was moved to the archive and hidden from readers.";
        return RedirectToAction(nameof(Books), new { status = "archived" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreBook(int bookId)
    {
        var book = await _context.Books
            .AsNoTracking()
            .Where(b => b.BookId == bookId)
            .Select(b => new { b.BookId, b.Title, b.IsArchived })
            .FirstOrDefaultAsync();

        if (book is null)
            return NotFound();

        if (!book.IsArchived)
        {
            TempData["AdminMessage"] = $"'{book.Title}' is already active.";
            return RedirectToAction(nameof(Books), new { status = "active" });
        }

        var affectedRows = await _context.Books
            .Where(b => b.BookId == bookId && b.IsArchived)
            .ExecuteUpdateAsync(update => update
                .SetProperty(b => b.IsArchived, false));

        if (affectedRows != 1)
        {
            TempData["AdminError"] = $"Could not restore '{book.Title}'. Please refresh the catalog and try again.";
            return RedirectToAction(nameof(Books), new { status = "archived" });
        }

        TempData["AdminMessage"] = $"'{book.Title}' was restored to the active catalog.";
        return RedirectToAction(nameof(Books), new { status = "active" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBook(int bookId)
    {
        var book = await _context.Books
            .Include(b => b.Copies)
            .Include(b => b.BookAuthors)
            .FirstOrDefaultAsync(b => b.BookId == bookId);
        if (book is null) return NotFound();

        var hasReservations = await _context.Reservations.AnyAsync(r => r.BookId == bookId);
        var hasLoans = await _context.Loans.AnyAsync(l => l.Copy.BookId == bookId);
        var hasReviews = await _context.Reviews.AnyAsync(r => r.BookId == bookId);
        var hasRatings = await _context.Ratings.AnyAsync(r => r.BookId == bookId);
        var hasFavorites = await _context.Favorites.AnyAsync(f => f.BookId == bookId);

        if (hasReservations || hasLoans || hasReviews || hasRatings || hasFavorites ||
            book.Copies.Any(c => c.Status != "Available"))
        {
            book.IsArchived = true;
            await _context.SaveChangesAsync();
            TempData["AdminMessage"] =
                $"'{book.Title}' has related history, so it was archived instead of permanently deleted.";
            return RedirectToAction(nameof(Books), new { status = "archived" });
        }

        _context.BookAuthors.RemoveRange(book.BookAuthors);
        _context.BookCopies.RemoveRange(book.Copies);
        _context.Books.Remove(book);
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = $"Book '{book.Title}' was permanently deleted.";
        return RedirectToAction(nameof(Books));
    }}


using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Services;

public sealed class BookCatalogService
{
    private const string UkrainianLanguageCode = "uk";

    private readonly ApplicationDbContext _context;

    public BookCatalogService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<Genre>> GetGenresAsync() =>
        _context.Genres
            .AsNoTracking()
            .Include(g => g.Translations)
            .OrderBy(g => g.Name)
            .ToListAsync();

    public async Task<AdminBookViewModel?> GetBookForEditAsync(int bookId)
    {
        var book = await _context.Books
            .AsNoTracking()
            .Include(b => b.Translations)
            .Include(b => b.BookAuthors)
                .ThenInclude(ba => ba.Author)
                    .ThenInclude(a => a.Translations)
            .FirstOrDefaultAsync(b =>
                b.BookId == bookId && !b.IsArchived);

        if (book is null)
            return null;

        var orderedAuthors = book.BookAuthors
            .OrderBy(ba => ba.AuthorId)
            .Select(ba => ba.Author)
            .ToList();

        var ukrainianAuthorTranslations = orderedAuthors
            .Select(author => author.Translations.FirstOrDefault(
                t => t.LanguageCode == UkrainianLanguageCode))
            .ToList();

        var allAuthorsHaveUkrainianNames =
            orderedAuthors.Count > 0 &&
            ukrainianAuthorTranslations.All(t => t is not null);

        var ukrainianBookTranslation = book.Translations
            .FirstOrDefault(t =>
                t.LanguageCode == UkrainianLanguageCode);

        return new AdminBookViewModel
        {
            BookId = book.BookId,

            Title = book.Title,
            Description = book.Description,

            TitleUk = ukrainianBookTranslation?.Title ?? string.Empty,
            DescriptionUk = ukrainianBookTranslation?.Description,

            AuthorNames = string.Join(
                Environment.NewLine,
                orderedAuthors.Select(a =>
                    $"{a.FirstName} {a.LastName}")),

            AuthorNamesUk = allAuthorsHaveUkrainianNames
                ? string.Join(
                    Environment.NewLine,
                    ukrainianAuthorTranslations.Select(t =>
                        $"{t!.FirstName} {t.LastName}"))
                : string.Empty,

            ISBN = book.ISBN,
            PublicationYear = book.PublicationYear,
            Publisher = book.Publisher,
            Language = book.Language,
            CoverImageUrl = book.CoverImageUrl,
            GenreId = book.GenreId,
            ActiveLanguage = "en"
        };
    }

    public async Task<Book> CreateBookAsync(AdminBookViewModel model)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var book = new Book
        {
            Title = model.Title.Trim(),
            Description = Clean(model.Description),
            ISBN = Clean(model.ISBN),
            PublicationYear = model.PublicationYear,
            Publisher = Clean(model.Publisher),
            Language = Clean(model.Language),
            CoverImageUrl = Clean(model.CoverImageUrl),
            GenreId = model.GenreId,
            IsArchived = false
        };

        _context.Books.Add(book);
        await _context.SaveChangesAsync();

        await SyncBookTranslationAsync(book.BookId, model);

        await SyncAuthorsAsync(
            book.BookId,
            model.AuthorNames,
            model.AuthorNamesUk);

        AddCopies(book, model.InitialCopies);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return book;
    }

    public async Task<Book?> UpdateActiveBookAsync(
        AdminBookViewModel model)
    {
        var book = await _context.Books
            .FirstOrDefaultAsync(b =>
                b.BookId == model.BookId && !b.IsArchived);

        if (book is null)
            return null;

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        book.Title = model.Title.Trim();
        book.Description = Clean(model.Description);
        book.ISBN = Clean(model.ISBN);
        book.PublicationYear = model.PublicationYear;
        book.Publisher = Clean(model.Publisher);
        book.Language = Clean(model.Language);
        book.CoverImageUrl = Clean(model.CoverImageUrl);
        book.GenreId = model.GenreId;

        await SyncBookTranslationAsync(book.BookId, model);

        await SyncAuthorsAsync(
            book.BookId,
            model.AuthorNames,
            model.AuthorNamesUk);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return book;
    }

    private async Task SyncBookTranslationAsync(
        int bookId,
        AdminBookViewModel model)
    {
        var translation = await _context.BookTranslations
            .FirstOrDefaultAsync(t =>
                t.BookId == bookId &&
                t.LanguageCode == UkrainianLanguageCode);

        if (translation is null)
        {
            translation = new BookTranslation
            {
                BookId = bookId,
                LanguageCode = UkrainianLanguageCode
            };

            _context.BookTranslations.Add(translation);
        }

        translation.Title = model.TitleUk.Trim();
        translation.Description = Clean(model.DescriptionUk);
    }

    private async Task SyncAuthorsAsync(
        int bookId,
        string englishAuthorNames,
        string ukrainianAuthorNames)
    {
        var englishNames = ParseAuthorNames(englishAuthorNames);
        var ukrainianNames = ParseAuthorNames(ukrainianAuthorNames);

        if (englishNames.Count != ukrainianNames.Count)
        {
            throw new InvalidOperationException(
                "The English and Ukrainian author lists must contain the same number of authors.");
        }

        var existingLinks = await _context.BookAuthors
            .Where(link => link.BookId == bookId)
            .ToListAsync();

        _context.BookAuthors.RemoveRange(existingLinks);
        await _context.SaveChangesAsync();

        var existingAuthors = await _context.Authors.ToListAsync();

        var newLinks = new List<BookAuthor>();
        var linkedAuthorIds = new HashSet<int>();

        for (var i = 0; i < englishNames.Count; i++)
        {
            var englishName = englishNames[i];
            var ukrainianName = ukrainianNames[i];

            var author = existingAuthors.FirstOrDefault(item =>
                string.Equals(
                    item.FirstName,
                    englishName.FirstName,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    item.LastName,
                    englishName.LastName,
                    StringComparison.OrdinalIgnoreCase));

            if (author is null)
            {
                author = new Author
                {
                    FirstName = englishName.FirstName,
                    LastName = englishName.LastName
                };

                _context.Authors.Add(author);
                await _context.SaveChangesAsync();

                existingAuthors.Add(author);
            }

            if (linkedAuthorIds.Add(author.AuthorId))
            {
                newLinks.Add(new BookAuthor
                {
                    BookId = bookId,
                    AuthorId = author.AuthorId
                });
            }

            await SyncAuthorTranslationAsync(
                author.AuthorId,
                ukrainianName);
        }

        _context.BookAuthors.AddRange(newLinks);
        await _context.SaveChangesAsync();
    }

    private async Task SyncAuthorTranslationAsync(
        int authorId,
        (string FirstName, string LastName) ukrainianName)
    {
        var translation = await _context.AuthorTranslations
            .FirstOrDefaultAsync(t =>
                t.AuthorId == authorId &&
                t.LanguageCode == UkrainianLanguageCode);

        if (translation is null)
        {
            translation = new AuthorTranslation
            {
                AuthorId = authorId,
                LanguageCode = UkrainianLanguageCode
            };

            _context.AuthorTranslations.Add(translation);
        }

        translation.FirstName = ukrainianName.FirstName;
        translation.LastName = ukrainianName.LastName;
    }

    private static List<(string FirstName, string LastName)> ParseAuthorNames(
        string names)
    {
        return names
            .Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Select(value =>
            {
                var parts = value.Split(
                    ' ',
                    2,
                    StringSplitOptions.RemoveEmptyEntries);

                return (
                    FirstName: parts[0],
                    LastName: parts.Length > 1 ? parts[1] : string.Empty
                );
            })
            .ToList();
    }

    public async Task<(Book? Book, int CurrentCount, bool WouldDecrease)>
        IncreaseCopiesAsync(int bookId, int targetCount)
    {
        var book = await _context.Books
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b =>
                b.BookId == bookId && !b.IsArchived);

        if (book is null)
            return (null, 0, false);

        var currentCount = book.Copies.Count;

        if (targetCount < currentCount)
            return (book, currentCount, true);

        if (targetCount == currentCount)
            return (book, currentCount, false);

        AddCopies(
            book,
            targetCount - currentCount,
            book.Copies.Select(c => c.InventoryNumber));

        await _context.SaveChangesAsync();

        return (book, targetCount, false);
    }

    private void AddCopies(
        Book book,
        int count,
        IEnumerable<string>? existingInventoryNumbers = null)
    {
        var existing = (existingInventoryNumbers ??
                        Enumerable.Empty<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nextNumber = 1;
        var added = 0;

        while (added < count)
        {
            var inventoryNumber =
                $"PL-{book.BookId:D6}-{nextNumber:D3}";

            nextNumber++;

            if (!existing.Add(inventoryNumber))
                continue;

            _context.BookCopies.Add(new BookCopy
            {
                BookId = book.BookId,
                InventoryNumber = inventoryNumber,
                Status = "Available",
                Condition = "Good",
                Location = "Main Library"
            });

            added++;
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}

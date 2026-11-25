
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Controllers;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using Xunit;

namespace PrivateLibrary.Tests;

public class BookSearchFunctionalTests
{
    private async Task<ApplicationDbContext> CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);

        context.Books.AddRange(
            new Book
            {
                Title = "Alice's Adventures in Wonderland",
                IsArchived = false
            },
            new Book
            {
                Title = "The Little Prince",
                IsArchived = false
            },
            new Book
            {
                Title = "24",
                IsArchived = true
            });

        await context.SaveChangesAsync();

        return context;
    }

    private BooksController CreateController(ApplicationDbContext context)
    {
        return new BooksController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact(DisplayName = "Пошук книг - знаходження активної книги Alice's Adventures in Wonderland")]
    public async Task SearchBooks_ShouldFindActiveBook()
    {
        await using var context = await CreateDatabase();
        var controller = CreateController(context);

        var result = await controller.Index(
            "Alice's Adventures in Wonderland",
            null,
            null,
            null,
            null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var books = Assert.IsAssignableFrom<IEnumerable<Book>>(viewResult.Model);

        Assert.Single(books);
        Assert.Equal("Alice's Adventures in Wonderland", books.First().Title);
        Assert.Equal("Alice's Adventures in Wonderland", controller.ViewData["Search"]);
    }

    [Fact(DisplayName = "Пошук книг - архівована книга 24 не відображається у результатах")]
    public async Task SearchBooks_ShouldNotFindArchivedBook()
    {
        await using var context = await CreateDatabase();
        var controller = CreateController(context);

        var result = await controller.Index(
            "24",
            null,
            null,
            null,
            null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var books = Assert.IsAssignableFrom<IEnumerable<Book>>(viewResult.Model);

        Assert.Empty(books);
        Assert.Equal("24", controller.ViewData["Search"]);
    }
}

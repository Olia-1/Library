
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrivateLibrary.Controllers;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using Xunit;

namespace PrivateLibrary.Tests;

public class BookReservationIntegrationTests
{
    [Fact(DisplayName = "Бронювання книги - збереження бронювання, зміна статусу примірника та створення сповіщення")]
    public async Task CreateReservation_ShouldSaveReservation_UpdateBookCopy_AndCreateNotification()
    {
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services.AddIdentityCore<User>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<User>>();

        var user = new User
        {
            UserName = $"reader_{Guid.NewGuid():N}",
            FirstName = "Test",
            LastName = "Reader"
        };

        var userCreationResult = await userManager.CreateAsync(user);

        Assert.True(
            userCreationResult.Succeeded,
            string.Join("; ", userCreationResult.Errors.Select(e => e.Description)));

        var book = new Book
        {
            Title = "Integration Test Book"
        };

        context.Books.Add(book);
        await context.SaveChangesAsync();

        var bookCopy = new BookCopy
        {
            BookId = book.BookId,
            InventoryNumber = "TEST-001",
            Status = "Available"
        };

        context.BookCopies.Add(bookCopy);
        await context.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();

        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                },
                "TestAuthentication"));

        var controller = new ReservationsController(context, userManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(
                httpContext,
                new TestTempDataProvider())
        };

        var result = await controller.Create(book.BookId);

        var reservation = await context.Reservations
            .SingleAsync(r => r.UserId == user.Id && r.BookId == book.BookId);

        var updatedBookCopy = await context.BookCopies
            .SingleAsync(c => c.CopyId == bookCopy.CopyId);

        var notification = await context.Notifications
            .SingleAsync(n => n.UserId == user.Id);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", ((RedirectToActionResult)result).ActionName);

        Assert.Equal("Ready", reservation.Status);
        Assert.Null(reservation.QueuePosition);
        Assert.NotNull(reservation.ExpirationDate);

        Assert.Equal("Reserved", updatedBookCopy.Status);

        Assert.Equal("Book reservation", notification.Title);
        Assert.Equal("Reservation", notification.Type);
        Assert.Equal(reservation.ReservationId, notification.RelatedEntityId);
    }

    private class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values)
        {
        }
    }
}

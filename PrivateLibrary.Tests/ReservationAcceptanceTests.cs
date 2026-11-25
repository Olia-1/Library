
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

public class ReservationAcceptanceTests
{
    [Fact(DisplayName = "Приймальне тестування - читач не може мати більше 7 активних бронювань")]
    public async Task CreateReservation_ShouldRejectEighthActiveReservation()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddIdentityCore<User>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = await CreateUserAsync(userManager, "reader");

        var books = Enumerable.Range(1, 8)
            .Select(i => new Book { Title = $"Test Book {i}" })
            .ToList();

        context.Books.AddRange(books);
        await context.SaveChangesAsync();

        for (var i = 0; i < 7; i++)
        {
            context.Reservations.Add(new Reservation
            {
                UserId = user.Id,
                BookId = books[i].BookId,
                Status = "Pending",
                ReservationDate = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        var controller = CreateController(context, userManager, user.Id);

        var result = await controller.Create(books[7].BookId);

        var activeReservations = await context.Reservations
            .CountAsync(r =>
                r.UserId == user.Id &&
                (r.Status == "Pending" || r.Status == "Ready"));

        var newReservation = await context.Reservations
            .AnyAsync(r =>
                r.UserId == user.Id &&
                r.BookId == books[7].BookId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", ((RedirectToActionResult)result).ActionName);
        Assert.Equal(
            "You can have no more than 7 active reservations.",
            controller.TempData["Error"]);
        Assert.Equal(7, activeReservations);
        Assert.False(newReservation);
    }

    [Fact(DisplayName = "Приймальне тестування - система не дозволяє додати четвертого читача до черги очікування")]
    public async Task CreateReservation_ShouldRejectFourthReaderInWaitingQueue()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddIdentityCore<User>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var currentUser = await CreateUserAsync(userManager, "reader");

        var waitingUsers = new List<User>();

        for (var i = 1; i <= 3; i++)
        {
            waitingUsers.Add(await CreateUserAsync(userManager, $"waiting_reader_{i}"));
        }

        var book = new Book
        {
            Title = "Waiting Queue Test Book"
        };

        context.Books.Add(book);
        await context.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            context.Reservations.Add(new Reservation
            {
                UserId = waitingUsers[i].Id,
                BookId = book.BookId,
                Status = "Pending",
                QueuePosition = i + 1,
                ReservationDate = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        var controller = CreateController(context, userManager, currentUser.Id);

        var result = await controller.Create(book.BookId);

        var waitingCount = await context.Reservations
            .CountAsync(r =>
                r.BookId == book.BookId &&
                r.Status == "Pending");

        var newReservation = await context.Reservations
            .AnyAsync(r =>
                r.UserId == currentUser.Id &&
                r.BookId == book.BookId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", ((RedirectToActionResult)result).ActionName);
        Assert.Equal(
            "The waiting list is full (maximum 3 readers).",
            controller.TempData["Error"]);
        Assert.Equal(3, waitingCount);
        Assert.False(newReservation);
    }

    private static async Task<User> CreateUserAsync(
        UserManager<User> userManager,
        string prefix)
    {
        var user = new User
        {
            UserName = $"{prefix}_{Guid.NewGuid():N}",
            FirstName = "Test",
            LastName = "Reader"
        };

        var result = await userManager.CreateAsync(user);

        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Errors.Select(e => e.Description)));

        return user;
    }

    private static ReservationsController CreateController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        int userId)
    {
        var httpContext = new DefaultHttpContext();

        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                },
                "TestAuthentication"));

        return new ReservationsController(context, userManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(
                httpContext,
                new TestTempDataProvider())
        };
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

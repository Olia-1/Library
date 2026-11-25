
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace PrivateLibrary.Tests;

public class LibrarySmokeTests
{
    [Fact(DisplayName = "Димове тестування - запуск сайту та доступність головної сторінки, каталогу, входу і реєстрації")]
    public async Task Application_ShouldStart_AndMainPagesShouldBeAvailable()
    {
        await using var factory = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:DatabaseName"] = $"Smoke_{Guid.NewGuid():N}"
            });
        });
    });
    

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = true
            });

        var pages = new Dictionary<string, string>
        {
            ["Головна сторінка"] = "/",
            ["Каталог книг"] = "/Books",
            ["Сторінка входу"] = "/Account/Login",
            ["Сторінка реєстрації"] = "/Account/Register"
        };

        foreach (var page in pages)
        {
            var response = await client.GetAsync(page.Value);

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{page.Key}: очікувався HTTP 200, отримано {(int)response.StatusCode}.");

            var content = await response.Content.ReadAsStringAsync();

            Assert.False(
                string.IsNullOrWhiteSpace(content),
                $"{page.Key}: сторінка повернула порожній вміст.");
        }
    }
}

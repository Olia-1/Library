
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace PrivateLibrary.Tests;

public class LibraryEndToEndTests
{
    [Fact(DisplayName = "End-to-End - реєстрація користувача, вхід у систему та перехід до каталогу")]
    public async Task UserJourney_ShouldRegister_Login_AndOpenBookCatalog()
    {
        await using var factory = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:DatabaseName"] = $"E2E_{Guid.NewGuid():N}"
            });
        });
    });

        using var registrationClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = true
            });

        var registrationPage = await registrationClient.GetAsync("/Account/Register");

        Assert.Equal(HttpStatusCode.OK, registrationPage.StatusCode);

        var registrationHtml = await registrationPage.Content.ReadAsStringAsync();
        var registrationToken = GetAntiForgeryToken(registrationHtml);

        var userName = $"e2e_reader_{Guid.NewGuid():N}";
        const string password = "TestPassword123";

        var registrationResponse = await registrationClient.PostAsync(
            "/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = registrationToken,
                ["UserName"] = userName,
                ["Email"] = "",
                ["Password"] = password,
                ["ConfirmPassword"] = password
            }));

        Assert.Equal(HttpStatusCode.OK, registrationResponse.StatusCode);
        Assert.EndsWith("/Books", registrationResponse.RequestMessage!.RequestUri!.AbsolutePath);

        using var loginClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = true
            });

        var loginPage = await loginClient.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);

        var loginHtml = await loginPage.Content.ReadAsStringAsync();
        var loginToken = GetAntiForgeryToken(loginHtml);

        var loginResponse = await loginClient.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = loginToken,
                ["UserName"] = userName,
                ["Password"] = password,
                ["RememberMe"] = "false"
            }));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.EndsWith("/Books", loginResponse.RequestMessage!.RequestUri!.AbsolutePath);

        var catalogResponse = await loginClient.GetAsync("/Books");

       var catalogContent = await catalogResponse.Content.ReadAsStringAsync();

Assert.True(
    catalogResponse.StatusCode == HttpStatusCode.OK,
    $"Catalog returned {(int)catalogResponse.StatusCode} ({catalogResponse.StatusCode}). Response: {catalogContent}");
    }

    private static string GetAntiForgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        Assert.True(match.Success, "Anti-forgery token was not found.");

        return match.Groups[1].Value;
    }
}

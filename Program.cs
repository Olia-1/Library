using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<BookCatalogService>();
builder.Services.AddScoped<NotificationMaintenanceService>();
builder.Services.AddHostedService<NotificationReminderWorker>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found. Configure it with User Secrets.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services
    .AddIdentity<User, IdentityRole<int>>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedEmail = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Seed the roles required by the application. The Identity tables must already exist.
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    string[] roles = ["Reader", "Librarian", "Admin"];

    foreach (var roleName in roles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole<int>(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role '{roleName}': " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

var englishCulture = CultureInfo.GetCultureInfo("en-GB");
var ukrainianCulture = CultureInfo.GetCultureInfo("uk-UA");
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(englishCulture),
    SupportedCultures = new[] { englishCulture, ukrainianCulture },
    SupportedUICultures = new[] { englishCulture, ukrainianCulture },
    RequestCultureProviders =
    {
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    }
};
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Lifetime.ApplicationStarted.Register(() =>
{
    const int httpPort = 5100;
    var localUrl = $"http://localhost:{httpPort}";

    Console.WriteLine();
    Console.WriteLine("==================================================");
    Console.WriteLine(" PrivateLibrary is running");
    Console.WriteLine("==================================================");
    Console.WriteLine($"Local address:  http://localhost:{httpPort}");
    Console.WriteLine($"Local address:  http://127.0.0.1:{httpPort}");

    var localAddresses = NetworkInterface.GetAllNetworkInterfaces()
        .Where(network =>
            network.OperationalStatus == OperationalStatus.Up &&
            network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(network => network.GetIPProperties().UnicastAddresses)
        .Where(address =>
            address.Address.AddressFamily == AddressFamily.InterNetwork &&
            !IPAddress.IsLoopback(address.Address) &&
            !address.Address.ToString().StartsWith("169.254."))
        .Select(address => address.Address.ToString())
        .Distinct()
        .OrderBy(address => address)
        .ToList();

    if (localAddresses.Count == 0)
    {
        Console.WriteLine("No local network IPv4 address was found.");
        Console.WriteLine("Connect this computer to Wi-Fi or Ethernet and restart the application.");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine("Open from another device on the same Wi-Fi/LAN:");

        foreach (var address in localAddresses)
        {
            Console.WriteLine($"  http://{address}:{httpPort}");
        }
    }

    Console.WriteLine("==================================================");
    Console.WriteLine();

    // 0.0.0.0 is a listening address, not a browser destination.
    // Open the local site through localhost after Kestrel has started.
    if (app.Environment.IsDevelopment())
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = localUrl,
                UseShellExecute = true
            });
            Console.WriteLine($"Browser opened: {localUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not open the browser automatically: {ex.Message}");
            Console.WriteLine($"Open the site manually: {localUrl}");
        }
    }
});

app.Run();

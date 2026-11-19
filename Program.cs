using System.Net;
using System.Globalization;
using System.Net.NetworkInformation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

// Keep all server-rendered dates and month/day names in English,
// regardless of the operating system or browser language.
var englishCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = englishCulture;
CultureInfo.DefaultThreadCurrentUICulture = englishCulture;

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<NotificationService>();
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
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

Console.WriteLine();
Console.WriteLine("========================================");
Console.WriteLine("          PRIVATE LIBRARY");
Console.WriteLine("========================================");
Console.WriteLine("Local address:   http://localhost:5100");
Console.WriteLine("Network addresses:");

var networkAddresses = NetworkInterface.GetAllNetworkInterfaces()
    .Where(network =>
        network.OperationalStatus == OperationalStatus.Up &&
        network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
    .SelectMany(network => network.GetIPProperties().UnicastAddresses)
    .Where(address =>
        address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
        !IPAddress.IsLoopback(address.Address))
    .Select(address => address.Address.ToString())
    .Distinct()
    .ToList();

if (networkAddresses.Count == 0)
{
    Console.WriteLine("  No active IPv4 network address found.");
}
else
{
    foreach (var address in networkAddresses)
    {
        Console.WriteLine($"  http://{address}:5100");
    }
}

Console.WriteLine("========================================");
Console.WriteLine();

app.Run();

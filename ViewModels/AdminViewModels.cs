using System.ComponentModel.DataAnnotations;
using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalBooks { get; set; }
    public int TotalCopies { get; set; }
    public int ActiveLoans { get; set; }
    public int PendingReviews { get; set; }
    public int PendingEvents { get; set; }
    public int PendingReservations { get; set; }
    public List<AdminActivityViewModel> RecentActivities { get; set; } = new();
    public List<AdminModerationItemViewModel> PendingReviewItems { get; set; } = new();
    public List<AdminModerationItemViewModel> PendingEventItems { get; set; } = new();
}

public class AdminActivityViewModel
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Status { get; set; } = "";
}

public class AdminModerationItemViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Description { get; set; } = "";
}

public class AdminModerationViewModel
{
    public List<Review> PendingReviews { get; set; } = new();
    public List<Event> PendingEvents { get; set; } = new();
}

public class AdminUserViewModel
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string? Email { get; set; }
    public string Roles { get; set; } = "";
}

public class AdminCreateUserViewModel
{
    [Required, StringLength(50, MinimumLength = 3)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = "";

    [EmailAddress]
    [Display(Name = "Email address (optional)")]
    public string? Email { get; set; }

    [Required, MinLength(8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    public string Role { get; set; } = "Reader";
}

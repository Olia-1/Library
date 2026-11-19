using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class MyReviewsViewModel
{
    public string Filter { get; set; } = "all";
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int PublishedCount { get; set; }
    public int RejectedCount { get; set; }
    public List<Review> Reviews { get; set; } = new();
}

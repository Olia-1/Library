using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class BookDetailsViewModel
{
    public Book Book { get; set; } = null!;
    public List<Review> ApprovedReviews { get; set; } = new();
    public List<Rating> ApprovedRatings { get; set; } = new();
    public Rating? MyRating { get; set; }
    public Review? MyReview { get; set; }
    public bool CanViewCommunityContent { get; set; }
    public double AverageRating => ApprovedRatings.Count == 0
        ? 0
        : ApprovedRatings.Average(x => x.RatingValue);
}

namespace PrivateLibrary.ViewModels;

public class CommunityViewModel
{
    public bool IsAuthenticatedReader { get; set; }
    public List<CommunityReviewItemViewModel> LatestReviews { get; set; } = new();
    public List<CommunityPopularBookViewModel> MostRatedBooks { get; set; } = new();
}

public class CommunityReviewItemViewModel
{
    public int ReviewId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public int LikesCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
}

public class CommunityPopularBookViewModel
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string Authors { get; set; } = string.Empty;
    public int RatingsCount { get; set; }
    public double AverageRating { get; set; }
}

using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class AccountDashboardViewModel
{
    public User User { get; set; } = null!;
    public int ActiveReservationsCount { get; set; }
    public int FavoritesCount { get; set; }
    public int ReviewsCount { get; set; }
    public int ActiveLoansCount { get; set; }
    public int EventsCount { get; set; }
    public int UnreadNotificationsCount { get; set; }
    public Subscription? ActiveSubscription { get; set; }
    public List<Reservation> RecentReservations { get; set; } = new();
    public List<Favorite> FavoriteBooks { get; set; } = new();
    public List<Review> RecentReviews { get; set; } = new();
    public List<Loan> ActiveLoans { get; set; } = new();
    public List<EventParticipant> UpcomingEvents { get; set; } = new();
    public List<Notification> RecentNotifications { get; set; } = new();
}

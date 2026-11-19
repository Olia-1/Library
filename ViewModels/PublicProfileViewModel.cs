using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class PublicProfileViewModel
{
    public User User { get; set; } = null!;
    public List<Favorite> Favorites { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public List<Rating> Ratings { get; set; } = new();
    public bool FavoritesArePublic { get; set; }
}

using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class PopularBookViewModel
{
    public Book Book { get; set; } = null!;
    public int FavoritesCount { get; set; }
    public int Position { get; set; }
}

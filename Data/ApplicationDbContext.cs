using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Models;

namespace PrivateLibrary.Data;

public class ApplicationDbContext
    : IdentityDbContext<User, IdentityRole<int>, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BookAuthor>()
            .HasKey(x => new { x.BookId, x.AuthorId });

        builder.Entity<BookAuthor>()
            .HasOne(x => x.Book).WithMany(x => x.BookAuthors)
            .HasForeignKey(x => x.BookId);

        builder.Entity<BookAuthor>()
            .HasOne(x => x.Author).WithMany(x => x.BookAuthors)
            .HasForeignKey(x => x.AuthorId);

        builder.Entity<Book>()
            .HasOne(x => x.Genre).WithMany(x => x.Books)
            .HasForeignKey(x => x.GenreId);

        builder.Entity<BookCopy>()
            .HasOne(x => x.Book).WithMany(x => x.Copies)
            .HasForeignKey(x => x.BookId);

        builder.Entity<Reservation>()
            .HasOne(x => x.User).WithMany(x => x.Reservations)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Reservation>()
            .HasOne(x => x.Book).WithMany(x => x.Reservations)
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Loan>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Loan>()
            .HasOne(x => x.Librarian).WithMany()
            .HasForeignKey(x => x.LibrarianId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Subscription>()
            .HasOne(x => x.User).WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Subscription>()
            .HasOne(x => x.MembershipPlan).WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.PlanId);

        builder.Entity<Notification>()
            .HasOne(x => x.User).WithMany(x => x.Notifications)
            .HasForeignKey(x => x.UserId);
    }
}

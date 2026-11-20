namespace PrivateLibrary.ViewModels;

public class LibrarianDashboardViewModel
{
    public int TotalBooks { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
    public int PendingReservations { get; set; }
    public int ReadyReservations { get; set; }
    public List<LibrarianLoanItemViewModel> RecentLoans { get; set; } = new();
}

public class LibrarianIssueReturnViewModel
{
    public string ActiveTab { get; set; } = "issue";
    public string ReaderSearch { get; set; } = "";
    public string BookSearch { get; set; } = "";
    public string LoanSearch { get; set; } = "";
    public int? SelectedReaderId { get; set; }
    public int? SelectedBookId { get; set; }
    public int? SelectedCopyId { get; set; }
    public DateTime DueDate { get; set; }
    public List<LibrarianReaderOption> Readers { get; set; } = new();
    public List<LibrarianBookOption> Books { get; set; } = new();
    public List<LibrarianLoanItemViewModel> ActiveLoans { get; set; } = new();
}

public class LibrarianReaderOption
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
}

public class LibrarianBookOption
{
    public int BookId { get; set; }
    public string Title { get; set; } = "";
    public string Authors { get; set; } = "";
    public string Isbn { get; set; } = "";
    public string Genre { get; set; } = "";
    public string? CoverImageUrl { get; set; }
    public List<LibrarianCopyOption> Copies { get; set; } = new();
}

public class LibrarianCopyOption
{
    public int CopyId { get; set; }
    public string InventoryNumber { get; set; } = "";
    public string Condition { get; set; } = "";
    public string Location { get; set; } = "";
    public string Status { get; set; } = "";
}

public class LibrarianLoanItemViewModel
{
    public int LoanId { get; set; }
    public string BookTitle { get; set; } = "";
    public string ReaderName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string CopyNumber { get; set; } = "";
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "";
}

public class LibrarianReservationsViewModel
{
    public string Search { get; set; } = "";
    public string Status { get; set; } = "";
    public int PendingCount { get; set; }
    public int ReadyCount { get; set; }
    public int BooksCount { get; set; }
    public List<LibrarianReservationBookGroup> Books { get; set; } = new();
}

public class LibrarianReservationBookGroup
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = "";
    public string? CoverImageUrl { get; set; }
    public int PendingCount { get; set; }
    public int ReadyCount { get; set; }
    public List<LibrarianReservationItem> Reservations { get; set; } = new();
}

public class LibrarianReservationItem
{
    public int ReservationId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string ReaderName { get; set; } = "";
    public DateTime ReservationDate { get; set; }
    public int? QueuePosition { get; set; }
    public string Status { get; set; } = "";
    public DateTime? ExpirationDate { get; set; }
}

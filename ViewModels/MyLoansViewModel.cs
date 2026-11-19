using PrivateLibrary.Models;

namespace PrivateLibrary.ViewModels;

public class MyLoansViewModel
{
    public string Filter { get; set; } = "all";
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int OverdueCount { get; set; }
    public int ReturnedCount { get; set; }
    public List<Loan> Loans { get; set; } = new();
}

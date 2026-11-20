namespace PrivateLibrary.ViewModels;

public class TestCardCheckoutViewModel
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }

    public string CardholderName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public string Cvc { get; set; } = string.Empty;
}

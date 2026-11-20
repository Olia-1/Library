using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Data;
using PrivateLibrary.Models;
using PrivateLibrary.Services;
using PrivateLibrary.ViewModels;

namespace PrivateLibrary.Controllers;

[Authorize(Roles = "Reader")]
public class MembershipController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly NotificationService _notificationService;
    private readonly ILogger<MembershipController> _logger;

    public MembershipController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        NotificationService notificationService,
        ILogger<MembershipController> logger)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Plans()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var now = DateTime.UtcNow;

        var activeSubscription = await _context.Subscriptions
            .AsNoTracking()
            .Where(s => s.UserId == user.Id &&
                        s.Status == "Active" &&
                        s.StartDate <= now &&
                        s.EndDate > now)
            .Include(s => s.MembershipPlan)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

        // Only completed payments count as purchased memberships.
        // Load all completed purchases so the total is not limited to the latest 10 rows.
        var purchasedPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.Subscription.UserId == user.Id && p.Status == "Completed")
            .Include(p => p.Subscription)
                .ThenInclude(s => s.MembershipPlan)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        // Protect the summary from accidentally counting a subscription twice
        // if duplicate completed payment rows ever exist for the same subscription.
        var uniquePurchasedPayments = purchasedPayments
            .GroupBy(p => p.SubscriptionId)
            .Select(g => g.First())
            .ToList();

        var recentPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.Subscription.UserId == user.Id)
            .Include(p => p.Subscription)
                .ThenInclude(s => s.MembershipPlan)
            .OrderByDescending(p => p.PaymentDate)
            .Take(10)
            .ToListAsync();

        var model = new MembershipPlansViewModel
        {
            Plans = await _context.MembershipPlans
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.DurationDays)
                .ToListAsync(),

            ActiveSubscription = activeSubscription,
            RecentPayments = recentPayments,
            PurchasedMemberships = uniquePurchasedPayments,
            TotalPurchasedDays = uniquePurchasedPayments.Sum(p => p.Subscription.MembershipPlan.DurationDays),
            MembershipValidUntil = uniquePurchasedPayments.Count > 0
                ? uniquePurchasedPayments.Max(p => p.Subscription.EndDate)
                : activeSubscription?.EndDate
        };

        return View(model);
    }

    private static bool PassesLuhnCheck(string digits)
    {
        if (string.IsNullOrEmpty(digits) || digits.Any(c => c < '0' || c > '9'))
            return false;

        var sum = 0;
        var doubleDigit = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';

            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int planId)
    {
        var plan = await _context.MembershipPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive);

        if (plan is null)
        {
            TempData["Error"] = "This membership plan is no longer available. Please refresh the page.";
            return RedirectToAction(nameof(Plans));
        }

        return View(new TestCardCheckoutViewModel
        {
            PlanId = plan.PlanId,
            PlanName = plan.Name,
            Price = plan.Price,
            DurationDays = plan.DurationDays
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTestCard(TestCardCheckoutViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var plan = await _context.MembershipPlans
            .FirstOrDefaultAsync(p => p.PlanId == model.PlanId && p.IsActive);

        if (plan is null)
        {
            TempData["Error"] = "This membership plan is no longer available. Please refresh the page.";
            return RedirectToAction(nameof(Plans));
        }

        model.PlanName = plan.Name;
        model.Price = plan.Price;
        model.DurationDays = plan.DurationDays;

        // Local structural validation only: length + Luhn checksum.
        // This does NOT verify that a real card exists or is active.
        // The card number is never persisted or written to application logs.
        var rawCardNumber = model.CardNumber ?? string.Empty;
        var digitsOnlyCardNumber = new string(rawCardNumber
            .Where(char.IsDigit)
            .ToArray());

        var containsOnlyAllowedCardCharacters = rawCardNumber
            .All(c => char.IsDigit(c) || char.IsWhiteSpace(c));

        var cardNumberIsStructurallyValid =
            containsOnlyAllowedCardCharacters &&
            digitsOnlyCardNumber.Length >= 13 &&
            digitsOnlyCardNumber.Length <= 19 &&
            PassesLuhnCheck(digitsOnlyCardNumber);

        var expiryIsValid =
            int.TryParse(model.ExpiryMonth, out var expiryMonth) &&
            int.TryParse(model.ExpiryYear, out var expiryYear) &&
            expiryMonth >= 1 && expiryMonth <= 12 &&
            (expiryYear > DateTime.UtcNow.Year ||
             (expiryYear == DateTime.UtcNow.Year && expiryMonth >= DateTime.UtcNow.Month));

        if (!cardNumberIsStructurallyValid)
            ModelState.AddModelError(nameof(model.CardNumber),
                "Enter 13–19 digits with a valid checksum. Spaces are allowed; hyphens and other characters are not. Use test data only.");

        if (!expiryIsValid)
            ModelState.AddModelError(nameof(model.ExpiryMonth), "Enter a valid future expiry month and year.");

        if (string.IsNullOrWhiteSpace(model.CardholderName))
            ModelState.AddModelError(nameof(model.CardholderName), "Enter the test cardholder name.");

        if (string.IsNullOrWhiteSpace(model.Cvc) ||
            model.Cvc.Length != 3 ||
            !model.Cvc.All(char.IsDigit))
            ModelState.AddModelError(nameof(model.Cvc), "Enter any 3-digit test CVC.");

        if (!ModelState.IsValid)
            return View("Checkout", model);

        if (plan.Price <= 0 || plan.DurationDays <= 0)
        {
            TempData["Error"] = "This membership plan has invalid pricing or duration settings.";
            return RedirectToAction(nameof(Plans));
        }

        var now = DateTime.UtcNow;
        var latestMembershipEnd = await _context.Subscriptions
            .Where(s => s.UserId == user.Id &&
                        s.Status == "Active" &&
                        s.EndDate > now)
            .Select(s => (DateTime?)s.EndDate)
            .MaxAsync();

        var startDate = latestMembershipEnd.HasValue && latestMembershipEnd.Value > now
            ? latestMembershipEnd.Value
            : now;
        var endDate = startDate.AddDays(plan.DurationDays);

        var currentStep = "begin transaction";
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var subscription = new Subscription
            {
                UserId = user.Id,
                PlanId = plan.PlanId,
                StartDate = startDate,
                EndDate = endDate,
                Status = "Active"
            };

            currentStep = "save subscription";
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var payment = new Payment
            {
                SubscriptionId = subscription.SubscriptionId,
                Amount = plan.Price,
                PaymentDate = now,
                PaymentMethod = "Test Card",
                Status = "Completed",
                TransactionReference = $"TEST-CARD-{Guid.NewGuid():N}"
            };

            currentStep = "save payment";
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            currentStep = "commit transaction";
            await transaction.CommitAsync();

            try
            {
                await _notificationService.NotifySubscriptionPaymentSucceededAsync(subscription.SubscriptionId);
            }
            catch (Exception notificationException)
            {
                _logger.LogWarning(notificationException,
                    "Test-card membership purchase {PaymentReference} succeeded, but its notification failed.",
                    payment.TransactionReference);
            }

            TempData["Success"] = startDate > now
                ? $"Test card accepted. No money was charged. Your {plan.Name} membership is scheduled to start on {startDate.ToLocalTime():dd MMM yyyy}."
                : $"Test card accepted. No money was charged. Your {plan.Name} membership is active until {endDate.ToLocalTime():dd MMM yyyy}.";

            return RedirectToAction(nameof(Plans));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Test-card membership purchase failed during step {PurchaseStep}. UserId={UserId}, PlanId={PlanId}.",
                currentStep, user.Id, plan.PlanId);

            TempData["Error"] =
                $"The test purchase could not be saved during: {currentStep}. No successful purchase was confirmed. Check the application log.";
            return RedirectToAction(nameof(Plans));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected test-card membership purchase error during step {PurchaseStep}. UserId={UserId}, PlanId={PlanId}.",
                currentStep, user.Id, plan.PlanId);

            TempData["Error"] = "An unexpected error occurred while processing the test purchase. Check the application log.";
            return RedirectToAction(nameof(Plans));
        }
    }
}

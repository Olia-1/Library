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

    private static bool IsTrialPlan(MembershipPlan plan) =>
        plan.Price == 0 ||
        plan.Name.Contains("trial", StringComparison.OrdinalIgnoreCase) ||
        plan.Name.Contains("пробн", StringComparison.OrdinalIgnoreCase);

    private async Task<MembershipPlan> GetOrCreateTrialPlanAsync()
    {
        var trialPlan = await _context.MembershipPlans
            .FirstOrDefaultAsync(p => p.IsActive &&
                (p.Price == 0 ||
                 EF.Functions.Like(p.Name, "%trial%") ||
                 EF.Functions.Like(p.Name, "%пробн%")));

        if (trialPlan != null)
            return trialPlan;

        trialPlan = new MembershipPlan
        {
            Name = "Free Trial",
            Description = "One-month introductory membership. Available once per reader.",
            Price = 0,
            DurationDays = 30,
            IsActive = true
        };

        _context.MembershipPlans.Add(trialPlan);
        await _context.SaveChangesAsync();
        return trialPlan;
    }

    private async Task<bool> HasUsedTrialAsync(int userId)
    {
        return await _context.Subscriptions
            .Include(s => s.MembershipPlan)
            .AnyAsync(s => s.UserId == userId &&
                (s.MembershipPlan.Price == 0 ||
                 EF.Functions.Like(s.MembershipPlan.Name, "%trial%") ||
                 EF.Functions.Like(s.MembershipPlan.Name, "%пробн%")));
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

        var purchasedPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.Subscription.UserId == user.Id && p.Status == "Completed")
            .Include(p => p.Subscription)
                .ThenInclude(s => s.MembershipPlan)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

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

        var trialPlan = await GetOrCreateTrialPlanAsync();
        var trialAlreadyUsed = await HasUsedTrialAsync(user.Id);

        var model = new MembershipPlansViewModel
        {
            TrialPlanId = trialPlan.PlanId,
            CanStartTrial = !trialAlreadyUsed && activeSubscription is null,
            Plans = await _context.MembershipPlans
                .AsNoTracking()
                .Where(p => p.IsActive && p.Price > 0 &&
                    !EF.Functions.Like(p.Name, "%trial%") &&
                    !EF.Functions.Like(p.Name, "%пробн%"))
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AcceptTrialTerms(bool accepted)
    {
        if (!accepted)
        {
            TempData["Error"] = "Please accept the free-trial terms before continuing.";
            return RedirectToAction(nameof(Plans));
        }

        TempData["TrialTermsAccepted"] = "true";
        return RedirectToAction(nameof(TrialCheckout));
    }

    [HttpGet]
    public async Task<IActionResult> TrialCheckout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (!TempData.ContainsKey("TrialTermsAccepted"))
        {
            TempData["Error"] = "Please review and accept the free-trial terms first.";
            return RedirectToAction(nameof(Plans));
        }
        TempData.Remove("TrialTermsAccepted");

        if (await HasUsedTrialAsync(user.Id))
        {
            TempData["Error"] = "The free trial can only be activated once per account.";
            return RedirectToAction(nameof(Plans));
        }

        var activeSubscription = await _context.Subscriptions.AnyAsync(s =>
            s.UserId == user.Id && s.Status == "Active" &&
            s.StartDate <= DateTime.UtcNow && s.EndDate > DateTime.UtcNow);
        if (activeSubscription)
        {
            TempData["Error"] = "You already have an active membership.";
            return RedirectToAction(nameof(Plans));
        }

        var plan = await GetOrCreateTrialPlanAsync();
        return View(new TestCardCheckoutViewModel
        {
            PlanId = plan.PlanId,
            PlanName = "Free one-month trial",
            Price = 0,
            DurationDays = 30
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTrialCard(TestCardCheckoutViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var trialPlan = await GetOrCreateTrialPlanAsync();
        if (model.PlanId != trialPlan.PlanId || await HasUsedTrialAsync(user.Id))
        {
            TempData["Error"] = "The free trial is unavailable or has already been used.";
            return RedirectToAction(nameof(Plans));
        }

        var hasActiveSubscription = await _context.Subscriptions.AnyAsync(s =>
            s.UserId == user.Id && s.Status == "Active" &&
            s.StartDate <= DateTime.UtcNow && s.EndDate > DateTime.UtcNow);
        if (hasActiveSubscription)
        {
            TempData["Error"] = "You already have an active membership.";
            return RedirectToAction(nameof(Plans));
        }

        var rawCardNumber = model.CardNumber ?? string.Empty;
        var digits = new string(rawCardNumber.Where(char.IsDigit).ToArray());
        var cardCharactersValid = rawCardNumber.All(c => char.IsDigit(c) || char.IsWhiteSpace(c));
        var cardValid = cardCharactersValid && digits.Length >= 13 && digits.Length <= 19 && PassesLuhnCheck(digits);
        var expiryValid = int.TryParse(model.ExpiryMonth, out var month) &&
            int.TryParse(model.ExpiryYear, out var year) &&
            month >= 1 && month <= 12 &&
            (year > DateTime.UtcNow.Year || (year == DateTime.UtcNow.Year && month >= DateTime.UtcNow.Month));

        if (!cardValid)
            ModelState.AddModelError(nameof(model.CardNumber), "Enter 13–19 test digits with a valid checksum.");
        if (!expiryValid)
            ModelState.AddModelError(nameof(model.ExpiryMonth), "Enter a valid future expiry month and year.");
        if (string.IsNullOrWhiteSpace(model.CardholderName))
            ModelState.AddModelError(nameof(model.CardholderName), "Enter the test cardholder name.");
        if (string.IsNullOrWhiteSpace(model.Cvc) || model.Cvc.Length != 3 || !model.Cvc.All(char.IsDigit))
            ModelState.AddModelError(nameof(model.Cvc), "Enter any 3-digit test CVC.");

        if (!ModelState.IsValid)
        {
            model.PlanName = "Free one-month trial";
            model.Price = 0;
            model.DurationDays = 30;
            return View("TrialCheckout", model);
        }

        var now = DateTime.UtcNow;
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            if (await HasUsedTrialAsync(user.Id))
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "The free trial has already been activated for this account.";
                return RedirectToAction(nameof(Plans));
            }

            var subscription = new Subscription
            {
                UserId = user.Id,
                PlanId = trialPlan.PlanId,
                StartDate = now,
                EndDate = now.AddDays(30),
                Status = "Active"
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "Your free one-month trial has been activated. No payment was taken.";
            return RedirectToAction(nameof(Plans));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate free trial for UserId={UserId}.", user.Id);
            TempData["Error"] = "The free trial could not be activated. Please try again.";
            return RedirectToAction(nameof(Plans));
        }
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
            .FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive && p.Price > 0);

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
            .FirstOrDefaultAsync(p => p.PlanId == model.PlanId && p.IsActive && p.Price > 0);

        if (plan is null)
        {
            TempData["Error"] = "This membership plan is no longer available. Please refresh the page.";
            return RedirectToAction(nameof(Plans));
        }

        model.PlanName = plan.Name;
        model.Price = plan.Price;
        model.DurationDays = plan.DurationDays;

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

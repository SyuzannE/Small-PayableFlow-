using PayableFlow.Core;

namespace PayableFlow.Tests;

public class PaymentOptimizerTests
{
    [Fact]
    public void Beats_a_greedy_choice()
    {
        // Best benefit-per-dollar is A, but A blocks B and C, which together are worth more.
        var candidates = new List<PaymentCandidate>
        {
            new(1, "A", 60m, 10m),
            new(2, "B", 50m, 7m),
            new(3, "C", 50m, 7m)
        };

        var plan = PaymentOptimizer.Plan(candidates, 100m);

        Assert.Equal(new[] { 2, 3 }, plan.Selected.Select(s => s.InvoiceId).ToArray());
        Assert.Equal(14m, plan.TotalBenefit);
        Assert.Equal(100m, plan.TotalPaid);
    }

    [Fact]
    public void Never_exceeds_the_budget()
    {
        var candidates = new List<PaymentCandidate>
        {
            new(1, "A", 40.5m, 5m),
            new(2, "B", 30.2m, 4m),
            new(3, "C", 25m, 3m)
        };

        var plan = PaymentOptimizer.Plan(candidates, 70m);

        Assert.True(plan.TotalPaid <= 70m);
    }

    [Fact]
    public void Returns_empty_plan_when_nothing_fits()
    {
        var plan = PaymentOptimizer.Plan(new List<PaymentCandidate> { new(1, "A", 500m, 10m) }, 100m);
        Assert.Empty(plan.Selected);
    }

    [Fact]
    public void Rejects_negative_budget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentOptimizer.Plan(new List<PaymentCandidate>(), -1m));
    }

    [Fact]
    public void Captures_early_payment_discount_and_late_fee()
    {
        var vendor = new Vendor { EarlyDiscountPercent = 2m, EarlyDiscountDays = 10, PaymentTermsDays = 30 };
        var invoice = new Invoice { Id = 1, InvoiceNumber = "X1", Amount = 1000m, InvoiceDate = new DateOnly(2026, 9, 1), DueDate = new DateOnly(2026, 10, 1) };

        var withinDiscount = PaymentOptimizer.BuildCandidate(invoice, vendor, new DateOnly(2026, 9, 8));
        Assert.Equal(980m, withinDiscount.PayAmount);
        Assert.Equal(20m, withinDiscount.Benefit);

        var late = PaymentOptimizer.BuildCandidate(invoice, vendor, new DateOnly(2026, 10, 5));
        Assert.Equal(1000m, late.PayAmount);
        Assert.Equal(15m, late.Benefit); // 1.5% late fee avoided
    }
}

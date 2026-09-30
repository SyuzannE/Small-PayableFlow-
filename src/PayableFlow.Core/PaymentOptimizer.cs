namespace PayableFlow.Core;

/// <param name="PayAmount">Cash needed to settle the invoice (after any early-payment discount).</param>
/// <param name="Benefit">Money saved by paying now: discount captured plus late fee avoided.</param>
public sealed record PaymentCandidate(int InvoiceId, string InvoiceNumber, decimal PayAmount, decimal Benefit);

public sealed record PaymentPlan(
    IReadOnlyList<PaymentCandidate> Selected,
    decimal TotalPaid,
    decimal TotalBenefit);

/// <summary>
/// Chooses which invoices to pay in a payment run when cash is limited.
/// This is the 0/1 knapsack problem: weight = cash needed, value = benefit of paying now.
/// Solved with dynamic programming in O(n * budget) time, which is optimal
/// (a greedy "best ratio first" approach can be wrong).
/// </summary>
public static class PaymentOptimizer
{
    public const int MaxBudgetUnits = 5_000_000;

    public static PaymentCandidate BuildCandidate(Invoice invoice, Vendor vendor, DateOnly payDate, decimal lateFeeRate = 0.015m)
    {
        var discountDeadline = invoice.InvoiceDate.AddDays(vendor.EarlyDiscountDays);
        var discount = vendor.EarlyDiscountPercent > 0 && payDate <= discountDeadline
            ? Math.Round(invoice.Amount * vendor.EarlyDiscountPercent / 100m, 2)
            : 0m;
        var lateFee = payDate > invoice.DueDate
            ? Math.Round(invoice.Amount * lateFeeRate, 2)
            : 0m;
        return new PaymentCandidate(invoice.Id, invoice.InvoiceNumber, invoice.Amount - discount, discount + lateFee);
    }

    public static PaymentPlan Plan(IReadOnlyList<PaymentCandidate> candidates, decimal budget)
    {
        if (budget < 0) throw new ArgumentOutOfRangeException(nameof(budget), "Budget cannot be negative.");
        var capacity = (int)Math.Floor(budget);
        if (capacity > MaxBudgetUnits)
            throw new ArgumentOutOfRangeException(nameof(budget), $"Budget is limited to {MaxBudgetUnits} currency units.");

        var n = candidates.Count;
        // Weights are whole currency units (rounded up, so the plan never exceeds the budget).
        var weight = new int[n];
        // Value = benefit in cents. The pay amount is added as a tiny tie-breaker so that,
        // between equal-benefit plans, the one that settles more debt wins.
        var value = new long[n];
        for (var i = 0; i < n; i++)
        {
            weight[i] = (int)Math.Ceiling(candidates[i].PayAmount);
            value[i] = (long)Math.Round(candidates[i].Benefit * 100m) * 100_000L + weight[i];
        }

        var best = new long[capacity + 1];
        var taken = new bool[n, capacity + 1];
        for (var i = 0; i < n; i++)
        {
            for (var c = capacity; c >= weight[i]; c--)
            {
                var withItem = best[c - weight[i]] + value[i];
                if (withItem > best[c])
                {
                    best[c] = withItem;
                    taken[i, c] = true;
                }
            }
        }

        var selected = new List<PaymentCandidate>();
        var remaining = capacity;
        for (var i = n - 1; i >= 0; i--)
        {
            if (taken[i, remaining])
            {
                selected.Add(candidates[i]);
                remaining -= weight[i];
            }
        }
        selected.Reverse();
        return new PaymentPlan(selected, selected.Sum(s => s.PayAmount), selected.Sum(s => s.Benefit));
    }
}

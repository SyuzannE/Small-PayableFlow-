namespace PayableFlow.Core;

public sealed record OrderLine(string Sku, decimal Quantity, decimal UnitPrice);
public sealed record ReceiptLine(string Sku, decimal Quantity);
public sealed record InvoiceLine(string Sku, decimal Quantity, decimal UnitPrice);

public enum MatchIssueType
{
    NotOrdered,
    NotReceived,
    QuantityOverBilled,
    PriceMismatch
}

public sealed record MatchIssue(string Sku, MatchIssueType Type, string Message);

public sealed record MatchResult(bool IsMatched, IReadOnlyList<MatchIssue> Issues);

/// <summary>
/// Three-way match: a vendor bill may be paid only if it agrees with the purchase order
/// (what we ordered and at what price) and the receipt (what actually arrived).
/// </summary>
public static class ThreeWayMatcher
{
    public static MatchResult Match(
        IEnumerable<OrderLine> order,
        IEnumerable<ReceiptLine> receipts,
        IEnumerable<InvoiceLine> invoice,
        decimal priceTolerancePercent = 2m)
    {
        var cmp = StringComparer.OrdinalIgnoreCase;
        var orderBySku = order.GroupBy(o => o.Sku, cmp)
            .ToDictionary(g => g.Key, g => g.First(), cmp);
        var receivedBySku = receipts.GroupBy(r => r.Sku, cmp)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity), cmp);
        var invoiceLines = invoice.ToList();
        var invoicedBySku = invoiceLines.GroupBy(i => i.Sku, cmp)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity), cmp);

        var issues = new List<MatchIssue>();

        foreach (var (sku, invoicedQty) in invoicedBySku)
        {
            if (!orderBySku.ContainsKey(sku))
            {
                issues.Add(new MatchIssue(sku, MatchIssueType.NotOrdered, "Item is not on the purchase order."));
                continue;
            }

            var received = receivedBySku.GetValueOrDefault(sku, 0m);
            if (received == 0m)
                issues.Add(new MatchIssue(sku, MatchIssueType.NotReceived, "Nothing has been received for this item."));
            else if (invoicedQty > received)
                issues.Add(new MatchIssue(sku, MatchIssueType.QuantityOverBilled,
                    $"Billed {invoicedQty} but only {received} received."));
        }

        foreach (var line in invoiceLines)
        {
            if (!orderBySku.TryGetValue(line.Sku, out var ordered) || ordered.UnitPrice <= 0m) continue;
            var diffPercent = Math.Abs(line.UnitPrice - ordered.UnitPrice) / ordered.UnitPrice * 100m;
            if (diffPercent > priceTolerancePercent)
                issues.Add(new MatchIssue(line.Sku, MatchIssueType.PriceMismatch,
                    $"Invoice price {line.UnitPrice} differs from PO price {ordered.UnitPrice} by {diffPercent:0.##}%."));
        }

        return new MatchResult(issues.Count == 0, issues);
    }
}

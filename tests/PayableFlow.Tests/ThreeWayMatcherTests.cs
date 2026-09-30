using PayableFlow.Core;

namespace PayableFlow.Tests;

public class ThreeWayMatcherTests
{
    private static readonly OrderLine[] Order =
    {
        new("LAP-15", 10, 1000m),
        new("MOUSE", 20, 25m)
    };

    [Fact]
    public void Matches_when_everything_agrees()
    {
        var receipts = new[] { new ReceiptLine("LAP-15", 10), new ReceiptLine("MOUSE", 20) };
        var invoice = new[] { new InvoiceLine("LAP-15", 10, 1000m), new InvoiceLine("MOUSE", 20, 25m) };

        var result = ThreeWayMatcher.Match(Order, receipts, invoice);

        Assert.True(result.IsMatched);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Flags_over_billing_when_partly_received()
    {
        var receipts = new[] { new ReceiptLine("LAP-15", 6) };
        var invoice = new[] { new InvoiceLine("LAP-15", 10, 1000m) };

        var result = ThreeWayMatcher.Match(Order, receipts, invoice);

        Assert.False(result.IsMatched);
        Assert.Contains(result.Issues, i => i.Type == MatchIssueType.QuantityOverBilled);
    }

    [Fact]
    public void Flags_items_not_received_and_not_ordered()
    {
        var invoice = new[] { new InvoiceLine("MOUSE", 5, 25m), new InvoiceLine("DESK", 1, 300m) };

        var result = ThreeWayMatcher.Match(Order, Array.Empty<ReceiptLine>(), invoice);

        Assert.Contains(result.Issues, i => i.Sku == "MOUSE" && i.Type == MatchIssueType.NotReceived);
        Assert.Contains(result.Issues, i => i.Sku == "DESK" && i.Type == MatchIssueType.NotOrdered);
    }

    [Fact]
    public void Price_tolerance_is_respected()
    {
        var receipts = new[] { new ReceiptLine("LAP-15", 10) };

        var within = ThreeWayMatcher.Match(Order, receipts, new[] { new InvoiceLine("LAP-15", 10, 1015m) }); // 1.5%
        var beyond = ThreeWayMatcher.Match(Order, receipts, new[] { new InvoiceLine("LAP-15", 10, 1050m) }); // 5%

        Assert.True(within.IsMatched);
        Assert.Contains(beyond.Issues, i => i.Type == MatchIssueType.PriceMismatch);
    }
}

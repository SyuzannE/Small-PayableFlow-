using PayableFlow.Core;

namespace PayableFlow.Tests;

public class AgingCalculatorTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 30);

    [Theory]
    [InlineData(2026, 10, 15, AgingBucket.Current)]   // not yet due
    [InlineData(2026, 9, 30, AgingBucket.Current)]    // due today
    [InlineData(2026, 9, 29, AgingBucket.Days1To30)]  // 1 day late
    [InlineData(2026, 8, 31, AgingBucket.Days1To30)]  // 30 days late
    [InlineData(2026, 8, 30, AgingBucket.Days31To60)] // 31 days late
    [InlineData(2026, 7, 31, AgingBucket.Days61To90)] // 61 days late
    [InlineData(2026, 6, 1, AgingBucket.Over90)]
    public void Puts_invoice_in_the_right_bucket(int y, int m, int d, AgingBucket expected)
    {
        Assert.Equal(expected, AgingCalculator.GetBucket(new DateOnly(y, m, d), AsOf));
    }

    [Fact]
    public void Summarize_ignores_paid_and_void_invoices()
    {
        var invoices = new[]
        {
            new Invoice { Amount = 100m, DueDate = new DateOnly(2026, 9, 20) },
            new Invoice { Amount = 50m, DueDate = new DateOnly(2026, 9, 25) },
            new Invoice { Amount = 999m, DueDate = new DateOnly(2026, 9, 20), Status = InvoiceStatus.Paid },
            new Invoice { Amount = 999m, DueDate = new DateOnly(2026, 9, 20), Status = InvoiceStatus.Void }
        };

        var summary = AgingCalculator.Summarize(invoices, AsOf);

        Assert.Equal(150m, summary[AgingBucket.Days1To30]);
        Assert.Equal(0m, summary[AgingBucket.Current]);
    }
}

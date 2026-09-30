using PayableFlow.Core;

namespace PayableFlow.Tests;

public class DuplicateDetectorTests
{
    [Theory]
    [InlineData("inv-0042", "INV0042")]
    [InlineData("  00123 ", "123")]
    [InlineData("A/B 7", "AB7")]
    public void Normalize_cleans_invoice_numbers(string input, string expected)
    {
        Assert.Equal(expected, DuplicateDetector.Normalize(input));
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("", "abc", 3)]
    [InlineData("same", "same", 0)]
    public void Levenshtein_counts_edits(string a, string b, int expected)
    {
        Assert.Equal(expected, DuplicateDetector.Levenshtein(a, b));
    }

    private static Invoice Make(int id, string number, decimal amount, int day, int vendor = 1) =>
        new() { Id = id, VendorId = vendor, InvoiceNumber = number, Amount = amount, InvoiceDate = new DateOnly(2026, 9, day) };

    [Fact]
    public void Finds_same_vendor_same_amount_similar_number()
    {
        var existing = new[] { Make(1, "INV-1001", 250m, 5) };
        var candidate = Make(0, "inv1001", 250m, 9);

        Assert.Single(DuplicateDetector.FindPossibleDuplicates(candidate, existing));
    }

    [Fact]
    public void Ignores_other_vendor_other_amount_or_void()
    {
        var voided = Make(3, "INV-1001", 250m, 5);
        voided.Status = InvoiceStatus.Void;
        var existing = new[]
        {
            Make(1, "INV-1001", 250m, 5, vendor: 2),
            Make(2, "INV-1001", 251m, 5),
            voided
        };

        Assert.Empty(DuplicateDetector.FindPossibleDuplicates(Make(0, "INV-1001", 250m, 9), existing));
    }
}

namespace PayableFlow.Core;

/// <summary>Flags vendor invoices that look like the same bill entered twice.</summary>
public static class DuplicateDetector
{
    /// <summary>Uppercase, letters and digits only, leading zeros removed: "inv-0042" becomes "INV0042".</summary>
    public static string Normalize(string invoiceNumber)
    {
        var chars = invoiceNumber.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray();
        return new string(chars).TrimStart('0');
    }

    /// <summary>Minimum number of single-character edits between two strings (dynamic programming).</summary>
    public static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    /// <summary>Same vendor, same amount, invoice dates within 30 days, and a matching or near-matching number.</summary>
    public static IReadOnlyList<Invoice> FindPossibleDuplicates(Invoice candidate, IEnumerable<Invoice> existing)
    {
        var number = Normalize(candidate.InvoiceNumber);
        return existing
            .Where(e => e.Id != candidate.Id
                        && e.VendorId == candidate.VendorId
                        && e.Status != InvoiceStatus.Void
                        && e.Amount == candidate.Amount
                        && Math.Abs(e.InvoiceDate.DayNumber - candidate.InvoiceDate.DayNumber) <= 30
                        && Levenshtein(Normalize(e.InvoiceNumber), number) <= 1)
            .ToList();
    }
}

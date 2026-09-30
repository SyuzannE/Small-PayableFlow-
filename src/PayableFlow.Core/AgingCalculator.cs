namespace PayableFlow.Core;

public enum AgingBucket
{
    Current,
    Days1To30,
    Days31To60,
    Days61To90,
    Over90
}

/// <summary>Accounts payable aging: how long open invoices are past due.</summary>
public static class AgingCalculator
{
    public static AgingBucket GetBucket(DateOnly dueDate, DateOnly asOf)
    {
        var daysPastDue = asOf.DayNumber - dueDate.DayNumber;
        if (daysPastDue <= 0) return AgingBucket.Current;
        if (daysPastDue <= 30) return AgingBucket.Days1To30;
        if (daysPastDue <= 60) return AgingBucket.Days31To60;
        if (daysPastDue <= 90) return AgingBucket.Days61To90;
        return AgingBucket.Over90;
    }

    public static Dictionary<AgingBucket, decimal> Summarize(IEnumerable<Invoice> invoices, DateOnly asOf)
    {
        var result = Enum.GetValues<AgingBucket>().ToDictionary(b => b, _ => 0m);
        foreach (var invoice in invoices)
        {
            if (invoice.Status != InvoiceStatus.Open) continue;
            result[GetBucket(invoice.DueDate, asOf)] += invoice.Amount;
        }
        return result;
    }
}

namespace PayableFlow.Core;

public enum InvoiceStatus
{
    Open,
    Paid,
    Void
}

public sealed class Vendor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Net payment terms, e.g. 30 means "Net 30".</summary>
    public int PaymentTermsDays { get; set; } = 30;
    /// <summary>Early-payment discount in percent, e.g. 2 for "2/10 Net 30".</summary>
    public decimal EarlyDiscountPercent { get; set; }
    /// <summary>Days after the invoice date in which the discount applies.</summary>
    public int EarlyDiscountDays { get; set; }
}

public sealed class Invoice
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;
    public DateOnly? PaidOn { get; set; }
}

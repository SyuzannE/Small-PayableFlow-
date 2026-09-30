using PayableFlow.Core;

namespace PayableFlow.Api;

public sealed record CreateVendorRequest(string Name, int PaymentTermsDays = 30, decimal EarlyDiscountPercent = 0, int EarlyDiscountDays = 0);

public sealed record CreateInvoiceRequest(int VendorId, string InvoiceNumber, decimal Amount, DateOnly? InvoiceDate, DateOnly? DueDate);

public sealed record PaymentPlanRequest(decimal Budget, DateOnly? PayDate, int? HorizonDays);

public sealed record MatchingRequest(
    List<OrderLine>? Order,
    List<ReceiptLine>? Receipts,
    List<InvoiceLine>? Invoice,
    decimal? PriceTolerancePercent);

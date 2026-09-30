using Microsoft.EntityFrameworkCore;
using PayableFlow.Api;
using PayableFlow.Core;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=payableflow.db";
builder.Services.AddDbContext<PayableDbContext>(o => o.UseSqlite(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<PayableDbContext>().Database.EnsureCreated();
}

static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ---- Vendors ----
app.MapGet("/vendors", async (PayableDbContext db) => Results.Ok(await db.Vendors.OrderBy(v => v.Name).ToListAsync()));

app.MapPost("/vendors", async (CreateVendorRequest r, PayableDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(r.Name))
        return Results.BadRequest(new { error = "Name is required." });
    if (r.PaymentTermsDays < 0 || r.EarlyDiscountPercent < 0 || r.EarlyDiscountPercent > 100 || r.EarlyDiscountDays < 0)
        return Results.BadRequest(new { error = "Payment terms or discount values are out of range." });
    if (await db.Vendors.AnyAsync(v => v.Name == r.Name.Trim()))
        return Results.Conflict(new { error = "A vendor with this name already exists." });

    var vendor = new Vendor
    {
        Name = r.Name.Trim(),
        PaymentTermsDays = r.PaymentTermsDays,
        EarlyDiscountPercent = r.EarlyDiscountPercent,
        EarlyDiscountDays = r.EarlyDiscountDays
    };
    db.Vendors.Add(vendor);
    await db.SaveChangesAsync();
    return Results.Created($"/vendors/{vendor.Id}", vendor);
});

// ---- Invoices (bills) ----
app.MapGet("/invoices", async (InvoiceStatus? status, PayableDbContext db) =>
{
    var query = db.Invoices.AsQueryable();
    if (status is not null) query = query.Where(i => i.Status == status.Value);
    var list = await query.ToListAsync();
    return Results.Ok(list.OrderBy(i => i.DueDate).ThenBy(i => i.Id));
});

app.MapPost("/invoices", async (CreateInvoiceRequest r, bool? force, PayableDbContext db) =>
{
    if (r.Amount <= 0)
        return Results.BadRequest(new { error = "Amount must be greater than zero." });
    if (string.IsNullOrWhiteSpace(r.InvoiceNumber))
        return Results.BadRequest(new { error = "Invoice number is required." });

    var vendor = await db.Vendors.FindAsync(r.VendorId);
    if (vendor is null)
        return Results.NotFound(new { error = "Vendor not found." });

    var invoiceDate = r.InvoiceDate ?? Today();
    var invoice = new Invoice
    {
        VendorId = vendor.Id,
        InvoiceNumber = r.InvoiceNumber.Trim(),
        Amount = r.Amount,
        InvoiceDate = invoiceDate,
        DueDate = r.DueDate ?? invoiceDate.AddDays(vendor.PaymentTermsDays)
    };

    var existing = await db.Invoices.Where(i => i.VendorId == vendor.Id).ToListAsync();
    if (existing.Any(i => i.InvoiceNumber == invoice.InvoiceNumber))
        return Results.Conflict(new { error = "This vendor already has an invoice with this number." });

    var duplicates = DuplicateDetector.FindPossibleDuplicates(invoice, existing);
    if (duplicates.Count > 0 && force != true)
    {
        return Results.Conflict(new
        {
            error = "Possible duplicate invoice. Resend with ?force=true if it is genuine.",
            possibleDuplicates = duplicates.Select(d => new { d.Id, d.InvoiceNumber, d.InvoiceDate, d.Amount })
        });
    }

    db.Invoices.Add(invoice);
    await db.SaveChangesAsync();
    return Results.Created($"/invoices/{invoice.Id}", invoice);
});

app.MapPost("/invoices/{id:int}/pay", async (int id, PayableDbContext db) =>
{
    var invoice = await db.Invoices.FindAsync(id);
    if (invoice is null) return Results.NotFound();
    if (invoice.Status != InvoiceStatus.Open)
        return Results.Conflict(new { error = $"Invoice is already {invoice.Status}." });

    invoice.Status = InvoiceStatus.Paid;
    invoice.PaidOn = Today();
    await db.SaveChangesAsync();
    return Results.Ok(invoice);
});

app.MapPost("/invoices/{id:int}/void", async (int id, PayableDbContext db) =>
{
    var invoice = await db.Invoices.FindAsync(id);
    if (invoice is null) return Results.NotFound();
    if (invoice.Status != InvoiceStatus.Open)
        return Results.Conflict(new { error = $"Invoice is already {invoice.Status}." });

    invoice.Status = InvoiceStatus.Void;
    await db.SaveChangesAsync();
    return Results.Ok(invoice);
});

// ---- Reports and algorithms ----
app.MapGet("/reports/aging", async (DateOnly? asOf, PayableDbContext db) =>
{
    var date = asOf ?? Today();
    var open = await db.Invoices.Where(i => i.Status == InvoiceStatus.Open).ToListAsync();
    var buckets = AgingCalculator.Summarize(open, date);
    return Results.Ok(new { asOf = date, buckets, total = buckets.Values.Sum() });
});

app.MapPost("/payment-runs/plan", async (PaymentPlanRequest r, PayableDbContext db) =>
{
    if (r.Budget <= 0)
        return Results.BadRequest(new { error = "Budget must be greater than zero." });

    var payDate = r.PayDate ?? Today();
    var horizon = r.HorizonDays ?? 7;
    var open = await db.Invoices.Include(i => i.Vendor).Where(i => i.Status == InvoiceStatus.Open).ToListAsync();

    // Only consider invoices that are worth paying now: they save money or are due soon.
    var candidates = new List<PaymentCandidate>();
    foreach (var invoice in open)
    {
        if (invoice.Vendor is null) continue;
        var candidate = PaymentOptimizer.BuildCandidate(invoice, invoice.Vendor, payDate);
        if (candidate.Benefit > 0 || invoice.DueDate <= payDate.AddDays(horizon))
            candidates.Add(candidate);
    }

    try
    {
        return Results.Ok(PaymentOptimizer.Plan(candidates, r.Budget));
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/matching", (MatchingRequest r) =>
{
    var result = ThreeWayMatcher.Match(
        r.Order ?? [],
        r.Receipts ?? [],
        r.Invoice ?? [],
        r.PriceTolerancePercent ?? 2m);
    return Results.Ok(result);
});

app.Run();

public partial class Program;

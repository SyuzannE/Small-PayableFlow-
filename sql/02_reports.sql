-- Example reporting queries for accounts payable (T-SQL).

-- 1. Aging report: open amount per vendor and aging bucket
DECLARE @AsOf DATE = CAST(GETDATE() AS DATE);

SELECT v.Name,
       SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOf) <= 0                THEN i.Amount ELSE 0 END) AS [Current],
       SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOf) BETWEEN 1  AND 30   THEN i.Amount ELSE 0 END) AS [1-30],
       SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOf) BETWEEN 31 AND 60   THEN i.Amount ELSE 0 END) AS [31-60],
       SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOf) BETWEEN 61 AND 90   THEN i.Amount ELSE 0 END) AS [61-90],
       SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOf) > 90                THEN i.Amount ELSE 0 END) AS [90+],
       SUM(i.Amount) AS Total
FROM Invoices i
JOIN Vendors v ON v.Id = i.VendorId
WHERE i.Status = 0
GROUP BY v.Name
ORDER BY Total DESC;

-- 2. Possible duplicate invoices: same vendor, same amount, more than one row
SELECT VendorId, Amount, COUNT(*) AS Copies, STRING_AGG(InvoiceNumber, ', ') AS InvoiceNumbers
FROM Invoices
WHERE Status <> 2
GROUP BY VendorId, Amount
HAVING COUNT(*) > 1;

-- 3. Running total of payments per vendor (window function)
SELECT v.Name, i.InvoiceNumber, i.PaidOn, i.Amount,
       SUM(i.Amount) OVER (PARTITION BY i.VendorId ORDER BY i.PaidOn, i.Id) AS RunningPaid
FROM Invoices i
JOIN Vendors v ON v.Id = i.VendorId
WHERE i.Status = 1
ORDER BY v.Name, i.PaidOn;

-- 4. Vendors ranked by spend, with share of total
SELECT v.Name,
       SUM(i.Amount) AS Spend,
       CAST(100.0 * SUM(i.Amount) / SUM(SUM(i.Amount)) OVER () AS DECIMAL(5,2)) AS SharePercent
FROM Invoices i
JOIN Vendors v ON v.Id = i.VendorId
WHERE i.Status <> 2
GROUP BY v.Name
ORDER BY Spend DESC;

-- 5. Invoices with an early-payment discount still available today
SELECT i.InvoiceNumber, v.Name, i.Amount,
       ROUND(i.Amount * v.EarlyDiscountPercent / 100, 2) AS DiscountIfPaidToday,
       DATEADD(DAY, v.EarlyDiscountDays, i.InvoiceDate) AS DiscountDeadline
FROM Invoices i
JOIN Vendors v ON v.Id = i.VendorId
WHERE i.Status = 0
  AND v.EarlyDiscountPercent > 0
  AND CAST(GETDATE() AS DATE) <= DATEADD(DAY, v.EarlyDiscountDays, i.InvoiceDate);

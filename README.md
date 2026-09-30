# PayableFlow

[![CI](https://github.com/SyuzannE/Small-PayableFlow-/actions/workflows/ci.yml/badge.svg)](https://github.com/SyuzannE/Small-PayableFlow-/actions/workflows/ci.yml)

A small **Accounts Payable (AP)** system in C# and ASP.NET Core, modelled on the vendor-bill workflow of an ERP such as Acumatica.
It focuses on the logic side of AP: catching bad bills, deciding what to pay, and reporting what is owed.

## Features

| Feature | What it does | Technique |
|---|---|---|
| Vendors and bills | Create vendors (with payment terms and early-payment discounts) and enter bills | ASP.NET Core minimal API, EF Core, SQL |
| Duplicate detection | Rejects a bill that looks like one already entered (`INV-1001` vs `inv1001`) | String normalisation + Levenshtein distance (DP) |
| Three-way match | Checks a bill against the purchase order and goods receipt (quantity and price tolerance) | Grouping and aggregation by SKU |
| Payment run planner | Picks which bills to pay with a limited cash budget to save the most money | 0/1 knapsack, dynamic programming, O(n x budget) |
| Aging report | Open amounts by days past due (Current, 1-30, 31-60, 61-90, 90+) | Date arithmetic and bucketing |
| SQL reports | Aging per vendor, duplicates, running totals, spend share (T-SQL) | `GROUP BY`, `CASE`, window functions |

## Why the payment planner uses dynamic programming

Suppose you have 100 to spend and three open bills: A costs 60 (saves 10), B costs 50 (saves 7), C costs 50 (saves 7).
A "best savings per dollar first" approach picks A and saves 10. The optimal plan pays B and C and saves 14.
The DP in `PaymentOptimizer` finds the optimal set every time. See `PaymentOptimizerTests.Beats_a_greedy_choice`.

## Project layout

```
src/PayableFlow.Core   Business rules and algorithms (no framework dependencies)
src/PayableFlow.Api    ASP.NET Core minimal API + EF Core (SQLite)
tests/PayableFlow.Tests  xUnit tests for the algorithms
sql/                   T-SQL schema and reporting queries (SQL Server)
```

Business logic lives in `Core`, so it can be tested without a database or web server.

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
dotnet run --project src/PayableFlow.Api
```

The API listens on http://localhost:5000 and creates `payableflow.db` on first start.
Open `api.http` for ready-made example requests.

### Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET/POST | `/vendors` | List or create vendors |
| GET/POST | `/invoices` | List (`?status=Open`) or enter bills |
| POST | `/invoices/{id}/pay` | Mark a bill as paid |
| POST | `/invoices/{id}/void` | Void an open bill |
| GET | `/reports/aging?asOf=2026-09-30` | Aging report |
| POST | `/payment-runs/plan` | Optimal payment run for a cash budget |
| POST | `/matching` | Three-way match check |
| GET | `/health` | Health check |

### Docker

```
docker build -t payableflow .
docker run -p 8080:8080 payableflow
```

## Design notes

- Amounts use `decimal`, never `double`, to avoid rounding errors in money.
- The planner works in whole currency units (rounded up) so a plan never exceeds the budget.
- The database is created with `EnsureCreated`. Switch to EF Core migrations once the schema changes.
- `sql/` targets SQL Server because that is the database Acumatica uses. The API runs on SQLite so it works anywhere.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Licensed under the [MIT License](LICENSE).

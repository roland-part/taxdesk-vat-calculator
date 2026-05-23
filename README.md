# Hungarian VAT Declaration Generator

A web application that processes invoice/transaction CSV files and generates a formally correct Hungarian VAT Declaration (ÁFA bevallás) report summary, including PDF export.

## Tech stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 9, C# |
| CSV parsing | CsvHelper 33 |
| PDF generation | QuestPDF 2026 (Community licence) |
| Logging | Serilog — console + daily rolling file (`logs/app-YYYYMMDD.log`) |
| Frontend | React 19, TypeScript, Vite |

## Project structure

```
taxdesk-vat-calculator/
├── backend/          # ASP.NET Core 9 Web API
├── frontend/         # React + TypeScript (Vite)
└── sample-invoices.csv
```

No monorepo tooling (Nx, Turborepo, pnpm workspaces) — the two projects are independent and only share this root folder.

## Getting started

### Prerequisites

- .NET 9 SDK
- Node.js 18+

### Backend

```bash
cd backend/VatCalculator.Api
dotnet run
```

API listens on `http://localhost:5150`.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

App runs at `http://localhost:5173`.

## Input file format

Upload a CSV with the following columns:

```
InvoiceId,PerformanceDate,Direction,TransactionType,PartnerName,TaxNumber,NetAmount,VatRate,VatAmount,GrossAmount
INV-001,2024-01-15,Sale,Domestic,Acme Kft,12345678-1-41,100000,27,27000,127000
INV-002,2024-01-16,Purchase,IntraCommunityAcquisition,Österreich GmbH,,120000,27,32400,152400
INV-003,2024-01-17,Sale,Domestic,Acme Kft,12345678-1-41,-100000,27,-27000,-127000
```

The `TransactionType` column is optional — existing CSV files without it are accepted and all rows default to `Domestic`.

**Currency:** all amounts must be in **HUF**. Foreign-currency invoices (e.g. EUR for intra-Community transactions) must be converted to HUF at the MNB rate for the teljesítési időpont before being entered in the CSV.

**Encoding:** the file must be saved as **UTF-8**. The included `sample-invoices.csv` is UTF-8 with BOM so Excel opens it correctly without garbling accented characters. If you prepare the file in Excel, use *File → Save As → CSV UTF-8 (with BOM)*.

Negative amounts are supported for storno (reversing) and correction invoices.

| Column | Required | Notes |
|---|---|---|
| `InvoiceId` | Yes | Unique invoice identifier |
| `PerformanceDate` | Yes | Teljesítési időpont — the date that determines which period the invoice belongs to |
| `Direction` | Yes | `Sale` or `Purchase` |
| `TransactionType` | No | `Domestic` (default when column is absent or blank), `IntraCommunitySale`, `IntraCommunityAcquisition`, `Import`, `ReverseCharge` |
| `PartnerName` | Yes | Counterparty name, max 200 characters |
| `TaxNumber` | No | Counterparty adószám — if provided must match `XXXXXXXX-Y-ZZ` |
| `NetAmount` | Yes | Net amount in HUF, must not be negative |
| `VatRate` | Yes | `27`, `18`, `5`, `0`, or `AAM` (exempt) |
| `VatAmount` | Yes | Cross-validated against `NetAmount × VatRate`; mismatches > 1 HUF produce a warning |
| `GrossAmount` | Yes | Cross-validated against `NetAmount + VatAmount`; mismatches > 1 HUF produce a warning |

A `sample-invoices.csv` file covering all VAT rates and both directions is included in the repository root.

## AI conversation log

The full, unedited conversation with Claude Code (the AI tool used to build this) is available in [`ai_log.md`](ai_log.md).

---

## Known enhancements / out-of-scope items

The items below were identified during development but are deliberately deferred. They are documented here for future reference.

### PDF generation — stateless round-trip vs. Azure Blob Storage

**Current behaviour:** After `/api/vat/upload` returns the `VatReportDto` JSON, the frontend holds it in memory. When the user clicks *Download PDF*, the full JSON is POSTed back to `/api/vat/report/pdf`, which regenerates the PDF on the fly and streams it back.

**Why:** Avoids server-side state that would be lost on a pod restart or scale-out event.

**Production-grade alternative:**
1. After `/upload`, persist the `VatReportDto` to **Azure Blob Storage** and return a short-lived SAS URL alongside the JSON response.
2. The `/report/pdf` endpoint accepts only the blob reference (not the full payload), fetches the report from storage, generates the PDF, and can optionally write the result back to blob storage for caching.
3. This keeps request payloads small, enables async/background PDF generation, and provides an audit trail of generated declarations.

### Foreign currency invoices — conversion to HUF

**Current behaviour:** All amounts in the CSV are assumed to be in HUF. The declaration is produced in HUF, which is correct — Hungarian VAT law (Act CXXVII of 2007) requires the declaration to be filed entirely in HUF regardless of the invoice currency.

Foreign currency invoices (e.g. EUR for intra-Community acquisitions) are valid and common, but the HUF conversion must happen *before* the CSV is prepared. The applicable rate is the official MNB (Magyar Nemzeti Bank) exchange rate on the teljesítési időpont, or the ECB rate if agreed contractually. This conversion is typically performed in the taxpayer's accounting software.

**Production-grade alternative:** Add optional `Currency` and `ExchangeRate` columns to the CSV. The app would convert non-HUF amounts to HUF at the stated rate before aggregating. The MNB publishes daily exchange rates via a public API (`https://www.mnb.hu/arfolyamok`) that could be called automatically when `Currency` is provided but `ExchangeRate` is omitted.

---

### Tax number validation — duplicated regex

**Current behaviour:** The Hungarian adószám format (`XXXXXXXX-Y-ZZ`) is validated independently in two places:

- **Backend** — `CsvParserService.cs`: validates the `TaxNumber` column of each CSV row.
- **Frontend** — `UploadForm.tsx`: validates the taxpayer tax number field before submission.

Both use the same regex but there is no shared source of truth. If the rules change (e.g. NAV introduces a new territorial code), both sides must be updated manually and can drift.

**Production-grade alternative:**
- Expose validation rules via a dedicated API endpoint (e.g. `GET /api/vat/rules`) that the frontend fetches on load, or
- Extract the rule into a shared library (NuGet package for the backend, npm package for the frontend) so a single change propagates automatically to both consumers.

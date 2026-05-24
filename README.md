# Hungarian VAT Declaration Generator

> **Demo application** — This is a coding challenge submission, not a production service. It processes invoice CSV files and generates a Hungarian VAT Declaration (ÁFA bevallás) report summary, including PDF export. Feel free to try it with the test files below.

## Live application

**[https://icy-grass-014755103.7.azurestaticapps.net](https://icy-grass-014755103.7.azurestaticapps.net)**

> The backend runs on Azure App Service F1 (free tier) — the first request after a period of inactivity may take 10–15 seconds while the process warms up.

## Test files

A set of ready-made CSV files is included in the repository to exercise different scenarios:

| File | What it tests |
|---|---|
| [`sample-invoices.csv`](sample-invoices.csv) | Happy-path: all VAT rates (27%, 18%, 5%, 0%, AAM), both directions, storno and ICA rows |
| [`test-invoices/warn-amount-mismatch.csv`](test-invoices/warn-amount-mismatch.csv) | Rows where VatAmount or GrossAmount don't match the calculated values (produces warnings) |
| [`test-invoices/warn-duplicate-ids.csv`](test-invoices/warn-duplicate-ids.csv) | Duplicate InvoiceId values (produces warnings) |
| [`test-invoices/warn-out-of-period.csv`](test-invoices/warn-out-of-period.csv) | Rows whose PerformanceDate falls outside the declared period (produces warnings) |
| [`test-invoices/invalid-missing-columns.csv`](test-invoices/invalid-missing-columns.csv) | Required columns absent (hard error, no report produced) |
| [`test-invoices/invalid-row-errors.csv`](test-invoices/invalid-row-errors.csv) | Invalid field values on individual rows (hard errors) |
| [`test-invoices/invalid-type-direction-mismatch.csv`](test-invoices/invalid-type-direction-mismatch.csv) | TransactionType/Direction combinations that are logically inconsistent (hard errors) |
| [`test-invoices/invalid-mixed-errors-and-warnings.csv`](test-invoices/invalid-mixed-errors-and-warnings.csv) | Mix of hard errors and warnings in the same file |

## AI conversation log

The full, unedited conversation with Claude Code (the AI tool used to build this) is available in [`ai_log.md`](ai_log.md).

> **Note for reviewers:** `ai_log.md` is exported from the Claude Code session transcript and committed separately. If the file is absent, the raw session log is stored locally at `.claude/projects/…/<session-id>.jsonl` and can be exported via Claude Code's `/export` command.

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
├── test-invoices/    # CSV fixtures for testing error and warning scenarios
└── sample-invoices.csv
```

No monorepo tooling (Nx, Turborepo, pnpm workspaces) — the two projects are independent and only share this root folder.

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
| `NetAmount` | Yes | Net amount in HUF. Negative values are accepted for storno (reversal) and correction invoices |
| `VatRate` | Yes | `27`, `18`, `5`, `0`, or `AAM` (exempt) |
| `VatAmount` | Yes | Cross-validated against `NetAmount × VatRate`; mismatches > 1 HUF produce a warning |
| `GrossAmount` | Yes | Cross-validated against `NetAmount + VatAmount`; mismatches > 1 HUF produce a warning |

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

---

### Foreign currency invoices — conversion to HUF

**Current behaviour:** All amounts in the CSV are assumed to be in HUF. The declaration is produced in HUF, which is correct — Hungarian VAT law (Act CXXVII of 2007) requires the declaration to be filed entirely in HUF regardless of the invoice currency.

Foreign currency invoices (e.g. EUR for intra-Community acquisitions) are valid and common, but the HUF conversion must happen *before* the CSV is prepared. The applicable rate is the official MNB (Magyar Nemzeti Bank) exchange rate on the teljesítési időpont, or the ECB rate if agreed contractually. This conversion is typically performed in the taxpayer's accounting software.

**Production-grade alternative:** Add optional `Currency` and `ExchangeRate` columns to the CSV. The app would convert non-HUF amounts to HUF at the stated rate before aggregating. The MNB publishes daily exchange rates via a public API (`https://www.mnb.hu/arfolyamok`) that could be called automatically when `Currency` is provided but `ExchangeRate` is omitted.

---

### Backend API access restriction — AzureCloud service tag

**Current behaviour:** The App Service (`app-taxdesk-api-prod`) is protected by an Azure access restriction that allows only the `AzureCloud` service tag and denies all public internet traffic. All API calls flow through the Azure Static Web Apps linked backend proxy (`stapp-taxdesk-prod`), so the App Service URL returns `403 Ip Forbidden` when called directly from outside Azure.

**Limitation:** The `AzureCloud` service tag covers the entire Azure datacenter IP space, not just this application's SWA instance. Any other service running inside Azure could technically reach the App Service URL directly.

**Production-grade alternative:** Layer a shared secret on top of the network restriction:
1. Generate a random secret and store it as an App Service environment variable.
2. Configure the SWA linked backend to inject the secret as a custom request header on every proxied call.
3. Add a middleware to the ASP.NET Core pipeline that rejects any request missing the correct header with `403`.

This scopes access to exactly one SWA instance without requiring a VNet or Private Link, and the secret is never exposed to the browser.

---

### Tax number validation — duplicated regex

**Current behaviour:** The Hungarian adószám format (`XXXXXXXX-Y-ZZ`) is validated independently in two places:

- **Backend** — `CsvParserService.cs`: validates the `TaxNumber` column of each CSV row.
- **Frontend** — `UploadForm.tsx`: validates the taxpayer tax number field before submission.

Both use the same regex but there is no shared source of truth. If the rules change (e.g. NAV introduces a new territorial code), both sides must be updated manually and can drift.

**Production-grade alternative:**
- Expose validation rules via a dedicated API endpoint (e.g. `GET /api/vat/rules`) that the frontend fetches on load, or
- Extract the rule into a shared library (NuGet package for the backend, npm package for the frontend) so a single change propagates automatically to both consumers.

---

### Self-assessed VAT — IntraCommunityAcquisition and Import

**Current behaviour:** `IntraCommunityAcquisition` and `Import` transactions are grouped under the Purchases section only. The `VatPayable` figure is calculated as `Sales.GrandTotalVat − Purchases.GrandTotalVat`.

**Known gap:** Under Hungarian VAT law (Act CXXVII of 2007) and Form 65, intra-Community acquisitions and imports trigger **self-assessment**: the buyer must declare the VAT amount as *both* output VAT (fizetendő ÁFA, sales side) and deductible input VAT (levonható ÁFA, purchases side) in the same period. The two entries cancel out in `VatPayable`, but both must appear in the formal declaration. The current implementation only shows these transactions on the purchases side, which understates the declared output VAT total for files containing these transaction types.

**Production-grade fix:** In `VatCalculationService`, detect `IntraCommunityAcquisition` and `Import` purchase records and add a synthetic mirror entry to the Sales section (same VAT amount, labelled accordingly), so both sides of the declaration are complete even though the net tax position is unchanged.

---

### Application Insights — resource not provisioned

**Current behaviour:** `Program.cs` conditionally activates the Serilog Application Insights sink when the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable is present. The `Serilog.Sinks.ApplicationInsights` package is included. However, no Application Insights resource has been created in Azure, so the sink is dormant in production.

**Next step:** Create an Application Insights resource in `rg-taxdesk-prod`, copy its connection string into the App Service environment variables, and structured log data will flow automatically with no further code changes.

---

### No automated tests

**Current behaviour:** No unit test project exists for the backend (`VatCalculationService`, `CsvParserService`, cross-validation logic) and no component tests for the frontend.

**Production-grade alternative:** Add an xUnit project (`VatCalculator.Api.Tests`) covering at minimum the calculation engine with a representative set of invoice fixtures, and Vitest tests for the `formatLineLabel` / `formatPeriod` helpers. The seven `test-invoices/` CSV files already in the repository provide a ready-made set of fixtures for integration-level testing of the CSV parser.

---

## Architecture Decision Records

A summary of the key architectural choices made during development, for reviewer context.

### ADR-001 — CSV as the input format

**Decision:** Accept a plain UTF-8 CSV file rather than an Excel workbook (.xlsx) or a NAV-specific XML format.

**Rationale:** No official NAV import format exists for ÁFA bevallás source data. XLSX parsing adds significant library complexity. CSV is universally exportable from any accounting system, maps directly to the data model, and allows the backend to enforce structure strictly via column headers. UTF-8 with BOM is required so Excel can open the sample file without re-encoding.

**Trade-off:** Users working in Excel must use *File → Save As → CSV UTF-8 (with BOM)*; this is documented in the README and enforced with a clear error message on invalid encoding.

---

### ADR-002 — Stateless PDF generation (round-trip)

**Decision:** The frontend holds the `VatReportDto` JSON in memory after `/upload`. Clicking *Download PDF* POSTs the full JSON back to `/report/pdf`, which regenerates the PDF on the fly.

**Rationale:** Eliminates server-side session state that would be lost on pod restart or horizontal scale-out. No blob storage dependency required for the MVP.

**Trade-off:** The PDF request payload is proportional to the report size (typically a few KB). The production-grade alternative — persisting to Azure Blob Storage and returning a SAS URL — is documented in the known enhancements section.

---

### ADR-003 — HUF-only amounts; no in-app currency conversion

**Decision:** All CSV amounts are assumed to be pre-converted to HUF. The application does not accept or convert foreign currencies.

**Rationale:** Hungarian VAT law (Act CXXVII of 2007) requires the declaration to be filed entirely in HUF. The applicable exchange rate (MNB rate on the teljesítési időpont, or ECB rate if contractually agreed) is specific to each invoice and is best resolved in the taxpayer's accounting software before export. Adding a conversion step would require either a live MNB API call per row or an additional CSV column — both are out of scope for the summary-report use case.

---

### ADR-004 — Deployment: Azure Static Web Apps + App Service (no Docker)

**Decision:** Frontend on Azure Static Web Apps (Free → Standard); backend on Azure App Service Linux F1 with DOTNETCORE:9.0 runtime. No Dockerfile, no Container Apps.

**Rationale:** App Service has native .NET runtime support — no container build step, no registry, no image versioning. SWA handles global CDN, HTTPS, and the linked-backend proxy in one resource. GitHub Actions workflows are straightforward `dotnet publish` + `az webapp deploy` / `static-web-apps-deploy`. Total infrastructure provisioning time: ~5 minutes via Azure CLI.

**Trade-off:** F1 has no Always On; cold starts take 10–15 s. Container Apps would scale to zero more cheaply at idle but add Docker build complexity.

---

### ADR-005 — API access restriction via AzureCloud service tag

**Decision:** The App Service accepts traffic only from the `AzureCloud` service tag (all Azure datacenter IPs). All browser traffic reaches the API through the SWA linked-backend proxy; direct calls return `403 Ip Forbidden`.

**Rationale:** Azure Static Web Apps does not have its own service tag. Its proxy traffic originates from Azure-internal infrastructure, which is covered by `AzureCloud`. This blocks all non-Azure internet traffic with zero application-layer changes.

**Trade-off:** `AzureCloud` is broader than needed — any other Azure-hosted service could theoretically reach the App Service URL. The production-grade fix (shared secret header injected by SWA, validated in ASP.NET Core middleware) is documented in the known enhancements section.

---

### ADR-006 — TransactionType as an optional, backward-compatible CSV column

**Decision:** The `TransactionType` column is optional. When absent or blank, all rows default to `Domestic`. A custom `CsvHelper` type converter handles the defaulting.

**Rationale:** Existing CSV exports from accounting systems will not have this column. Making it required would break backward compatibility and force users to modify their export templates before the first upload.

---

### ADR-007 — No authentication; single-function public API

**Decision:** The API has no user authentication, no sessions, and no per-user data persistence. Every request is stateless and self-contained.

**Rationale:** The tool is a document-processing utility, not a multi-tenant SaaS application. The input (CSV) and output (JSON + PDF) are handled entirely client-side between requests. Rate limiting, network-level access restriction, and request size limits provide adequate protection for a publicly accessible tool of this scope.

**Trade-off:** Any authenticated user of the SWA URL can submit files. For a production tax-office deployment, Azure AD B2C or Entra ID authentication would be the natural next layer.

---

### ADR-008 — Built-in ASP.NET Core middleware for all security primitives

**Decision:** Rate limiting (`AddRateLimiter`), request timeouts (`AddRequestTimeouts`), and security headers are implemented using ASP.NET Core 8+ built-in APIs. No third-party middleware packages (e.g. AspNetCoreRateLimit, NWebSec) were added.

**Rationale:** The built-in implementations cover the required policies without additional NuGet dependencies, reducing supply-chain surface area and keeping the csproj minimal.

---

## Getting started (local development)

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

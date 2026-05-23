# Hungarian VAT Declaration Generator

A web application that processes invoice/transaction CSV files and generates a formally correct Hungarian VAT Declaration (ÁFA bevallás) report summary, including PDF export.

## Tech Stack

- **Backend:** ASP.NET Core 9 Web API
- **Frontend:** React 19 + TypeScript (Vite)
- **PDF Generation:** QuestPDF

## Project Structure

```
taxdesk-vat-calculator/
├── backend/          # ASP.NET Core Web API
└── frontend/         # React + TypeScript (Vite)
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Node.js 18+

### Backend

```bash
cd backend
dotnet restore
dotnet run --project VatCalculator.Api
```

API runs at `https://localhost:7001`

### Frontend

```bash
cd frontend
npm install
npm run dev
```

App runs at `http://localhost:5173`

## Input File Format

Upload a CSV file with the following columns:

```
InvoiceId,Date,Direction,PartnerName,TaxNumber,NetAmount,VatRate,VatAmount,GrossAmount
INV-001,2024-01-15,Sale,Acme Kft,12345678-1-41,100000,27,27000,127000
INV-002,2024-01-16,Purchase,Supplier Bt,87654321-2-02,50000,5,2500,52500
```

**Direction:** `Sale` or `Purchase`  
**VatRate:** `27`, `18`, `5`, `0`, or `AAM` (exempt)

## AI Conversation Log

The full, unedited conversation with Claude Code (the AI tool used to build this) is available in [`ai_log.md`](ai_log.md).

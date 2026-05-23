namespace VatCalculator.Api.DTOs;

public class VatReportDto
{
    public DateTime GeneratedAt { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>Declaration period — monthly ("2024-01") or quarterly ("2024-Q2").</summary>
    public string Period { get; set; } = string.Empty;
    public string? TaxpayerName { get; set; }
    public string? TaxpayerTaxNumber { get; set; }

    public int TotalInvoices { get; set; }
    public VatSectionDto Sales { get; set; } = new();
    public VatSectionDto Purchases { get; set; } = new();

    /// <summary>Output VAT minus input VAT. Positive = payable to NAV, negative = refundable.</summary>
    public decimal VatPayable { get; set; }

    /// <summary>Non-blocking warnings, e.g. invoices whose performance date falls outside the declared period.</summary>
    public List<string> Warnings { get; set; } = [];
}

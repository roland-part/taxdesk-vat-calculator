namespace VatCalculator.Api.Models;

public class InvoiceRecord
{
    public string InvoiceId { get; set; } = string.Empty;
    public DateTime PerformanceDate { get; set; }
    public TransactionDirection Direction { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public decimal NetAmount { get; set; }
    public string VatRate { get; set; } = string.Empty;
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
}

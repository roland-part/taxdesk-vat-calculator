namespace VatCalculator.Api.DTOs;

public class VatReportDto
{
    public DateTime GeneratedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalInvoices { get; set; }
    public VatSectionDto Sales { get; set; } = new();
    public VatSectionDto Purchases { get; set; } = new();
}

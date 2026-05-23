namespace VatCalculator.Api.DTOs;

public class VatCategoryLineDto
{
    public string VatRate { get; set; } = string.Empty;
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }
    public int InvoiceCount { get; set; }
}

namespace VatCalculator.Api.DTOs;

public class VatSectionDto
{
    public string Direction { get; set; } = string.Empty;
    public List<VatCategoryLineDto> Lines { get; set; } = [];
    public decimal GrandTotalNet { get; set; }
    public decimal GrandTotalVat { get; set; }
    public decimal GrandTotalGross { get; set; }
}

using VatCalculator.Api.DTOs;
using VatCalculator.Api.Models;

namespace VatCalculator.Api.Services;

public interface IVatCalculationService
{
    VatReportDto Calculate(List<InvoiceRecord> records, string fileName);
}

public class VatCalculationService : IVatCalculationService
{
    private static readonly Dictionary<string, int> VatRateOrder = new()
    {
        ["27"] = 0,
        ["18"] = 1,
        ["5"]  = 2,
        ["0"]  = 3,
        ["AAM"] = 4,
    };

    public VatReportDto Calculate(List<InvoiceRecord> records, string fileName)
    {
        return new VatReportDto
        {
            GeneratedAt = DateTime.UtcNow,
            FileName = fileName,
            TotalInvoices = records.Count,
            Sales = BuildSection(records, TransactionDirection.Sale),
            Purchases = BuildSection(records, TransactionDirection.Purchase),
        };
    }

    private static VatSectionDto BuildSection(List<InvoiceRecord> records, TransactionDirection direction)
    {
        var lines = records
            .Where(r => r.Direction == direction)
            .GroupBy(r => r.VatRate)
            .OrderBy(g => VatRateOrder.GetValueOrDefault(g.Key, int.MaxValue))
            .Select(g => new VatCategoryLineDto
            {
                VatRate = g.Key,
                TotalNet = g.Sum(r => r.NetAmount),
                TotalVat = g.Sum(r => r.VatAmount),
                TotalGross = g.Sum(r => r.GrossAmount),
                InvoiceCount = g.Count(),
            })
            .ToList();

        return new VatSectionDto
        {
            Direction = direction.ToString(),
            Lines = lines,
            GrandTotalNet = lines.Sum(l => l.TotalNet),
            GrandTotalVat = lines.Sum(l => l.TotalVat),
            GrandTotalGross = lines.Sum(l => l.TotalGross),
        };
    }
}

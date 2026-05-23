using System.Text.RegularExpressions;
using VatCalculator.Api.DTOs;
using VatCalculator.Api.Models;

namespace VatCalculator.Api.Services;

public interface IVatCalculationService
{
    VatReportDto Calculate(
        List<InvoiceRecord> records,
        string fileName,
        string period,
        string? taxpayerName,
        string? taxpayerTaxNumber);
}

public class VatCalculationService : IVatCalculationService
{
    private static readonly Dictionary<string, int> VatRateOrder = new()
    {
        ["27"] = 0, ["18"] = 1, ["5"] = 2, ["0"] = 3, ["AAM"] = 4,
    };

    // Matches "2024-01" (monthly) or "2024-Q2" (quarterly)
    private static readonly Regex MonthlyPattern  = new(@"^(\d{4})-(0[1-9]|1[0-2])$");
    private static readonly Regex QuarterlyPattern = new(@"^(\d{4})-Q([1-4])$");

    public VatReportDto Calculate(
        List<InvoiceRecord> records,
        string fileName,
        string period,
        string? taxpayerName,
        string? taxpayerTaxNumber)
    {
        var sales     = BuildSection(records, TransactionDirection.Sale);
        var purchases = BuildSection(records, TransactionDirection.Purchase);

        return new VatReportDto
        {
            GeneratedAt        = DateTime.UtcNow,
            FileName           = fileName,
            Period             = period,
            TaxpayerName       = taxpayerName,
            TaxpayerTaxNumber  = taxpayerTaxNumber,
            TotalInvoices      = records.Count,
            Sales              = sales,
            Purchases          = purchases,
            VatPayable         = sales.GrandTotalVat - purchases.GrandTotalVat,
            Warnings           = GenerateWarnings(records, period),
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
                VatRate      = g.Key,
                TotalNet     = g.Sum(r => r.NetAmount),
                TotalVat     = g.Sum(r => r.VatAmount),
                TotalGross   = g.Sum(r => r.GrossAmount),
                InvoiceCount = g.Count(),
            })
            .ToList();

        return new VatSectionDto
        {
            Direction      = direction.ToString(),
            Lines          = lines,
            GrandTotalNet  = lines.Sum(l => l.TotalNet),
            GrandTotalVat  = lines.Sum(l => l.TotalVat),
            GrandTotalGross = lines.Sum(l => l.TotalGross),
        };
    }

    private static List<string> GenerateWarnings(List<InvoiceRecord> records, string period)
    {
        var warnings = new List<string>();
        var outOfPeriod = records.Where(r => !IsInPeriod(r.PerformanceDate, period)).ToList();

        if (outOfPeriod.Count > 0)
        {
            var preview = string.Join(", ", outOfPeriod.Take(5).Select(r => r.InvoiceId));
            var tail    = outOfPeriod.Count > 5 ? $" and {outOfPeriod.Count - 5} more" : string.Empty;
            warnings.Add(
                $"{outOfPeriod.Count} invoice(s) have a performance date outside the declared period " +
                $"({period}): {preview}{tail}");
        }

        return warnings;
    }

    private static bool IsInPeriod(DateTime date, string period)
    {
        var monthly = MonthlyPattern.Match(period);
        if (monthly.Success)
        {
            return date.Year  == int.Parse(monthly.Groups[1].Value)
                && date.Month == int.Parse(monthly.Groups[2].Value);
        }

        var quarterly = QuarterlyPattern.Match(period);
        if (quarterly.Success)
        {
            var year       = int.Parse(quarterly.Groups[1].Value);
            var quarter    = int.Parse(quarterly.Groups[2].Value);
            var startMonth = (quarter - 1) * 3 + 1;
            return date.Year == year
                && date.Month >= startMonth
                && date.Month <= startMonth + 2;
        }

        return true; // unknown format — don't warn
    }
}

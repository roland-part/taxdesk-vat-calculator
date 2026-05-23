using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using VatCalculator.Api.Models;

namespace VatCalculator.Api.Services;

public record CsvParseResult(List<InvoiceRecord> Records, List<string> Errors);

public interface ICsvParserService
{
    Task<CsvParseResult> ParseAsync(IFormFile file);
}

public class CsvParserService : ICsvParserService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".csv" };

    private static readonly HashSet<string> ValidVatRates =
        new(StringComparer.OrdinalIgnoreCase) { "27", "18", "5", "0", "AAM" };

    private static readonly string[] RequiredHeaders =
        ["InvoiceId", "Date", "Direction", "PartnerName", "NetAmount", "VatRate", "VatAmount", "GrossAmount"];

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public async Task<CsvParseResult> ParseAsync(IFormFile file)
    {
        if (file.Length == 0)
            return new CsvParseResult([], ["The uploaded file is empty."]);

        if (file.Length > MaxFileSizeBytes)
            return new CsvParseResult([], ["File exceeds the maximum allowed size of 10 MB."]);

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return new CsvParseResult([], [$"Invalid file type '{extension}'. Only .csv files are accepted."]);

        var records = new List<InvoiceRecord>();
        var errors = new List<string>();

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
            };

            using var reader = new StreamReader(file.OpenReadStream());
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<InvoiceRecordMap>();

            await csv.ReadAsync();
            csv.ReadHeader();

            var missingHeaders = RequiredHeaders
                .Where(h => !csv.HeaderRecord!.Contains(h, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (missingHeaders.Count > 0)
                return new CsvParseResult([], [$"Missing required CSV columns: {string.Join(", ", missingHeaders)}"]);

            int rowNumber = 1;
            while (await csv.ReadAsync())
            {
                rowNumber++;
                try
                {
                    var record = csv.GetRecord<InvoiceRecord>();
                    if (record == null) continue;

                    var rowErrors = ValidateRecord(record, rowNumber);
                    if (rowErrors.Count > 0)
                    {
                        errors.AddRange(rowErrors);
                        continue;
                    }

                    record.VatRate = record.VatRate.ToUpperInvariant();
                    record.InvoiceId = Sanitize(record.InvoiceId);
                    record.PartnerName = Sanitize(record.PartnerName);
                    if (record.TaxNumber != null)
                        record.TaxNumber = Sanitize(record.TaxNumber);

                    records.Add(record);
                }
                catch (CsvHelperException ex)
                {
                    errors.Add($"Row {rowNumber}: could not parse — {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            return new CsvParseResult([], [$"Failed to read CSV file: {ex.Message}"]);
        }

        if (records.Count == 0 && errors.Count == 0)
            return new CsvParseResult([], ["The CSV file contains no data rows."]);

        return new CsvParseResult(records, errors);
    }

    private static List<string> ValidateRecord(InvoiceRecord r, int row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(r.InvoiceId))
            errors.Add($"Row {row}: InvoiceId is required.");

        if (!ValidVatRates.Contains(r.VatRate))
            errors.Add($"Row {row}: Invalid VatRate '{r.VatRate}'. Allowed: 27, 18, 5, 0, AAM.");

        if (r.NetAmount < 0)
            errors.Add($"Row {row}: NetAmount must not be negative.");

        if (r.VatAmount < 0)
            errors.Add($"Row {row}: VatAmount must not be negative.");

        if (r.GrossAmount < 0)
            errors.Add($"Row {row}: GrossAmount must not be negative.");

        return errors;
    }

    // Strip control characters; prefix formula-injection characters so they are inert in any downstream renderer.
    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        value = new string(value.Where(c => !char.IsControl(c)).ToArray());
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
            value = "'" + value;
        return value;
    }
}

public sealed class InvoiceRecordMap : ClassMap<InvoiceRecord>
{
    public InvoiceRecordMap()
    {
        Map(m => m.InvoiceId).Name("InvoiceId");
        Map(m => m.Date).Name("Date");
        Map(m => m.Direction).Name("Direction");
        Map(m => m.PartnerName).Name("PartnerName");
        Map(m => m.TaxNumber).Name("TaxNumber").Optional();
        Map(m => m.NetAmount).Name("NetAmount");
        Map(m => m.VatRate).Name("VatRate");
        Map(m => m.VatAmount).Name("VatAmount");
        Map(m => m.GrossAmount).Name("GrossAmount");
    }
}

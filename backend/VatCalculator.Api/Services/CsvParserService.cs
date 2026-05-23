using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using VatCalculator.Api.Models;

namespace VatCalculator.Api.Services;

public record CsvParseResult(List<InvoiceRecord> Records, List<string> Errors, List<string> Warnings);

public interface ICsvParserService
{
    Task<CsvParseResult> ParseAsync(IFormFile file, CancellationToken cancellationToken = default);
}

public class CsvParserService : ICsvParserService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".csv" };

    private static readonly HashSet<string> ValidVatRates =
        new(StringComparer.OrdinalIgnoreCase) { "27", "18", "5", "0", "AAM" };

    private static readonly HashSet<string> ValidTransactionTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Domestic", "IntraCommunitySale", "IntraCommunityAcquisition", "Import", "ReverseCharge"
        };

    // Hungarian adószám: XXXXXXXX-Y-ZZ
    private static readonly System.Text.RegularExpressions.Regex HungarianTaxNumberRe =
        new(@"^\d{8}-[1-5]-(0[1-9]|1[0-9]|20|22|41|51)$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly string[] RequiredHeaders =
        ["InvoiceId", "PerformanceDate", "Direction", "PartnerName", "NetAmount", "VatRate", "VatAmount", "GrossAmount"];

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public async Task<CsvParseResult> ParseAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return new CsvParseResult([], ["The uploaded file is empty."], []);

        if (file.Length > MaxFileSizeBytes)
            return new CsvParseResult([], ["File exceeds the maximum allowed size of 10 MB."], []);

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return new CsvParseResult([], [$"Invalid file type '{extension}'. Only .csv files are accepted."], []);

        var records    = new List<InvoiceRecord>();
        var errors     = new List<string>();
        var warnings   = new List<string>();
        var seenIds    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
            };

            // ── Binary file detection ─────────────────────────────────────────
            // Read the first 512 bytes and reject if a null byte (0x00) is found,
            // which reliably distinguishes binary files (xlsx, pdf, zip, etc.) from text.
            var rawStream = file.OpenReadStream();
            var peek = new byte[Math.Min(512, (int)file.Length)];
            int bytesRead = await rawStream.ReadAsync(peek.AsMemory(0, peek.Length), cancellationToken);
            if (peek.AsSpan(0, bytesRead).Contains((byte)0))
                return new CsvParseResult([],
                    ["The uploaded file appears to be a binary file (e.g. Excel .xlsx). Please export it as a plain CSV text file and re-upload."],
                    []);
            rawStream.Seek(0, SeekOrigin.Begin);

            // UTF-8 with BOM detection enabled. Files saved as Windows-1252 (e.g. from older
            // Excel "Save As CSV") will cause a DecoderFallbackException here, which is caught
            // below and reported as a clear encoding error rather than silently garbling names.
            var utf8 = new System.Text.UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);
            using var reader = new StreamReader(rawStream, utf8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<InvoiceRecordMap>();

            await csv.ReadAsync();
            csv.ReadHeader();

            var missingHeaders = RequiredHeaders
                .Where(h => !csv.HeaderRecord!.Contains(h, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (missingHeaders.Count > 0)
                return new CsvParseResult([], [$"Missing required CSV columns: {string.Join(", ", missingHeaders)}"], []);

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

                    record.VatRate     = record.VatRate.ToUpperInvariant();
                    record.InvoiceId   = Sanitize(record.InvoiceId);
                    record.PartnerName = Sanitize(record.PartnerName);
                    if (record.TaxNumber != null)
                        record.TaxNumber = Sanitize(record.TaxNumber);

                    if (!seenIds.Add(record.InvoiceId))
                        warnings.Add($"Row {rowNumber}: Duplicate InvoiceId '{record.InvoiceId}' — this invoice will be counted twice.");

                    warnings.AddRange(CrossValidateRecord(record, rowNumber));
                    records.Add(record);
                }
                catch (CsvHelperException ex)
                {
                    errors.Add($"Row {rowNumber}: could not parse — {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        }
        catch (System.Text.DecoderFallbackException)
        {
            return new CsvParseResult([],
                ["The file contains invalid UTF-8 characters. Please save the CSV as UTF-8 (in Excel: File → Save As → CSV UTF-8 (with BOM)) and re-upload."],
                []);
        }
        catch (Exception ex)
        {
            return new CsvParseResult([], [$"Failed to read CSV file: {ex.Message}"], []);
        }

        if (records.Count == 0 && errors.Count == 0)
            return new CsvParseResult([], ["The CSV file contains no data rows."], []);

        return new CsvParseResult(records, errors, warnings);
    }

    private static List<string> ValidateRecord(InvoiceRecord r, int row)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(r.InvoiceId))
            errors.Add($"Row {row}: InvoiceId is required.");

        if (string.IsNullOrWhiteSpace(r.PartnerName))
            errors.Add($"Row {row}: PartnerName is required.");
        else if (r.PartnerName.Length > 200)
            errors.Add($"Row {row}: PartnerName must not exceed 200 characters.");

        if (!string.IsNullOrWhiteSpace(r.TaxNumber) && !HungarianTaxNumberRe.IsMatch(r.TaxNumber))
            errors.Add($"Row {row}: TaxNumber '{r.TaxNumber}' is not a valid Hungarian adószám (expected XXXXXXXX-Y-ZZ, e.g. 12345678-2-41).");

        if (!ValidVatRates.Contains(r.VatRate))
            errors.Add($"Row {row}: Invalid VatRate '{r.VatRate}'. Allowed values: 27, 18, 5, 0, AAM.");

        if (r.PerformanceDate > DateTime.UtcNow.Date)
            errors.Add($"Row {row}: PerformanceDate {r.PerformanceDate:yyyy-MM-dd} is in the future.");

        // Direction/TransactionType consistency
        if (r.TransactionType == TransactionType.IntraCommunitySale && r.Direction != TransactionDirection.Sale)
            errors.Add($"Row {row}: TransactionType 'IntraCommunitySale' is only valid for Direction 'Sale'.");

        if (r.TransactionType == TransactionType.IntraCommunityAcquisition && r.Direction != TransactionDirection.Purchase)
            errors.Add($"Row {row}: TransactionType 'IntraCommunityAcquisition' is only valid for Direction 'Purchase'.");

        if (r.TransactionType == TransactionType.Import && r.Direction != TransactionDirection.Purchase)
            errors.Add($"Row {row}: TransactionType 'Import' is only valid for Direction 'Purchase'.");

        return errors;
    }

    // Cross-validates VatAmount and GrossAmount against calculated values.
    // Returns non-blocking warnings; tolerance of ±1 to allow for legitimate rounding.
    private static List<string> CrossValidateRecord(InvoiceRecord r, int row)
    {
        var warnings = new List<string>();
        const decimal tolerance = 1m;

        var vatRatePercent  = r.VatRate == "AAM" ? 0m : decimal.Parse(r.VatRate, CultureInfo.InvariantCulture);
        var expectedVat     = r.NetAmount * (vatRatePercent / 100m);
        var expectedGross   = r.NetAmount + expectedVat;

        if (Math.Abs(r.VatAmount - expectedVat) > tolerance)
            warnings.Add(
                $"Row {row} ({r.InvoiceId}): VatAmount {r.VatAmount:N2} differs from calculated " +
                $"{expectedVat:N2} (Net {r.NetAmount:N2} × {r.VatRate}{(r.VatRate == "AAM" ? "" : "%")}). " +
                "Check for rounding errors.");

        if (Math.Abs(r.GrossAmount - expectedGross) > tolerance)
            warnings.Add(
                $"Row {row} ({r.InvoiceId}): GrossAmount {r.GrossAmount:N2} differs from calculated " +
                $"{expectedGross:N2} (Net + VAT). Check for rounding errors.");

        return warnings;
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
        Map(m => m.PerformanceDate).Name("PerformanceDate");
        Map(m => m.Direction).Name("Direction");
        Map(m => m.TransactionType).Name("TransactionType")
            .Optional()
            .TypeConverter<TransactionTypeConverter>();
        Map(m => m.PartnerName).Name("PartnerName");
        Map(m => m.TaxNumber).Name("TaxNumber").Optional();
        Map(m => m.NetAmount).Name("NetAmount");
        Map(m => m.VatRate).Name("VatRate");
        Map(m => m.VatAmount).Name("VatAmount");
        Map(m => m.GrossAmount).Name("GrossAmount");
    }
}

/// <summary>
/// Parses TransactionType from a CSV cell, defaulting to Domestic for absent or blank values.
/// </summary>
public sealed class TransactionTypeConverter : CsvHelper.TypeConversion.DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, CsvHelper.IReaderRow row, CsvHelper.Configuration.MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text)) return TransactionType.Domestic;
        if (Enum.TryParse<TransactionType>(text.Trim(), ignoreCase: true, out var result)) return result;
        throw new InvalidOperationException(
            $"Invalid TransactionType '{text.Trim()}'. " +
            "Allowed values: Domestic, IntraCommunitySale, IntraCommunityAcquisition, Import, ReverseCharge.");
    }
}

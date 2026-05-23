using Microsoft.AspNetCore.Mvc;
using VatCalculator.Api.DTOs;
using VatCalculator.Api.Services;

namespace VatCalculator.Api.Controllers;

[ApiController]
[Route("api/vat")]
public class VatController : ControllerBase
{
    private readonly ICsvParserService _csvParser;
    private readonly IVatCalculationService _vatCalculation;
    private readonly IVatReportPdfService _pdfService;

    public VatController(
        ICsvParserService csvParser,
        IVatCalculationService vatCalculation,
        IVatReportPdfService pdfService)
    {
        _csvParser = csvParser;
        _vatCalculation = vatCalculation;
        _pdfService = pdfService;
    }

    private static readonly System.Text.RegularExpressions.Regex PeriodPattern =
        new(@"^(\d{4}-(0[1-9]|1[0-2])|\d{4}-Q[1-4])$");

    /// <summary>
    /// Upload a CSV file of invoices/transactions and receive a structured VAT report.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_485_760)]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        [FromForm] string? period,
        [FromForm] string? taxpayerName,
        [FromForm] string? taxpayerTaxNumber)
    {
        if (file is null)
            return BadRequest(new ErrorResponseDto { Message = "No file was uploaded." });

        if (string.IsNullOrWhiteSpace(period) || !PeriodPattern.IsMatch(period))
            return BadRequest(new ErrorResponseDto
            {
                Message = "A valid declaration period is required.",
                Errors  = new Dictionary<string, string[]>
                {
                    ["period"] = ["Format must be YYYY-MM (e.g. 2024-01) or YYYY-Q# (e.g. 2024-Q1)."]
                }
            });

        var result = await _csvParser.ParseAsync(file);

        if (result.Errors.Count > 0)
            return UnprocessableEntity(new ErrorResponseDto
            {
                Message = "The CSV file contains validation errors.",
                Errors  = new Dictionary<string, string[]> { ["csv"] = [.. result.Errors] }
            });

        var report = _vatCalculation.Calculate(
            result.Records, file.FileName, period,
            taxpayerName?.Trim(), taxpayerTaxNumber?.Trim());

        return Ok(report);
    }

    /// <summary>
    /// Generate a PDF from a previously computed VAT report and stream it back.
    /// The client sends the report JSON it received from /upload.
    /// </summary>
    [HttpPost("report/pdf")]
    public IActionResult GeneratePdf([FromBody] VatReportDto report)
    {
        var pdfBytes = _pdfService.Generate(report);
        var fileName = $"vat-report-{report.GeneratedAt:yyyyMMdd-HHmm}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}

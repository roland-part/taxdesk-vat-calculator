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

    /// <summary>
    /// Upload a CSV file of invoices/transactions and receive a structured VAT report.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_485_760)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null)
            return BadRequest(new ErrorResponseDto { Message = "No file was uploaded." });

        var result = await _csvParser.ParseAsync(file);

        if (result.Errors.Count > 0)
            return UnprocessableEntity(new ErrorResponseDto
            {
                Message = "The CSV file contains validation errors.",
                Errors = new Dictionary<string, string[]> { ["csv"] = [.. result.Errors] }
            });

        var report = _vatCalculation.Calculate(result.Records, file.FileName);
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

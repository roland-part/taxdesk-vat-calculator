using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VatCalculator.Api.DTOs;

namespace VatCalculator.Api.Services;

public interface IVatReportPdfService
{
    byte[] Generate(VatReportDto report);
}

public class VatReportPdfService : IVatReportPdfService
{
    public byte[] Generate(VatReportDto report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Column(col =>
                {
                    col.Item().Text("Hungarian VAT Declaration Report")
                        .FontSize(18).Bold();
                    col.Item().Text("ÁFA Bevallás Összesítő")
                        .FontSize(13).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(4);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"File: {report.FileName}");
                        row.RelativeItem().AlignRight()
                            .Text($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
                    });

                    col.Item().Text($"Total invoices processed: {report.TotalInvoices}");

                    col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    col.Item().Element(c => RenderSection(c, report.Sales, Colors.Blue.Medium));

                    col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    col.Item().Element(c => RenderSection(c, report.Purchases, Colors.Green.Darken1));
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("TaxDesk VAT Calculator  |  Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void RenderSection(IContainer container, VatSectionDto section, string accentColor)
    {
        var title = section.Direction == "Sale"
            ? "Sales — Output VAT (Fizetendő ÁFA)"
            : "Purchases — Input VAT (Levonható ÁFA)";

        container.Column(col =>
        {
            col.Spacing(6);
            col.Item().Text(title).FontSize(12).Bold().FontColor(accentColor);

            if (section.Lines.Count == 0)
            {
                col.Item().Text("No transactions in this period.")
                    .Italic().FontColor(Colors.Grey.Medium);
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(90);
                    c.ConstantColumn(60);
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                table.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("VAT Rate");
                    h.Cell().Element(HeaderCell).AlignRight().Text("Invoices");
                    h.Cell().Element(HeaderCell).AlignRight().Text("Net Amount");
                    h.Cell().Element(HeaderCell).AlignRight().Text("VAT Amount");
                    h.Cell().Element(HeaderCell).AlignRight().Text("Gross Amount");
                });

                bool shade = false;
                foreach (var line in section.Lines)
                {
                    var label = line.VatRate == "AAM" ? "AAM (exempt)" : $"{line.VatRate}%";
                    var bg = shade ? Colors.Grey.Lighten4 : Colors.White;
                    shade = !shade;

                    table.Cell().Element(c => DataCell(c, bg)).Text(label);
                    table.Cell().Element(c => DataCell(c, bg)).AlignRight().Text(line.InvoiceCount.ToString());
                    table.Cell().Element(c => DataCell(c, bg)).AlignRight().Text(Fmt(line.TotalNet));
                    table.Cell().Element(c => DataCell(c, bg)).AlignRight().Text(Fmt(line.TotalVat));
                    table.Cell().Element(c => DataCell(c, bg)).AlignRight().Text(Fmt(line.TotalGross));
                }

                table.Cell().Element(TotalCell).Text("TOTAL");
                table.Cell().Element(TotalCell).AlignRight()
                    .Text(section.Lines.Sum(l => l.InvoiceCount).ToString());
                table.Cell().Element(TotalCell).AlignRight().Text(Fmt(section.GrandTotalNet));
                table.Cell().Element(TotalCell).AlignRight().Text(Fmt(section.GrandTotalVat));
                table.Cell().Element(TotalCell).AlignRight().Text(Fmt(section.GrandTotalGross));
            });
        });
    }

    private static IContainer HeaderCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Grey.Medium)
         .Padding(5).DefaultTextStyle(x => x.Bold());

    private static IContainer DataCell(IContainer c, string background) =>
        c.Background(background).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);

    private static IContainer TotalCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten1).BorderTop(2).BorderColor(Colors.Grey.Medium)
         .Padding(5).DefaultTextStyle(x => x.Bold());

    private static string Fmt(decimal value) =>
        value.ToString("N2", CultureInfo.InvariantCulture);
}

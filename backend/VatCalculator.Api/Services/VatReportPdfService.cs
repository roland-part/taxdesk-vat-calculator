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
                    col.Spacing(6);

                    // ── Declaration header ──────────────────────────────────
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                        void MetaRow(string label, string? value)
                        {
                            if (string.IsNullOrWhiteSpace(value)) return;
                            t.Cell().Text(label).FontColor(Colors.Grey.Darken1);
                            t.Cell().Text(value).Bold();
                        }

                        MetaRow("Declaration period:", FormatPeriod(report.Period));
                        MetaRow("Taxpayer:",           report.TaxpayerName);
                        MetaRow("Tax number:",         report.TaxpayerTaxNumber);
                        MetaRow("Source file:",        report.FileName);
                        MetaRow("Total invoices:",     report.TotalInvoices.ToString());
                        MetaRow("Generated (UTC):",    report.GeneratedAt.ToString("yyyy-MM-dd HH:mm"));
                    });

                    col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    // ── VAT sections ────────────────────────────────────────
                    col.Item().Element(c => RenderSection(c, report.Sales, Colors.Blue.Medium));
                    col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    col.Item().Element(c => RenderSection(c, report.Purchases, Colors.Green.Darken1));

                    // ── Net VAT position ────────────────────────────────────
                    col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    col.Item().Element(c => RenderNetPosition(c, report));

                    // ── Warnings ────────────────────────────────────────────
                    if (report.Warnings.Count > 0)
                    {
                        col.Item().PaddingTop(8).Column(warn =>
                        {
                            warn.Item().Text("⚠ Warnings").Bold().FontColor(Colors.Orange.Darken2);
                            foreach (var w in report.Warnings)
                                warn.Item().PaddingLeft(8).Text($"• {w}").FontColor(Colors.Orange.Darken3);
                        });
                    }
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

    private static void RenderNetPosition(IContainer container, VatReportDto report)
    {
        var payable    = report.VatPayable;
        var isPayable  = payable >= 0;
        var label      = isPayable ? "VAT Payable to NAV (Fizetendő ÁFA egyenleg)" : "VAT Refundable (Visszaigényelhető ÁFA)";
        var color      = isPayable ? Colors.Red.Darken2 : Colors.Green.Darken2;

        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Net VAT Position").FontSize(12).Bold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });

                t.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("Output VAT (Sales)");
                    h.Cell().Element(HeaderCell).AlignCenter().Text("Input VAT (Purchases)");
                    h.Cell().Element(HeaderCell).AlignRight().Text(label);
                });

                t.Cell().Element(c => DataCell(c, Colors.White)).Text(Fmt(report.Sales.GrandTotalVat));
                t.Cell().Element(c => DataCell(c, Colors.White)).AlignCenter().Text(Fmt(report.Purchases.GrandTotalVat));
                t.Cell().Element(c => DataCell(c, Colors.White)).AlignRight()
                    .Text(Fmt(Math.Abs(payable))).Bold().FontColor(color);
            });
        });
    }

    private static string FormatPeriod(string period)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(period, @"^\d{4}-(0[1-9]|1[0-2])$"))
        {
            if (DateTime.TryParseExact(period + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
                return dt.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (System.Text.RegularExpressions.Regex.IsMatch(period, @"^\d{4}-Q([1-4])$"))
        {
            var year = period[..4];
            var q    = period[6];
            var months = q switch { '1' => "January–March", '2' => "April–June",
                                    '3' => "July–September", _ => "October–December" };
            return $"Q{q} {year} ({months})";
        }
        if (System.Text.RegularExpressions.Regex.IsMatch(period, @"^\d{4}$"))
            return $"{period} (Annual)";
        return period;
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

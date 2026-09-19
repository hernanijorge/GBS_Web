using ClosedXML.Excel;
using GBS_Web.Models;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace GBS_Web.Services;

// Port of ReportService.vb's GerarPdfComponentsSummary/GerarExcelComponentsSummary
// — same 11 columns, same order, same "GBS Components — Consolidated Summary"
// title. The first 6 columns are descriptive (group key); the last 5 are numeric
// totals and get the distinct green header (RGB 16,185,129), matching the desktop.
public static class ComponentSummaryReportGenerator
{
    private static readonly DeviceRgb ColorGrayHeader = new(55, 65, 81);
    private static readonly DeviceRgb ColorGreenHeader = new(16, 185, 129);
    private static readonly DeviceRgb ColorRowAlt = new(249, 250, 251);
    private static readonly DeviceRgb ColorDark = new(31, 41, 55);
    private static readonly DeviceRgb ColorMuted = new(107, 114, 128);
    private const int DescriptiveColumnCount = 6;

    private static readonly (string Header, float Width)[] Columns =
    [
        ("Type", 50),
        ("Capacity (GB)", 60),
        ("Generation", 60),
        ("Speed (MHz)", 55),
        ("CPU", 70),
        ("Storage (GB)", 55),
        ("Total", 36),
        ("In Stock", 42),
        ("Installed", 44),
        ("Sold", 32),
        ("Scrapped", 42)
    ];

    private static string[] RowValues(ComponentSummary s) =>
    [
        s.ComponentType,
        s.CapacityGb > 0 ? s.CapacityGb.ToString() : "",
        s.Generation ?? "",
        s.SpeedMhz?.ToString() ?? "",
        s.Cpu ?? "",
        s.StorageGb?.ToString() ?? "",
        s.Total.ToString(),
        s.InStock.ToString(),
        s.Installed.ToString(),
        s.Sold.ToString(),
        s.Scrapped.ToString()
    ];

    public static byte[] GeneratePdf(List<ComponentSummary> items)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        var doc = new Document(pdf, PageSize.A4.Rotate());
        doc.SetMargins(20, 20, 30, 20);

        var fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        doc.Add(new Paragraph("GBS Components — Consolidated Summary").SetFont(fontBold).SetFontSize(13).SetFontColor(ColorDark).SetMarginBottom(2));
        doc.Add(new Paragraph($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  |  Groups: {items.Count}")
            .SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorMuted).SetMarginBottom(8));

        var widths = Columns.Select(c => c.Width).ToArray();
        var table = new Table(UnitValue.CreatePercentArray(widths)).UseAllAvailableWidth();

        for (var i = 0; i < Columns.Length; i++)
        {
            table.AddHeaderCell(new Cell()
                .Add(new Paragraph(Columns[i].Header).SetFont(fontBold).SetFontSize(7.5f).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(i >= DescriptiveColumnCount ? ColorGreenHeader : ColorGrayHeader)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetPadding(5)
                .SetBorder(Border.NO_BORDER));
        }

        for (var i = 0; i < items.Count; i++)
        {
            var bg = i % 2 == 1 ? ColorRowAlt : ColorConstants.WHITE;
            var values = RowValues(items[i]);
            for (var c = 0; c < values.Length; c++)
            {
                table.AddCell(new Cell()
                    .Add(new Paragraph(values[c]).SetFont(fontNormal).SetFontSize(7).SetFontColor(ColorDark))
                    .SetBackgroundColor(bg)
                    .SetTextAlignment(c >= DescriptiveColumnCount ? TextAlignment.CENTER : TextAlignment.LEFT)
                    .SetPadding(4)
                    .SetBorder(Border.NO_BORDER));
            }
        }

        doc.Add(table);
        doc.Close();
        return stream.ToArray();
    }

    public static byte[] GenerateExcel(List<ComponentSummary> items)
    {
        using var stream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Summary");
            const int headerRow = 5;

            ws.Cell(1, 4).Value = "GBS Components — Consolidated Summary";
            ws.Cell(1, 4).Style.Font.Bold = true;
            ws.Cell(1, 4).Style.Font.FontSize = 16;
            ws.Range(1, 4, 1, 7).Merge();

            ws.Cell(2, 4).Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Groups: {items.Count}";
            ws.Cell(2, 4).Style.Font.FontSize = 10;
            ws.Cell(2, 4).Style.Font.FontColor = XLColor.FromArgb(107, 114, 128);
            ws.Range(2, 4, 2, 7).Merge();

            for (var c = 0; c < Columns.Length; c++)
            {
                var cell = ws.Cell(headerRow, c + 1);
                cell.Value = Columns[c].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = c >= DescriptiveColumnCount
                    ? XLColor.FromArgb(16, 185, 129)
                    : XLColor.FromArgb(55, 65, 81);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (var r = 0; r < items.Count; r++)
            {
                var values = RowValues(items[r]);
                for (var c = 0; c < values.Length; c++)
                {
                    var cell = ws.Cell(headerRow + r + 1, c + 1);
                    cell.Value = values[c];
                    if (c >= DescriptiveColumnCount)
                    {
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }
                if (r % 2 == 1)
                {
                    var rowRange = ws.Range(headerRow + r + 1, 1, headerRow + r + 1, Columns.Length);
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(249, 250, 251);
                }
            }

            for (var c = 0; c < Columns.Length; c++)
            {
                ws.Column(c + 1).Width = 16;
            }

            if (items.Count > 0)
            {
                ws.Range(headerRow, 1, headerRow + items.Count, Columns.Length).SetAutoFilter();
            }
            ws.SheetView.FreezeRows(headerRow);

            wb.SaveAs(stream);
        }
        return stream.ToArray();
    }
}

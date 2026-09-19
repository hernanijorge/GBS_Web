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

// Port of ReportService.vb's GerarPdfRemessa/GerarExcelRemessa — same 8 columns,
// same order, same "GBS Shipment Report" title. The desktop's shipment report has
// no Notes column (unlike the base inventory report), so this doesn't add one.
public static class ShipmentReportGenerator
{
    private static readonly DeviceRgb ColorGrayHeader = new(55, 65, 81);
    private static readonly DeviceRgb ColorRowAlt = new(249, 250, 251);
    private static readonly DeviceRgb ColorDark = new(31, 41, 55);
    private static readonly DeviceRgb ColorMuted = new(107, 114, 128);

    private static readonly (string Header, float Width)[] Columns =
    [
        ("Internal UID", 56),
        ("Manufacturer", 68),
        ("Model", 58),
        ("Serial Number", 72),
        ("Processor", 80),
        ("RAM GB", 32),
        ("Storage GB", 42),
        ("Battery Condition", 68)
    ];

    private static string[] RowValues(ItemRemessa item) =>
    [
        item.InternalUid,
        item.Manufacturer ?? "",
        item.Model ?? "",
        item.SerialNumber ?? "",
        item.CpuModel ?? "",
        item.RamGb?.ToString() ?? "",
        item.StorageGb?.ToString() ?? "",
        item.ConditionStatus ?? ""
    ];

    public static byte[] GeneratePdf(List<ItemRemessa> items, string remessaRef)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        var doc = new Document(pdf, PageSize.A4.Rotate());
        doc.SetMargins(20, 20, 30, 20);

        var fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        doc.Add(new Paragraph("GBS Shipment Report").SetFont(fontBold).SetFontSize(13).SetFontColor(ColorDark).SetMarginBottom(2));
        doc.Add(new Paragraph($"Shipment: {remessaRef}  |  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  |  Total items: {items.Count}")
            .SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorMuted).SetMarginBottom(8));

        var widths = Columns.Select(c => c.Width).ToArray();
        var table = new Table(UnitValue.CreatePercentArray(widths)).UseAllAvailableWidth();

        foreach (var col in Columns)
        {
            table.AddHeaderCell(new Cell()
                .Add(new Paragraph(col.Header).SetFont(fontBold).SetFontSize(7.5f).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(ColorGrayHeader)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetPadding(5)
                .SetBorder(Border.NO_BORDER));
        }

        for (var i = 0; i < items.Count; i++)
        {
            var bg = i % 2 == 1 ? ColorRowAlt : ColorConstants.WHITE;
            foreach (var val in RowValues(items[i]))
            {
                table.AddCell(new Cell()
                    .Add(new Paragraph(val).SetFont(fontNormal).SetFontSize(7).SetFontColor(ColorDark))
                    .SetBackgroundColor(bg)
                    .SetPadding(4)
                    .SetBorder(Border.NO_BORDER));
            }
        }

        doc.Add(table);
        doc.Close();
        return stream.ToArray();
    }

    public static byte[] GenerateExcel(List<ItemRemessa> items, string remessaRef)
    {
        using var stream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Shipment Report");
            const int headerRow = 5;

            ws.Cell(1, 4).Value = "GBS Shipment Report";
            ws.Cell(1, 4).Style.Font.Bold = true;
            ws.Cell(1, 4).Style.Font.FontSize = 16;
            ws.Range(1, 4, 1, 8).Merge();

            ws.Cell(2, 4).Value = $"Shipment: {remessaRef} | Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Total items: {items.Count}";
            ws.Cell(2, 4).Style.Font.FontSize = 10;
            ws.Cell(2, 4).Style.Font.FontColor = XLColor.FromArgb(107, 114, 128);
            ws.Range(2, 4, 2, 8).Merge();

            for (var c = 0; c < Columns.Length; c++)
            {
                var cell = ws.Cell(headerRow, c + 1);
                cell.Value = Columns[c].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(55, 65, 81);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (var r = 0; r < items.Count; r++)
            {
                var values = RowValues(items[r]);
                for (var c = 0; c < values.Length; c++)
                {
                    ws.Cell(headerRow + r + 1, c + 1).Value = values[c];
                }
                if (r % 2 == 1)
                {
                    var rowRange = ws.Range(headerRow + r + 1, 1, headerRow + r + 1, Columns.Length);
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(249, 250, 251);
                }
            }

            for (var c = 0; c < Columns.Length; c++)
            {
                ws.Column(c + 1).Width = 18;
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

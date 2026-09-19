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

// Port of frmImportacao.vb's MontarHtmlRelatorioQualidade (GerarRelatorioQualidade)
// — same summary cards (Total/OK/Issues/Issue rate) and same 10-column table, with
// problem rows highlighted. The desktop writes an HTML file with a .doc extension;
// here it's a real PDF (itext7) / Excel (ClosedXML), matching every other report
// ported in this pass. The manual "Problem?" checkbox override in the on-screen
// grid is not reflected here — the export always uses the server-computed flag
// (see Import.razor).
public static class ImportQualityReportGenerator
{
    private static readonly DeviceRgb ColorGrayHeader = new(55, 65, 81);
    private static readonly DeviceRgb ColorBad = new(254, 226, 226);
    private static readonly DeviceRgb ColorBadText = new(127, 29, 29);
    private static readonly DeviceRgb ColorRowAlt = new(249, 250, 251);
    private static readonly DeviceRgb ColorDark = new(31, 41, 55);
    private static readonly DeviceRgb ColorMuted = new(107, 114, 128);

    private static readonly (string Header, float Width)[] Columns =
    [
        ("Internal UID", 56),
        ("Manufacturer", 60),
        ("Model", 56),
        ("Serial Number", 64),
        ("Battery Condition", 56),
        ("Status", 50),
        ("Batch", 56),
        ("Problem?", 40),
        ("Issue Reason", 90),
        ("Observation", 90)
    ];

    private static string[] RowValues(ImportAnalysisRow r) =>
    [
        r.InternalUid,
        r.Manufacturer ?? "",
        r.Model ?? "",
        r.SerialNumber ?? "",
        r.ConditionStatus ?? "",
        r.Status ?? "",
        r.SourceBatch ?? "",
        r.Problem ? "Yes" : "No",
        r.IssueReason,
        r.Observation ?? ""
    ];

    public static byte[] GeneratePdf(List<ImportAnalysisRow> items, string sourceFileName)
    {
        var total = items.Count;
        var issues = items.Count(r => r.Problem);
        var ok = total - issues;
        var issueRate = total == 0 ? 0 : Math.Round(issues * 100m / total, 1);

        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        var doc = new Document(pdf, PageSize.A4.Rotate());
        doc.SetMargins(20, 20, 30, 20);

        var fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        doc.Add(new Paragraph("GBS Import Quality Report").SetFont(fontBold).SetFontSize(13).SetFontColor(ColorDark).SetMarginBottom(2));
        doc.Add(new Paragraph($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  |  Source file: {sourceFileName}")
            .SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorMuted).SetMarginBottom(8));

        var cards = new Table(UnitValue.CreatePercentArray([1, 1, 1, 1])).UseAllAvailableWidth().SetMarginBottom(10);
        AddCard(cards, fontNormal, fontBold, "Total imported", total.ToString());
        AddCard(cards, fontNormal, fontBold, "OK / According to spreadsheet", ok.ToString());
        AddCard(cards, fontNormal, fontBold, "With issue", issues.ToString());
        AddCard(cards, fontNormal, fontBold, "Issue rate", $"{issueRate:0.0}%");
        doc.Add(cards);

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
            var bg = items[i].Problem ? ColorBad : (i % 2 == 1 ? ColorRowAlt : ColorConstants.WHITE);
            var fg = items[i].Problem ? ColorBadText : ColorDark;
            foreach (var val in RowValues(items[i]))
            {
                table.AddCell(new Cell()
                    .Add(new Paragraph(val).SetFont(fontNormal).SetFontSize(7).SetFontColor(fg))
                    .SetBackgroundColor(bg)
                    .SetPadding(4)
                    .SetBorder(Border.NO_BORDER));
            }
        }

        doc.Add(table);
        doc.Close();
        return stream.ToArray();
    }

    private static void AddCard(Table cards, PdfFont fontNormal, PdfFont fontBold, string label, string value)
    {
        var cell = new Cell()
            .SetBorder(new SolidBorder(new DeviceRgb(209, 213, 219), 1))
            .SetPadding(8);
        cell.Add(new Paragraph(label).SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorMuted).SetMarginBottom(2));
        cell.Add(new Paragraph(value).SetFont(fontBold).SetFontSize(16).SetFontColor(ColorDark));
        cards.AddCell(cell);
    }

    public static byte[] GenerateExcel(List<ImportAnalysisRow> items, string sourceFileName)
    {
        var total = items.Count;
        var issues = items.Count(r => r.Problem);
        var ok = total - issues;
        var issueRate = total == 0 ? 0 : Math.Round(issues * 100m / total, 1);

        using var stream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Import Quality");
            const int headerRow = 6;

            ws.Cell(1, 4).Value = "GBS Import Quality Report";
            ws.Cell(1, 4).Style.Font.Bold = true;
            ws.Cell(1, 4).Style.Font.FontSize = 16;
            ws.Range(1, 4, 1, 8).Merge();

            ws.Cell(2, 4).Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Source file: {sourceFileName}";
            ws.Cell(2, 4).Style.Font.FontSize = 10;
            ws.Cell(2, 4).Style.Font.FontColor = XLColor.FromArgb(107, 114, 128);
            ws.Range(2, 4, 2, 8).Merge();

            ws.Cell(4, 1).Value = "Total imported";
            ws.Cell(4, 2).Value = total;
            ws.Cell(4, 3).Value = "OK / According to spreadsheet";
            ws.Cell(4, 4).Value = ok;
            ws.Cell(4, 5).Value = "With issue";
            ws.Cell(4, 6).Value = issues;
            ws.Cell(4, 7).Value = "Issue rate";
            ws.Cell(4, 8).Value = $"{issueRate:0.0}%";
            ws.Range(4, 1, 4, 8).Style.Font.Bold = true;

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
                if (items[r].Problem)
                {
                    var rowRange = ws.Range(headerRow + r + 1, 1, headerRow + r + 1, Columns.Length);
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 226, 226);
                    rowRange.Style.Font.FontColor = XLColor.FromArgb(127, 29, 29);
                }
                else if (r % 2 == 1)
                {
                    var rowRange = ws.Range(headerRow + r + 1, 1, headerRow + r + 1, Columns.Length);
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(249, 250, 251);
                }
            }

            for (var c = 0; c < Columns.Length; c++)
            {
                ws.Column(c + 1).Width = c is 8 or 9 ? 40 : 16;
            }
            ws.Column(9).Style.Alignment.WrapText = true;
            ws.Column(10).Style.Alignment.WrapText = true;

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

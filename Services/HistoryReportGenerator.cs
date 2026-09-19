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

// Port of frmHistoricoEquipamento.vb's MontarHtmlHistorico (GerarRelatorioHistorico)
// — same 3 sections in the same order (Arrival, Upgrades, Shipments), same columns,
// same running totals. The desktop writes this as an HTML file with a .doc
// extension; here it's a real PDF (itext7) / Excel (ClosedXML) per the project's
// established report pattern instead.
public static class HistoryReportGenerator
{
    private static readonly DeviceRgb ColorGrayHeader = new(55, 65, 81);
    private static readonly DeviceRgb ColorRowAlt = new(249, 250, 251);
    private static readonly DeviceRgb ColorDark = new(31, 41, 55);
    private static readonly DeviceRgb ColorMuted = new(107, 114, 128);

    private static readonly string[] ArrivalHeaders =
        ["Date Registered", "Source Batch", "Condition", "Status", "Serial Number", "Notes"];

    private static readonly string[] UpgradeHeaders =
        ["Date", "Component", "Before", "After", "Source", "Cost (USD)", "Part S/N", "Technician", "Notes"];

    private static readonly string[] ShipmentHeaders =
        ["Ref", "Carrier", "Tracking", "Recipient", "Est. Delivery", "Actual Delivery", "Status", "Sale Price", "Ship. Cost", "Notes"];

    private static string[] ArrivalValues(Equipamento eq) =>
    [
        eq.DataCadastro?.ToString("yyyy-MM-dd") ?? "",
        eq.SourceBatch ?? "",
        eq.ConditionStatus,
        eq.Status,
        eq.SerialNumber ?? "",
        eq.Notes ?? ""
    ];

    private static string[] UpgradeValues(HistoryUpgradeRow u) =>
    [
        u.DataUpgrade?.ToString("yyyy-MM-dd") ?? "",
        u.ComponentType,
        u.ValueBefore ?? "",
        u.ValueAfter ?? "",
        u.SourceOrigem ?? "",
        (u.CostUsd ?? 0).ToString("N2"),
        u.PartSerial ?? "",
        u.Technician ?? "",
        u.Notes ?? ""
    ];

    private static string[] ShipmentValues(HistoryShipmentRow s) =>
    [
        s.RemessaRef,
        s.Carrier ?? "",
        s.TrackingNumber ?? "",
        s.RecipientName ?? "",
        s.DataEnvio?.ToString("yyyy-MM-dd") ?? "",
        s.DataEntrega?.ToString("yyyy-MM-dd") ?? "",
        s.StatusRemessa ?? "",
        (s.SalePriceUsd ?? 0).ToString("N2"),
        (s.ShippingCostUsd ?? 0).ToString("N2"),
        s.ItemNotes ?? ""
    ];

    public static byte[] GeneratePdf(Equipamento eq, List<HistoryUpgradeRow> upgrades, List<HistoryShipmentRow> shipments)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        var doc = new Document(pdf, PageSize.A4.Rotate());
        doc.SetMargins(20, 20, 30, 20);

        var fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        doc.Add(new Paragraph("Equipment History Report").SetFont(fontBold).SetFontSize(13).SetFontColor(ColorDark).SetMarginBottom(2));
        doc.Add(new Paragraph($"UID: {eq.InternalUid}  |  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            .SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorMuted).SetMarginBottom(4));
        doc.Add(new Paragraph(
                $"Manufacturer: {eq.Manufacturer}   Model: {eq.Model}   S/N: {eq.SerialNumber}   " +
                $"CPU: {eq.CpuModel}   RAM: {eq.RamGb} GB   Storage: {eq.StorageGb} GB   Status: {eq.Status}")
            .SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorDark).SetMarginBottom(10));

        doc.Add(new Paragraph("1. Arrival").SetFont(fontBold).SetFontSize(10).SetFontColor(ColorDark).SetMarginBottom(4));
        AddTable(doc, fontNormal, fontBold, ArrivalHeaders, [ArrivalValues(eq)]);

        doc.Add(new Paragraph("2. Upgrades").SetFont(fontBold).SetFontSize(10).SetFontColor(ColorDark).SetMarginTop(12).SetMarginBottom(4));
        AddTable(doc, fontNormal, fontBold, UpgradeHeaders, upgrades.Select(UpgradeValues).ToList());
        var totalUpgrade = upgrades.Sum(u => u.CostUsd ?? 0);
        doc.Add(new Paragraph($"Total upgrade cost: $ {totalUpgrade:N2}").SetFont(fontBold).SetFontSize(8.5f).SetFontColor(ColorDark).SetMarginTop(3));

        doc.Add(new Paragraph("3. Shipments").SetFont(fontBold).SetFontSize(10).SetFontColor(ColorDark).SetMarginTop(12).SetMarginBottom(4));
        AddTable(doc, fontNormal, fontBold, ShipmentHeaders, shipments.Select(ShipmentValues).ToList());
        var totalSale = shipments.Sum(s => s.SalePriceUsd ?? 0);
        doc.Add(new Paragraph($"Total sale price: $ {totalSale:N2}").SetFont(fontBold).SetFontSize(8.5f).SetFontColor(ColorDark).SetMarginTop(3));

        doc.Close();
        return stream.ToArray();
    }

    private static void AddTable(Document doc, PdfFont fontNormal, PdfFont fontBold, string[] headers, List<string[]> rows)
    {
        var table = new Table(UnitValue.CreatePercentArray(headers.Length)).UseAllAvailableWidth();

        foreach (var header in headers)
        {
            table.AddHeaderCell(new Cell()
                .Add(new Paragraph(header).SetFont(fontBold).SetFontSize(7.5f).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(ColorGrayHeader)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetPadding(5)
                .SetBorder(Border.NO_BORDER));
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var bg = i % 2 == 1 ? ColorRowAlt : ColorConstants.WHITE;
            foreach (var val in rows[i])
            {
                table.AddCell(new Cell()
                    .Add(new Paragraph(val).SetFont(fontNormal).SetFontSize(7).SetFontColor(ColorDark))
                    .SetBackgroundColor(bg)
                    .SetPadding(4)
                    .SetBorder(Border.NO_BORDER));
            }
        }

        doc.Add(table);
    }

    public static byte[] GenerateExcel(Equipamento eq, List<HistoryUpgradeRow> upgrades, List<HistoryShipmentRow> shipments)
    {
        using var stream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("History");
            var row = 1;

            ws.Cell(row, 1).Value = "Equipment History Report";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 16;
            row++;

            ws.Cell(row, 1).Value = $"UID: {eq.InternalUid} | Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromArgb(107, 114, 128);
            row++;

            ws.Cell(row, 1).Value = $"Manufacturer: {eq.Manufacturer}   Model: {eq.Model}   S/N: {eq.SerialNumber}   CPU: {eq.CpuModel}   RAM: {eq.RamGb} GB   Storage: {eq.StorageGb} GB   Status: {eq.Status}";
            row += 2;

            ws.Cell(row, 1).Value = "1. Arrival";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            row = WriteTable(ws, row, ArrivalHeaders, [ArrivalValues(eq)]);
            row += 2;

            ws.Cell(row, 1).Value = "2. Upgrades";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            row = WriteTable(ws, row, UpgradeHeaders, upgrades.Select(UpgradeValues).ToList());
            var totalUpgrade = upgrades.Sum(u => u.CostUsd ?? 0);
            ws.Cell(row, 1).Value = $"Total upgrade cost: $ {totalUpgrade:N2}";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row += 2;

            ws.Cell(row, 1).Value = "3. Shipments";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            row = WriteTable(ws, row, ShipmentHeaders, shipments.Select(ShipmentValues).ToList());
            var totalSale = shipments.Sum(s => s.SalePriceUsd ?? 0);
            ws.Cell(row, 1).Value = $"Total sale price: $ {totalSale:N2}";
            ws.Cell(row, 1).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();

            wb.SaveAs(stream);
        }
        return stream.ToArray();
    }

    private static int WriteTable(IXLWorksheet ws, int startRow, string[] headers, List<string[]> rows)
    {
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(startRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(55, 65, 81);
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                ws.Cell(startRow + r + 1, c + 1).Value = rows[r][c];
            }
        }

        return startRow + rows.Count + 1;
    }
}

using ClosedXML.Excel;
using GBS_Web.Models;

namespace GBS_Web.Services;

// Port of ReportService.vb's GerarExcelUpgradesPorCliente — one sheet per
// customer (grouped by shipment recipient, "Unassigned" when none), same 9
// columns/order, same title/meta layout (header row 4, not 5, matching the
// desktop exactly). Excel-only: the desktop has no PDF version of this report.
public static class UpgradeClientReportGenerator
{
    private static readonly char[] InvalidSheetChars = ['/', '\\', '?', '*', '[', ']', ':'];

    private static readonly (string Header, double Width)[] Columns =
    [
        ("Internal UID", 16),
        ("Model", 16),
        ("Serial Number", 20),
        ("Date", 14),
        ("Component", 14),
        ("Before", 12),
        ("After", 12),
        ("Technician", 16),
        ("Notes", 40)
    ];

    private static string[] RowValues(UpgradeReportRow r) =>
    [
        r.InternalUid,
        r.Model ?? "",
        r.SerialNumber ?? "",
        r.DataUpgrade?.ToString("yyyy-MM-dd HH:mm") ?? "",
        r.ComponentType,
        r.ValueBefore ?? "",
        r.ValueAfter ?? "",
        r.Technician ?? "",
        r.Notes ?? ""
    ];

    private static string SanitizeSheetName(string customer)
    {
        var name = customer;
        foreach (var c in InvalidSheetChars)
        {
            name = name.Replace(c, '-');
        }
        return name.Length > 31 ? name[..31] : name;
    }

    public static byte[] GenerateExcel(List<UpgradeReportRow> rows)
    {
        var groups = rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Customer) ? "Unassigned" : r.Customer, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var stream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            foreach (var group in groups)
            {
                var ws = wb.Worksheets.Add(SanitizeSheetName(group.Key));
                const int headerRow = 4;
                var groupRows = group.ToList();

                ws.Cell(1, 4).Value = "Hardware Upgrade Report";
                ws.Cell(1, 4).Style.Font.Bold = true;
                ws.Cell(1, 4).Style.Font.FontSize = 14;
                ws.Range(1, 4, 1, 7).Merge();

                ws.Cell(2, 4).Value = $"Customer: {group.Key}   |   {groupRows.Count} upgrade(s)   |   Generated: {DateTime.Now:yyyy-MM-dd HH:mm}";
                ws.Cell(2, 4).Style.Font.FontSize = 9;
                ws.Cell(2, 4).Style.Font.FontColor = XLColor.FromArgb(107, 114, 128);
                ws.Range(2, 4, 2, 7).Merge();

                for (var c = 0; c < Columns.Length; c++)
                {
                    var cell = ws.Cell(headerRow, c + 1);
                    cell.Value = Columns[c].Header;
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(55, 65, 81);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (var r = 0; r < groupRows.Count; r++)
                {
                    var values = RowValues(groupRows[r]);
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
                    ws.Column(c + 1).Width = Columns[c].Width;
                }
                ws.Column(Columns.Length).Style.Alignment.WrapText = true;

                if (groupRows.Count > 0)
                {
                    ws.Range(headerRow, 1, headerRow + groupRows.Count, Columns.Length).SetAutoFilter();
                }
                ws.SheetView.FreezeRows(headerRow);
            }

            wb.SaveAs(stream);
        }
        return stream.ToArray();
    }
}

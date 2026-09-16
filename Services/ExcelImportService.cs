using ClosedXML.Excel;
using GBS_Web.Models;

namespace GBS_Web.Services;

public class ExcelImportResult
{
    public List<Equipamento> Equipamentos { get; set; } = new();
    public int TotalLinhasLidas { get; set; }
    public int TotalAbasProcessadas { get; set; }
    public List<string> Erros { get; set; } = new();
}

// Port of clsImportacaoExcel — generic reader that walks every worksheet in the
// workbook (no per-sheet special-casing in the original either) and maps rows
// to Equipamento by header name.
public static class ExcelImportService
{
    private static readonly string[] BatteryColumns =
    {
        "Battery Condition", "Battery", "Battery Status", "Battery Health", "Bateria", "Condicao Bateria"
    };

    public static ExcelImportResult ReadWorkbook(Stream stream)
    {
        var result = new ExcelImportResult();

        using var workbook = new XLWorkbook(stream);
        foreach (var ws in workbook.Worksheets)
        {
            try
            {
                var usedRange = ws.RangeUsed();
                if (usedRange is null) continue;

                var headerRow = usedRange.FirstRow();
                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.Cells())
                {
                    var name = cell.GetString().Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (!headers.ContainsKey(name))
                    {
                        headers[name] = cell.Address.ColumnNumber;
                    }
                }

                var rowsProcessedForSheet = 0;
                foreach (var row in usedRange.RowsUsed().Skip(1))
                {
                    var equipamento = MapRow(row, headers, ws.Name);
                    if (equipamento is not null)
                    {
                        result.Equipamentos.Add(equipamento);
                        result.TotalLinhasLidas++;
                        rowsProcessedForSheet++;
                    }
                }

                if (rowsProcessedForSheet > 0 || usedRange.RowCount() > 1)
                {
                    result.TotalAbasProcessadas++;
                }
            }
            catch (Exception ex)
            {
                result.Erros.Add($"Erro na aba '{ws.Name}': {ex.Message}");
            }
        }

        return result;
    }

    private static Equipamento? MapRow(IXLRangeRow row, Dictionary<string, int> headers, string sheetName)
    {
        var uid = ReadCell(row, headers, "Internal UID");
        var serial = ReadCell(row, headers, "Serial");
        var mfr = ReadCell(row, headers, "Manufacturer");
        var model = ReadCell(row, headers, "Model");
        var battery = ReadFirstCell(row, headers, BatteryColumns);

        if (string.IsNullOrWhiteSpace(uid) && string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }
        if (string.Equals(uid.Trim(), "INTERNAL UID", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new Equipamento
        {
            InternalUid = uid.Trim(),
            SerialNumber = serial.Trim().ToUpperInvariant(),
            Manufacturer = mfr.Trim().ToUpperInvariant(),
            Model = model.Trim().ToUpperInvariant(),
            CpuFamily = ReadCell(row, headers, "CPU Family").Trim(),
            CpuModel = ReadCell(row, headers, "CPU Model").Trim(),
            CpuSpeedGhz = ParseDecimal(ReadCell(row, headers, "CPU Speed")),
            StorageGb = ParseGb(ReadCell(row, headers, "HDD Size")),
            RamGb = ParseGb(ReadCell(row, headers, "Memory(last#total)")),
            HardDriveType = ReadCell(row, headers, "Hard Drive Type").Trim(),
            Resolution = ReadCell(row, headers, "Resolution").Trim(),
            Graphics = ReadCell(row, headers, "Graphics").Trim(),
            ConditionStatus = NormalizeCondition(battery),
            BatteryCheck = battery.Trim(),
            Notes = ReadCell(row, headers, "Notes").Trim(),
            SourceBatch = sheetName,
            DeviceType = "LAPTOP",
            Status = "IN_STOCK",
            IdEmpresa = 1
        };
    }

    private static string ReadCell(IXLRangeRow row, Dictionary<string, int> headers, string columnName)
    {
        if (!headers.TryGetValue(columnName, out var col)) return "";
        return row.Worksheet.Cell(row.RowNumber(), col).GetString() ?? "";
    }

    private static string ReadFirstCell(IXLRangeRow row, Dictionary<string, int> headers, string[] columnNames)
    {
        foreach (var name in columnNames)
        {
            var value = ReadCell(row, headers, name).Trim();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return "";
    }

    private static decimal? ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.ToUpperInvariant().Replace("GHZ", "").Replace("GB", "").Replace("MHZ", "").Trim();
        return decimal.TryParse(cleaned.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static int? ParseGb(string value)
    {
        var d = ParseDecimal(value);
        return d.HasValue ? (int)d.Value : null;
    }

    private static string NormalizeCondition(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "GOOD";

        return value.Trim().ToUpperInvariant() switch
        {
            "EXCELLENT" or "E" => "EXCELLENT",
            "GOOD" or "G" or "OK" or "NORMAL" => "GOOD",
            "FAIR" or "F" or "AVG" or "AVERAGE" => "FAIR",
            "POOR" or "P" or "BAD" or "REPLACE" or "REPLACED" or "FAIL" or "FAILED" => "POOR",
            _ => "GOOD"
        };
    }
}

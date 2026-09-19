namespace GBS_Web.Models;

public class Upgrade
{
    public int IdUpgrade { get; set; }
    public int IdEquipamento { get; set; }
    public string? InternalUid { get; set; }

    // RAM, SSD, HDD, BATTERY, SCREEN, KEYBOARD, COVER, GPU, OTHER (TIPO_UPGRADE)
    public string ComponentType { get; set; } = "RAM";

    public string? ValueBefore { get; set; }
    public string? ValueAfter { get; set; }

    // PART_SERIAL/SOURCE_ORIGEM/COST_USD have no dedicated columns in the live table —
    // PACK_UPGRADE's SELECT procs hardcode them as NULL. The desktop app instead folds
    // them into the free-text OBSERVACAO/Notes blob on save (MontarObservacao). Kept
    // here as real input fields; ported the same way.
    public string? PartSerial { get; set; }
    public string SourceOrigem { get; set; } = "NEW_PURCHASE";
    public decimal? CostUsd { get; set; }

    public string? Technician { get; set; }
    public string? Notes { get; set; }
    public DateTime? DataUpgrade { get; set; }

    public int? IdComponent { get; set; }
    public string ActionType { get; set; } = "MANUAL";
    public string? CompNewStatus { get; set; }
}

// Port of clsLeituraUpgrade.selecionarUpgradesComCliente's DataRow shape — used
// only by the per-client upgrade report (ReportService.vb GerarExcelUpgradesPorCliente).
public class UpgradeReportRow
{
    public string Customer { get; set; } = "Unassigned";
    public string InternalUid { get; set; } = "";
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? DataUpgrade { get; set; }
    public string ComponentType { get; set; } = "";
    public string? ValueBefore { get; set; }
    public string? ValueAfter { get; set; }
    public string? Technician { get; set; }
    public string? Notes { get; set; }
}

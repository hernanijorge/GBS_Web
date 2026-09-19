namespace GBS_Web.Models;

// Port of clsLeituraHistorico.vb's selecionarUpgradesEquipamento — SOURCE_ORIGEM,
// COST_USD and PART_SERIAL are always null: TBL_EQUIPAMENTO_UPGRADE has no columns
// for them, PROC_INSERT_UPGRADE folds them into the free-text OBSERVACAO/Notes blob
// instead (see UpgradeRepository.MontarObservacao). Same limitation as the desktop.
public class HistoryUpgradeRow
{
    public DateTime? DataUpgrade { get; set; }
    public string ComponentType { get; set; } = "";
    public string? ValueBefore { get; set; }
    public string? ValueAfter { get; set; }
    public string? SourceOrigem { get; set; }
    public decimal? CostUsd { get; set; }
    public string? PartSerial { get; set; }
    public string? Technician { get; set; }
    public string? Notes { get; set; }
}

// Port of clsLeituraHistorico.vb's selecionarShipmentEquipamento. ShippingCostUsd
// and ConditionAtShip are always null: TBL_REMESSA/TBL_REMESSA_ITEM have no such
// columns (confirmed by Remessa.ShippingCostUsd / ItemRemessa.ConditionAtShip).
public class HistoryShipmentRow
{
    public string RemessaRef { get; set; } = "";
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? RecipientName { get; set; }
    public DateTime? DataEnvio { get; set; }
    public DateTime? DataEntrega { get; set; }
    public string? StatusRemessa { get; set; }
    public decimal? ShippingCostUsd { get; set; }
    public decimal? SalePriceUsd { get; set; }
    public string? ConditionAtShip { get; set; }
    public string? ItemNotes { get; set; }
}

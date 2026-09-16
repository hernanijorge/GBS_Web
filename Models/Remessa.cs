namespace GBS_Web.Models;

public class Remessa
{
    public int IdRemessa { get; set; }
    public string RemessaRef { get; set; } = "";     // CODIGO_REMESSA
    public string Carrier { get; set; } = "OTHER";
    public string? TrackingNumber { get; set; }
    public string RecipientName { get; set; } = "";  // DESTINATARIO
    public string StatusRemessa { get; set; } = "LABEL_CREATED";
    public DateTime? DataEnvio { get; set; }
    public DateTime? DataEntrega { get; set; }
    public string? Notes { get; set; }                // OBSERVACAO
    public DateTime? DataCadastro { get; set; }
    public int TotalItens { get; set; }

    // Collected in frmRemessa's "New Shipment" form, but TBL_REMESSA has no backing
    // column for any of these — the desktop app silently discards them on save.
    // Kept here for model parity; never sent to the database.
    public string Direction { get; set; } = "OUTBOUND";
    public string? SenderName { get; set; }
    public string? SenderAddress { get; set; }
    public string? RecipientAddress { get; set; }
    public decimal? WeightLbs { get; set; }
    public decimal? ShippingCostUsd { get; set; }
    public decimal? InsuranceUsd { get; set; }
    public string? ServiceLevel { get; set; }
    public DateTime? EstimatedDelivery { get; set; }
    public DateTime? ActualDelivery { get; set; }

    public string UrlRastreio
    {
        get
        {
            if (string.IsNullOrEmpty(TrackingNumber)) return "";
            return Carrier.ToUpperInvariant() switch
            {
                "FEDEX" => $"https://www.fedex.com/fedextrack/?trknbr={TrackingNumber}",
                "UPS" => $"https://www.ups.com/track?tracknum={TrackingNumber}",
                "USPS" => $"https://tools.usps.com/go/TrackConfirmAction?tLabels={TrackingNumber}",
                "DHL" => $"https://www.dhl.com/en/express/tracking.html?AWB={TrackingNumber}",
                _ => ""
            };
        }
    }
}

public class ItemRemessa
{
    public int IdRemessaItem { get; set; }
    public int IdRemessa { get; set; }
    public int IdEquipamento { get; set; }
    public string InternalUid { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? CpuModel { get; set; }
    public int? RamGb { get; set; }
    public int? StorageGb { get; set; }

    // Collected in the desktop form but TBL_REMESSA_ITEM has no CONDITION_AT_SHIP
    // column — never persisted, display-only before save.
    public string ConditionAtShip { get; set; } = "GOOD";

    public string StatusItem { get; set; } = "PENDING";
    public decimal? SalePriceUsd { get; set; }
    public string? Notes { get; set; }
    public DateTime? DataCadastro { get; set; }
}

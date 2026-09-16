namespace GBS_Web.Models;

public class Equipamento
{
    public int IdEquipamento { get; set; }
    public string InternalUid { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string? SerialNumber { get; set; }

    // CpuFamily, CpuSpeedGhz, HardDriveType, Resolution, Graphics, DeviceType, IdEmpresa,
    // IdUsuarioCadastro: real TBL_EQUIPAMENTO columns, but PACK_EQUIPAMENTO never reads or
    // writes them (not in any PROC_SELECT_* cursor, not in PROC_INSERT/PROC_UPSERT_EQUIPAMENTO).
    // Kept here for model parity; always null/default until the package is extended.
    public string? CpuFamily { get; set; }
    public decimal? CpuSpeedGhz { get; set; }
    public string? HardDriveType { get; set; }
    public string? Resolution { get; set; }
    public string? Graphics { get; set; }
    public string DeviceType { get; set; } = "LAPTOP";
    public int IdEmpresa { get; set; } = 1;
    public int? IdUsuarioCadastro { get; set; }

    public string? CpuModel { get; set; }   // PROCESSADOR column
    public int? RamGb { get; set; }
    public int? StorageGb { get; set; }
    public string ConditionStatus { get; set; } = "GOOD";

    // Write-only via PROC_INSERT — no PROC_SELECT_* cursor returns it back.
    public string? BatteryCheck { get; set; }

    public string Status { get; set; } = "IN_STOCK";
    public string? StatusDescricao { get; set; }
    public string? Notes { get; set; }
    public string? SourceBatch { get; set; }
    public DateTime? DataCadastro { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public int? TotalUpgrades { get; set; }

    public string DescricaoCompleta
    {
        get
        {
            var desc = $"{Manufacturer} {Model}";
            if (!string.IsNullOrEmpty(CpuModel)) desc += " · " + CpuModel;
            if (RamGb is > 0) desc += $" · {RamGb} GB RAM";
            if (StorageGb is > 0) desc += $" · {StorageGb} GB Storage";
            return desc;
        }
    }

    public bool CondicaoBoa => ConditionStatus is "EXCELLENT" or "GOOD";
}

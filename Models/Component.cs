namespace GBS_Web.Models;

public class Component
{
    public int IdComponent { get; set; }
    public string InternalUid { get; set; } = "";
    public string ComponentType { get; set; } = "";   // RAM, SSD, HDD, MINI_DESKTOP
    public int CapacityGb { get; set; }
    public int? SpeedMhz { get; set; }                // RAM only
    public string? Generation { get; set; }           // DDR4, DDR5, NVMe, SATA
    public string? Brand { get; set; }
    public string? PartNumber { get; set; }
    public string? Cpu { get; set; }                  // MINI_DESKTOP only
    public int? StorageGb { get; set; }                // MINI_DESKTOP only
    public string ConditionStatus { get; set; } = "GOOD";
    public string Status { get; set; } = "IN_STOCK";
    public string? SourceBatch { get; set; }
    public string? Notes { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }

    public string DisplayLabel
    {
        get
        {
            var parts = new[]
            {
                ComponentType,
                CapacityGb > 0 ? $"{CapacityGb} GB" : "",
                Generation ?? "",
                SpeedMhz is > 0 ? $"{SpeedMhz} MHz" : "",
                Cpu ?? "",
                StorageGb is > 0 ? $"{StorageGb} GB Storage" : ""
            };
            return string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)));
        }
    }
}

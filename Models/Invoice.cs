namespace GBS_Web.Models;

public class Invoice
{
    public int IdInvoice { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public int IdCliente { get; set; }
    public string? ClienteNome { get; set; }
    public int? IdRemessa { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.Today;
    public DateTime? DueDate { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal SubtotalUsd { get; set; }
    public decimal DiscountUsd { get; set; }
    public decimal ShippingUsd { get; set; }
    public decimal TaxUsd { get; set; }
    public decimal TotalUsd { get; set; }
    public string StatusInvoice { get; set; } = "DRAFT";
    public string? ClientEmail { get; set; }
    public string? Notes { get; set; }
    public DateTime? DataCadastro { get; set; }
}

public class InvoiceItem
{
    public int IdInvoiceItem { get; set; }
    public int IdInvoice { get; set; }
    public int? IdEquipamento { get; set; }
    public string? InternalUid { get; set; }
    public string Description { get; set; } = "";
    public decimal Qty { get; set; } = 1;
    public decimal UnitPriceUsd { get; set; }
    public decimal LineTotalUsd { get; set; }
    public string? Notes { get; set; }

    public void RecalcularTotal() => LineTotalUsd = Qty * UnitPriceUsd;
}

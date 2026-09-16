using GBS_Web.Data;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Event;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace GBS_Web.Services;

public static class InvoicePdfGenerator
{
    private static readonly DeviceRgb ColorGrayHeader = new(55, 65, 81);
    private static readonly DeviceRgb ColorRowAlt = new(249, 250, 251);
    private static readonly DeviceRgb ColorDark = new(31, 41, 55);
    private static readonly DeviceRgb ColorLabel = new(75, 85, 99);
    private static readonly DeviceRgb ColorMuted = new(107, 114, 128);

    public static byte[] Generate(InvoiceDocumentData data, string? logoPath)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        var doc = new Document(pdf, PageSize.A4);
        doc.SetMargins(40, 40, 60, 40);

        var fontNormal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        pdf.AddEventHandler(PdfDocumentEvent.END_PAGE, new FooterEventHandler(data.Items.Count, fontNormal));

        var invoice = data.Invoice;
        var cliente = data.Cliente;

        // Header: logo/title + "INVOICE"
        var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 45, 55 })).UseAllAvailableWidth();

        var logoCell = new Cell().SetBorder(Border.NO_BORDER).SetVerticalAlignment(VerticalAlignment.MIDDLE);
        var effectiveLogo = ResolveLogoPath(logoPath);
        if (effectiveLogo is not null)
        {
            try
            {
                var img = new Image(iText.IO.Image.ImageDataFactory.Create(effectiveLogo));
                img.ScaleToFit(130, 50);
                logoCell.Add(img);
            }
            catch
            {
                logoCell.Add(new Paragraph("Global Business Solution").SetFont(fontBold).SetFontSize(14).SetFontColor(ColorDark));
            }
        }
        else
        {
            logoCell.Add(new Paragraph("Global Business Solution").SetFont(fontBold).SetFontSize(14).SetFontColor(ColorDark));
        }
        headerTable.AddCell(logoCell);

        var titleCell = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
        titleCell.Add(new Paragraph("Global Business Solution").SetFont(fontBold).SetFontSize(14).SetFontColor(ColorDark).SetTextAlignment(TextAlignment.RIGHT));
        titleCell.Add(new Paragraph("INVOICE").SetFont(fontNormal).SetFontSize(11).SetFontColor(ColorDark).SetTextAlignment(TextAlignment.RIGHT));
        headerTable.AddCell(titleCell);

        doc.Add(headerTable);
        doc.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(0.5f)).SetMarginTop(4).SetMarginBottom(10));

        // Invoice info + Bill To
        var infoTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).UseAllAvailableWidth();

        var infoCell = new Cell().SetBorder(Border.NO_BORDER);
        infoCell.Add(InfoLine("Invoice #:", invoice.InvoiceNumber, fontBold, fontNormal));
        infoCell.Add(InfoLine("Issue Date:", invoice.IssueDate.ToString("yyyy-MM-dd"), fontBold, fontNormal));
        infoCell.Add(InfoLine("Due Date:", invoice.DueDate?.ToString("yyyy-MM-dd") ?? "-", fontBold, fontNormal));
        infoTable.AddCell(infoCell);

        var billToCell = new Cell().SetBorder(Border.NO_BORDER);
        billToCell.Add(new Paragraph("Bill To").SetFont(fontBold).SetFontSize(9).SetMarginBottom(2));
        billToCell.Add(new Paragraph(cliente.NomeRazao).SetFont(fontNormal).SetFontSize(9).SetMarginBottom(1));
        if (!string.IsNullOrWhiteSpace(cliente.Documento))
            billToCell.Add(new Paragraph(cliente.Documento).SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorLabel).SetMarginBottom(1));
        if (!string.IsNullOrWhiteSpace(cliente.Email))
            billToCell.Add(new Paragraph(cliente.Email).SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorLabel).SetMarginBottom(1));
        var addr1 = $"{cliente.Endereco1} {cliente.Endereco2}".Trim();
        if (!string.IsNullOrWhiteSpace(addr1))
            billToCell.Add(new Paragraph(addr1).SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorLabel).SetMarginBottom(1));
        var addr2 = $"{cliente.Cidade} - {cliente.Estado} {cliente.ZipCode} / {cliente.Pais}".Trim();
        if (!string.IsNullOrWhiteSpace(cliente.Cidade))
            billToCell.Add(new Paragraph(addr2).SetFont(fontNormal).SetFontSize(8).SetFontColor(ColorLabel));
        infoTable.AddCell(billToCell);

        doc.Add(infoTable.SetMarginBottom(12));

        // Items table
        var itemsTable = new Table(UnitValue.CreatePercentArray(new float[] { 55, 10, 17.5f, 17.5f })).UseAllAvailableWidth();
        foreach (var (text, alignRight) in new[] { ("Description", false), ("Qty", true), ("Unit (USD)", true), ("Total (USD)", true) })
        {
            var cell = new Cell()
                .Add(new Paragraph(text).SetFont(fontBold).SetFontSize(9).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(ColorGrayHeader)
                .SetPadding(5)
                .SetTextAlignment(alignRight ? TextAlignment.RIGHT : TextAlignment.LEFT)
                .SetBorder(Border.NO_BORDER);
            itemsTable.AddHeaderCell(cell);
        }

        decimal subtotal = 0;
        var rowIndex = 0;
        foreach (var item in data.Items)
        {
            item.RecalcularTotal();
            subtotal += item.LineTotalUsd;
            var bg = rowIndex % 2 == 0 ? ColorConstants.WHITE : ColorRowAlt;

            itemsTable.AddCell(BodyCell(item.Description, fontNormal, bg, TextAlignment.LEFT));
            itemsTable.AddCell(BodyCell(item.Qty.ToString("0.##"), fontNormal, bg, TextAlignment.RIGHT));
            itemsTable.AddCell(BodyCell(item.UnitPriceUsd.ToString("0.00"), fontNormal, bg, TextAlignment.RIGHT));
            itemsTable.AddCell(BodyCell(item.LineTotalUsd.ToString("0.00"), fontNormal, bg, TextAlignment.RIGHT));
            rowIndex++;
        }
        doc.Add(itemsTable.SetMarginBottom(14));

        // Totals
        var totalFinal = subtotal - invoice.DiscountUsd + invoice.ShippingUsd + invoice.TaxUsd;
        var totalsTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 })).SetWidth(260).SetHorizontalAlignment(HorizontalAlignment.RIGHT);

        AddTotalRow(totalsTable, "Subtotal", subtotal.ToString("0.00"), fontNormal, false);
        AddTotalRow(totalsTable, "Discount", "-" + invoice.DiscountUsd.ToString("0.00"), fontNormal, false);
        AddTotalRow(totalsTable, "Shipping", invoice.ShippingUsd.ToString("0.00"), fontNormal, false);
        AddTotalRow(totalsTable, "Tax", invoice.TaxUsd.ToString("0.00"), fontNormal, false);
        AddTotalRow(totalsTable, "TOTAL USD", totalFinal.ToString("0.00"), fontBold, true);

        doc.Add(totalsTable);

        if (!string.IsNullOrWhiteSpace(invoice.Notes))
        {
            doc.Add(new Paragraph("Notes").SetFont(fontBold).SetFontSize(9).SetMarginTop(10).SetMarginBottom(4));
            doc.Add(new Paragraph(invoice.Notes).SetFont(fontNormal).SetFontSize(9));
        }

        doc.Close();
        return stream.ToArray();
    }

    private static string? ResolveLogoPath(string? logoPath)
    {
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath)) return logoPath;
        foreach (var candidate in new[] { @"C:\GBS\logo.png", @"C:\GBS\logo.jpg", @"C:\GBS\logo.jpeg" })
        {
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static Paragraph InfoLine(string label, string value, PdfFont fontLabel, PdfFont fontValue)
    {
        var p = new Paragraph().SetMarginBottom(2);
        p.Add(new Text(label + " ").SetFont(fontLabel).SetFontSize(9));
        p.Add(new Text(value).SetFont(fontValue).SetFontSize(9));
        return p;
    }

    private static Cell BodyCell(string text, PdfFont font, Color bg, TextAlignment align)
    {
        return new Cell()
            .Add(new Paragraph(text).SetFont(font).SetFontSize(9))
            .SetBackgroundColor(bg)
            .SetPadding(4)
            .SetTextAlignment(align)
            .SetBorder(Border.NO_BORDER);
    }

    private static void AddTotalRow(Table table, string label, string value, PdfFont font, bool highlight)
    {
        var border = highlight ? new SolidBorder(0.75f) : Border.NO_BORDER;
        table.AddCell(new Cell().Add(new Paragraph(label).SetFont(font).SetFontSize(highlight ? 10 : 9))
            .SetTextAlignment(TextAlignment.LEFT).SetPadding(4).SetBorder(Border.NO_BORDER).SetBorderTop(border));
        table.AddCell(new Cell().Add(new Paragraph(value).SetFont(font).SetFontSize(highlight ? 10 : 9))
            .SetTextAlignment(TextAlignment.RIGHT).SetPadding(4).SetBorder(Border.NO_BORDER).SetBorderTop(border));
    }

    private class FooterEventHandler(int itemCount, PdfFont font) : AbstractPdfDocumentEventHandler
    {
        protected override void OnAcceptedEvent(AbstractPdfDocumentEvent @event)
        {
            var docEvent = (PdfDocumentEvent)@event;
            var page = docEvent.GetPage();
            var pageSize = page.GetPageSize();
            var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);

            var text = $"Items: {itemCount} | Generated: {DateTime.Now:yyyy-MM-dd HH:mm}";
            var x = (pageSize.GetLeft() + pageSize.GetRight()) / 2;
            var y = pageSize.GetBottom() + 20;

            canvas.BeginText()
                .SetFontAndSize(font, 8)
                .SetColor(ColorMuted, true)
                .MoveText(x - text.Length * 1.7f, y)
                .ShowText(text)
                .EndText();
            canvas.Release();
        }
    }
}

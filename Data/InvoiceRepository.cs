using System.Data;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace GBS_Web.Data;

public class InvoiceDocumentData
{
    public Models.Invoice Invoice { get; set; } = new();
    public Models.Cliente Cliente { get; set; } = new();
    public List<Models.InvoiceItem> Items { get; set; } = new();
}

public class InvoiceRepository
{
    private readonly string _connectionString;

    public InvoiceRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    public async Task<List<Models.Invoice>> GetAllAsync(int? idCliente = null, string? status = null)
    {
        var list = new List<Models.Invoice>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_INVOICE.PROC_SELECT_INVOICES";
        command.BindByName = true;
        AddParam(command, "V_ID_CLIENTE", OracleDbType.Int32, idCliente);
        AddParam(command, "V_STATUS", OracleDbType.Varchar2, status);
        AddParam(command, "V_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.Invoice
            {
                IdInvoice = Convert.ToInt32(reader["ID_INVOICE"]),
                InvoiceNumber = reader["INVOICE_NUMBER"] as string ?? "",
                IdCliente = Convert.ToInt32(reader["ID_CLIENTE"]),
                ClienteNome = reader["CLIENTE_NOME"] as string,
                IdRemessa = reader["ID_REMESSA"] is DBNull ? null : Convert.ToInt32(reader["ID_REMESSA"]),
                IssueDate = Convert.ToDateTime(reader["ISSUE_DATE"]),
                DueDate = reader["DUE_DATE"] is DBNull ? null : Convert.ToDateTime(reader["DUE_DATE"]),
                SubtotalUsd = Convert.ToDecimal(reader["SUBTOTAL_USD"]),
                DiscountUsd = Convert.ToDecimal(reader["DISCOUNT_USD"]),
                ShippingUsd = Convert.ToDecimal(reader["SHIPPING_USD"]),
                TaxUsd = Convert.ToDecimal(reader["TAX_USD"]),
                TotalUsd = Convert.ToDecimal(reader["TOTAL_USD"]),
                StatusInvoice = reader["STATUS_INVOICE"] as string ?? "DRAFT",
                ClientEmail = reader["CLIENT_EMAIL"] as string,
                DataCadastro = reader["DATA_CADASTRO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_CADASTRO"])
            });
        }

        return list;
    }

    public async Task<List<Models.InvoiceItem>> GetItemsAsync(int idInvoice)
    {
        var list = new List<Models.InvoiceItem>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_INVOICE.PROC_SELECT_ITENS";
        command.BindByName = true;
        AddParam(command, "V_ID_INVOICE", OracleDbType.Int32, idInvoice);
        AddParam(command, "V_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapItem(reader));
        }

        return list;
    }

    // Port of InvoiceController.incluirComItens: header insert, then each item, then
    // recalc total — all via PACK_INVOICE, same three-step transaction as the desktop.
    public async Task<(int IdInvoice, string InvoiceNumber)> CreateWithItemsAsync(Models.Invoice invoice, List<Models.InvoiceItem> items)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            int idInvoice;
            string invoiceNumber;

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "PACK_INVOICE.PROC_INSERT_INVOICE";
                command.BindByName = true;

                AddParam(command, "V_INVOICE_NUMBER", OracleDbType.Varchar2, null);
                AddParam(command, "V_ID_CLIENTE", OracleDbType.Int32, invoice.IdCliente);
                AddParam(command, "V_ID_REMESSA", OracleDbType.Int32, invoice.IdRemessa);
                AddParam(command, "V_ISSUE_DATE", OracleDbType.Date, invoice.IssueDate);
                AddParam(command, "V_DUE_DATE", OracleDbType.Date, invoice.DueDate);
                AddParam(command, "V_CURRENCY", OracleDbType.Varchar2, invoice.Currency);
                AddParam(command, "V_DISCOUNT_USD", OracleDbType.Decimal, invoice.DiscountUsd);
                AddParam(command, "V_SHIPPING_USD", OracleDbType.Decimal, invoice.ShippingUsd);
                AddParam(command, "V_TAX_USD", OracleDbType.Decimal, invoice.TaxUsd);
                AddParam(command, "V_CLIENT_EMAIL", OracleDbType.Varchar2, invoice.ClientEmail);
                AddParam(command, "V_NOTES", OracleDbType.Varchar2, invoice.Notes);
                var idOut = AddParam(command, "V_ID", OracleDbType.Int32, null, ParameterDirection.Output);
                var numberOut = AddParam(command, "V_NUMBER_OUT", OracleDbType.Varchar2, null, ParameterDirection.Output);
                numberOut.Size = 40;

                await command.ExecuteNonQueryAsync();

                idInvoice = Convert.ToInt32(((OracleDecimal)idOut.Value).Value);
                invoiceNumber = numberOut.Value?.ToString() ?? "";
            }

            foreach (var item in items)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "PACK_INVOICE.PROC_INSERT_ITEM";
                command.BindByName = true;

                AddParam(command, "V_ID_INVOICE", OracleDbType.Int32, idInvoice);
                AddParam(command, "V_ID_EQUIPAMENTO", OracleDbType.Int32, item.IdEquipamento);
                AddParam(command, "V_DESCRIPTION", OracleDbType.Varchar2, item.Description);
                AddParam(command, "V_QTY", OracleDbType.Decimal, item.Qty);
                AddParam(command, "V_UNIT_PRICE_USD", OracleDbType.Decimal, item.UnitPriceUsd);
                AddParam(command, "V_NOTES", OracleDbType.Varchar2, item.Notes);

                await command.ExecuteNonQueryAsync();
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "PACK_INVOICE.PROC_RECALC_TOTAL";
                command.BindByName = true;
                AddParam(command, "V_ID_INVOICE", OracleDbType.Int32, idInvoice);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return (idInvoice, invoiceNumber);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateStatusAsync(int idInvoice, string status)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_INVOICE.PROC_UPDATE_STATUS";
        command.BindByName = true;
        AddParam(command, "V_ID", OracleDbType.Int32, idInvoice);
        AddParam(command, "V_STATUS", OracleDbType.Varchar2, status);

        await command.ExecuteNonQueryAsync();
    }

    // Port of clsLeituraInvoice.selecionarDadosDocumento — everything PDF generation needs.
    public async Task<InvoiceDocumentData?> GetDocumentDataAsync(int idInvoice)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_INVOICE.PROC_SELECT_DADOS_DOCUMENTO";
        command.BindByName = true;
        AddParam(command, "V_ID_INVOICE", OracleDbType.Int32, idInvoice);
        AddParam(command, "V_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var data = new InvoiceDocumentData
        {
            Invoice = new Models.Invoice
            {
                IdInvoice = Convert.ToInt32(reader["ID_INVOICE"]),
                InvoiceNumber = reader["INVOICE_NUMBER"] as string ?? "",
                IdRemessa = reader["ID_REMESSA"] is DBNull ? null : Convert.ToInt32(reader["ID_REMESSA"]),
                IssueDate = Convert.ToDateTime(reader["ISSUE_DATE"]),
                DueDate = reader["DUE_DATE"] is DBNull ? null : Convert.ToDateTime(reader["DUE_DATE"]),
                Currency = reader["CURRENCY"] as string ?? "USD",
                SubtotalUsd = Convert.ToDecimal(reader["SUBTOTAL_USD"]),
                DiscountUsd = Convert.ToDecimal(reader["DISCOUNT_USD"]),
                ShippingUsd = Convert.ToDecimal(reader["SHIPPING_USD"]),
                TaxUsd = Convert.ToDecimal(reader["TAX_USD"]),
                TotalUsd = Convert.ToDecimal(reader["TOTAL_USD"]),
                StatusInvoice = reader["STATUS_INVOICE"] as string ?? "DRAFT",
                ClientEmail = reader["EMAIL_CLIENTE"] as string,
                Notes = reader["NOTES"] as string
            },
            Cliente = new Models.Cliente
            {
                IdCliente = Convert.ToInt32(reader["ID_CLIENTE"]),
                NomeRazao = reader["NOME_RAZAO"] as string ?? "",
                NomeFantasia = reader["NOME_FANTASIA"] as string,
                Documento = reader["DOCUMENTO"] as string,
                Email = reader["EMAIL_CLIENTE"] as string,
                Telefone = reader["TELEFONE"] as string,
                Endereco1 = reader["ENDERECO1"] as string,
                Endereco2 = reader["ENDERECO2"] as string,
                Cidade = reader["CIDADE"] as string,
                Estado = reader["ESTADO"] as string,
                ZipCode = reader["ZIP_CODE"] as string,
                Pais = reader["PAIS"] as string ?? "USA"
            }
        };
        data.Invoice.IdCliente = data.Cliente.IdCliente;

        data.Items = await GetItemsAsync(idInvoice);
        return data;
    }

    private static Models.InvoiceItem MapItem(OracleDataReader reader)
    {
        return new Models.InvoiceItem
        {
            IdInvoiceItem = Convert.ToInt32(reader["ID_INVOICE_ITEM"]),
            IdInvoice = Convert.ToInt32(reader["ID_INVOICE"]),
            IdEquipamento = reader["ID_EQUIPAMENTO"] is DBNull ? null : Convert.ToInt32(reader["ID_EQUIPAMENTO"]),
            InternalUid = reader["INTERNAL_UID"] as string,
            Description = reader["DESCRIPTION"] as string ?? "",
            Qty = Convert.ToDecimal(reader["QTY"]),
            UnitPriceUsd = Convert.ToDecimal(reader["UNIT_PRICE_USD"]),
            LineTotalUsd = Convert.ToDecimal(reader["LINE_TOTAL_USD"]),
            Notes = reader["NOTES"] as string
        };
    }

    private static OracleParameter AddParam(
        OracleCommand command, string name, OracleDbType type, object? value,
        ParameterDirection direction = ParameterDirection.Input)
    {
        var parameter = new OracleParameter(name, type) { Direction = direction, Value = value ?? DBNull.Value };
        command.Parameters.Add(parameter);
        return parameter;
    }
}

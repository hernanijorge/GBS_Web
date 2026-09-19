using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace GBS_Web.Data;

public class RemessaRepository
{
    private readonly string _connectionString;

    public RemessaRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    public async Task<List<Models.Remessa>> GetActiveShipmentsAsync()
    {
        var list = new List<Models.Remessa>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_REMESSA.PROC_SELECT_REMESSAS";
        command.BindByName = true;
        AddParam(command, "P_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.Remessa
            {
                IdRemessa = Convert.ToInt32(reader["ID_REMESSA"]),
                RemessaRef = reader["REMESSA_REF"] as string ?? "",
                Carrier = reader["CARRIER"] as string ?? "OTHER",
                TrackingNumber = reader["TRACKING_NUMBER"] as string,
                RecipientName = reader["DESTINATARIO"] as string ?? "",
                StatusRemessa = reader["STATUS_REMESSA"] as string ?? "LABEL_CREATED",
                DataEnvio = reader["DATA_ENVIO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_ENVIO"]),
                DataEntrega = reader["DATA_ENTREGA"] is DBNull ? null : Convert.ToDateTime(reader["DATA_ENTREGA"]),
                Notes = reader["OBSERVACAO"] as string,
                DataCadastro = reader["DATA_CADASTRO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_CADASTRO"]),
                TotalItens = Convert.ToInt32(reader["TOTAL_ITENS"])
            });
        }

        return list;
    }

    public async Task<List<Models.ItemRemessa>> GetItemsAsync(string remessaRef)
    {
        var list = new List<Models.ItemRemessa>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_REMESSA.PROC_SELECT_ITENS";
        command.BindByName = true;
        AddParam(command, "P_CODIGO_REMESSA", OracleDbType.Varchar2, remessaRef);
        AddParam(command, "P_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.ItemRemessa
            {
                InternalUid = reader["INTERNAL_UID"] as string ?? "",
                Manufacturer = reader["MARCA"] as string,
                Model = reader["MODEL"] as string,
                CpuModel = reader["PROCESSADOR"] as string,
                RamGb = reader["RAM_GB"] is DBNull ? null : Convert.ToInt32(reader["RAM_GB"]),
                StorageGb = reader["STORAGE_GB"] is DBNull ? null : Convert.ToInt32(reader["STORAGE_GB"]),
                StatusItem = reader["STATUS_ITEM"] as string ?? "PENDING",
                SalePriceUsd = reader["SALE_PRICE_USD"] is DBNull ? null : Convert.ToDecimal(reader["SALE_PRICE_USD"]),
                Notes = reader["NOTES"] as string,
                DataCadastro = reader["DATA_CADASTRO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_CADASTRO"])
            });
        }

        return list;
    }

    // For the shipment report (ReportService.vb GerarExcelRemessa/GerarPdfRemessa) —
    // PROC_SELECT_ITENS doesn't return SERIAL_NUMBER or CONDITION_STATUS, so this
    // joins TBL_REMESSA_ITEM straight to TBL_EQUIPAMENTO as plain SQL rather than
    // touching the package.
    public async Task<List<Models.ItemRemessa>> GetItemsForReportAsync(string remessaRef)
    {
        var list = new List<Models.ItemRemessa>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText =
            "SELECT E.INTERNAL_UID, E.MARCA, E.MODEL, E.SERIAL_NUMBER, E.PROCESSADOR, " +
            "       E.RAM_GB, E.STORAGE_GB, E.CONDITION_STATUS " +
            "  FROM TBL_REMESSA_ITEM RI " +
            "  JOIN TBL_REMESSA R ON R.ID_REMESSA = RI.ID_REMESSA " +
            "  JOIN TBL_EQUIPAMENTO E ON E.ID_EQUIPAMENTO = RI.ID_EQUIPAMENTO " +
            " WHERE R.CODIGO_REMESSA = :P_CODIGO_REMESSA " +
            " ORDER BY E.INTERNAL_UID";
        command.BindByName = true;
        AddParam(command, "P_CODIGO_REMESSA", OracleDbType.Varchar2, remessaRef);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.ItemRemessa
            {
                InternalUid = reader["INTERNAL_UID"] as string ?? "",
                Manufacturer = reader["MARCA"] as string,
                Model = reader["MODEL"] as string,
                SerialNumber = reader["SERIAL_NUMBER"] as string,
                CpuModel = reader["PROCESSADOR"] as string,
                RamGb = reader["RAM_GB"] is DBNull ? null : Convert.ToInt32(reader["RAM_GB"]),
                StorageGb = reader["STORAGE_GB"] is DBNull ? null : Convert.ToInt32(reader["STORAGE_GB"]),
                ConditionStatus = reader["CONDITION_STATUS"] as string
            });
        }

        return list;
    }

    // Port of clsLeituraRemessa.selecionarEquipamentosDisponiveis — no PACK_REMESSA
    // proc exists for this; the desktop app already runs it as plain SQL.
    public async Task<List<Models.Equipamento>> GetAvailableEquipmentAsync(string? search)
    {
        var list = new List<Models.Equipamento>();
        var filtro = "%" + (string.IsNullOrWhiteSpace(search) ? "" : search.Trim().ToUpperInvariant()) + "%";

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText =
            "SELECT E.ID_EQUIPAMENTO, E.INTERNAL_UID, E.SERIAL_NUMBER, " +
            "       E.MARCA, E.MODEL, E.PROCESSADOR, E.RAM_GB, E.STORAGE_GB, E.STATUS " +
            "  FROM TBL_EQUIPAMENTO E " +
            " WHERE E.STATUS IN ('IN_STOCK','AVAILABLE','IN_REPAIR') " +
            "   AND (UPPER(NVL(E.INTERNAL_UID,'')) LIKE :P_PESQ_UID " +
            "    OR  UPPER(NVL(E.MODEL,''))         LIKE :P_PESQ_MODEL " +
            "    OR  UPPER(NVL(E.MARCA,''))         LIKE :P_PESQ_MARCA) " +
            " ORDER BY E.INTERNAL_UID";
        command.BindByName = true;
        AddParam(command, "P_PESQ_UID", OracleDbType.Varchar2, filtro);
        AddParam(command, "P_PESQ_MODEL", OracleDbType.Varchar2, filtro);
        AddParam(command, "P_PESQ_MARCA", OracleDbType.Varchar2, filtro);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.Equipamento
            {
                IdEquipamento = Convert.ToInt32(reader["ID_EQUIPAMENTO"]),
                InternalUid = reader["INTERNAL_UID"] as string ?? "",
                SerialNumber = reader["SERIAL_NUMBER"] as string,
                Manufacturer = reader["MARCA"] as string ?? "",
                Model = reader["MODEL"] as string ?? "",
                CpuModel = reader["PROCESSADOR"] as string,
                RamGb = reader["RAM_GB"] is DBNull ? null : Convert.ToInt32(reader["RAM_GB"]),
                StorageGb = reader["STORAGE_GB"] is DBNull ? null : Convert.ToInt32(reader["STORAGE_GB"]),
                Status = reader["STATUS"] as string ?? ""
            });
        }

        return list;
    }

    // Port of RemessaController.incluirRemessa: PACK_REMESSA.PROC_INSERT_REMESSA for the
    // header (matches what the desktop actually calls), then the same follow-up raw
    // INSERT/UPDATE the desktop uses per item (PACK_REMESSA.PROC_ADD_ITEM has no
    // sale-price/notes params, so it can't carry what the desktop form collects).
    public async Task<string> CreateShipmentAsync(Models.Remessa remessa, List<Models.ItemRemessa> items)
    {
        var codigoRemessa = "GBS-SH-" + DateTime.Now.ToString("yyyyMMddHHmmss");

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "PACK_REMESSA.PROC_INSERT_REMESSA";
                command.BindByName = true;
                AddParam(command, "P_CODIGO_REMESSA", OracleDbType.Varchar2, codigoRemessa);
                AddParam(command, "P_DESTINATARIO", OracleDbType.Varchar2, remessa.RecipientName);
                AddParam(command, "P_CARRIER", OracleDbType.Varchar2, remessa.Carrier);
                AddParam(command, "P_TRACKING", OracleDbType.Varchar2, remessa.TrackingNumber);
                AddParam(command, "P_OBSERVACAO", OracleDbType.Varchar2, remessa.Notes);
                await command.ExecuteNonQueryAsync();
            }

            int idRemessa;
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = "SELECT ID_REMESSA FROM TBL_REMESSA WHERE CODIGO_REMESSA = :P_COD AND ROWNUM = 1";
                command.BindByName = true;
                AddParam(command, "P_COD", OracleDbType.Varchar2, codigoRemessa);
                idRemessa = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            foreach (var item in items)
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText =
                        "INSERT INTO TBL_REMESSA_ITEM (ID_REMESSA_ITEM, ID_REMESSA, ID_EQUIPAMENTO, STATUS_ITEM, SALE_PRICE_USD, NOTES, DATA_CADASTRO) " +
                        "VALUES (SEQ_REMESSA_ITEM.NEXTVAL, :P_ID_REMESSA, :P_ID_EQUIP, 'PENDING', :P_SALE_PRICE, :P_NOTES, SYSDATE)";
                    command.BindByName = true;
                    AddParam(command, "P_ID_REMESSA", OracleDbType.Int32, idRemessa);
                    AddParam(command, "P_ID_EQUIP", OracleDbType.Int32, item.IdEquipamento);
                    AddParam(command, "P_SALE_PRICE", OracleDbType.Decimal, item.SalePriceUsd);
                    AddParam(command, "P_NOTES", OracleDbType.Varchar2, item.Notes);
                    await command.ExecuteNonQueryAsync();
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText =
                        "UPDATE TBL_EQUIPAMENTO SET STATUS = 'SHIPPED', DATA_ATUALIZACAO = SYSDATE WHERE ID_EQUIPAMENTO = :P_ID_EQUIP";
                    command.BindByName = true;
                    AddParam(command, "P_ID_EQUIP", OracleDbType.Int32, item.IdEquipamento);
                    await command.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            return codigoRemessa;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Port of PACK_REMESSA.PROC_UPDATE_STATUS — the verified-valid package proc that
    // does the same DELIVERED→SOLD cascade the desktop's VB layer duplicates via raw
    // SQL. Also fixes a real gap: the desktop computes "today" as the delivery date but
    // never actually saves it (dropped at the last step) — DATA_ENTREGA gets set here.
    public async Task UpdateStatusAsync(int idRemessa, string remessaRef, string newStatus)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "PACK_REMESSA.PROC_UPDATE_STATUS";
                command.BindByName = true;
                AddParam(command, "P_CODIGO_REMESSA", OracleDbType.Varchar2, remessaRef);
                AddParam(command, "P_STATUS_REMESSA", OracleDbType.Varchar2, newStatus);
                await command.ExecuteNonQueryAsync();
            }

            if (string.Equals(newStatus, "DELIVERED", StringComparison.OrdinalIgnoreCase))
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = "UPDATE TBL_REMESSA SET DATA_ENTREGA = SYSDATE WHERE ID_REMESSA = :P_ID";
                command.BindByName = true;
                AddParam(command, "P_ID", OracleDbType.Int32, idRemessa);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static void AddParam(
        OracleCommand command, string name, OracleDbType type, object? value,
        ParameterDirection direction = ParameterDirection.Input)
    {
        var parameter = new OracleParameter(name, type) { Direction = direction, Value = value ?? DBNull.Value };
        command.Parameters.Add(parameter);
    }
}

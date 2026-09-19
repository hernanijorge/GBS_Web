using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace GBS_Web.Data;

// Port of clsLeituraHistorico.vb — feeds the equipment history page
// (frmHistoricoEquipamento.vb). Plain SQL throughout: no existing package proc
// returns this shape, and the desktop's own class is plain ad-hoc SQL too.
public class HistoryRepository
{
    private readonly string _connectionString;

    public HistoryRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    // Port of selecionarUpgradesEquipamento.
    public async Task<List<Models.HistoryUpgradeRow>> GetUpgradesAsync(int idEquipamento)
    {
        var list = new List<Models.HistoryUpgradeRow>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText =
            "SELECT U.DATA_UPGRADE, " +
            "       U.TIPO_UPGRADE AS COMPONENT_TYPE, " +
            "       CASE U.TIPO_UPGRADE " +
            "           WHEN 'RAM' THEN TO_CHAR(U.RAM_ANTERIOR_GB)     || ' GB' " +
            "           WHEN 'SSD' THEN TO_CHAR(U.STORAGE_ANTERIOR_GB) || ' GB' " +
            "           WHEN 'HDD' THEN TO_CHAR(U.STORAGE_ANTERIOR_GB) || ' GB' " +
            "           ELSE NULL " +
            "       END AS VALUE_BEFORE, " +
            "       CASE U.TIPO_UPGRADE " +
            "           WHEN 'RAM' THEN CASE WHEN U.RAM_NOVA_GB     > 0 THEN TO_CHAR(U.RAM_NOVA_GB)     || ' GB' END " +
            "           WHEN 'SSD' THEN CASE WHEN U.STORAGE_NOVO_GB > 0 THEN TO_CHAR(U.STORAGE_NOVO_GB) || ' GB' END " +
            "           WHEN 'HDD' THEN CASE WHEN U.STORAGE_NOVO_GB > 0 THEN TO_CHAR(U.STORAGE_NOVO_GB) || ' GB' END " +
            "           ELSE NULL " +
            "       END AS VALUE_AFTER, " +
            "       U.TECNICO    AS TECHNICIAN, " +
            "       U.OBSERVACAO AS NOTES " +
            "  FROM TBL_EQUIPAMENTO_UPGRADE U " +
            " WHERE U.ID_EQUIPAMENTO = :P_ID " +
            " ORDER BY U.DATA_UPGRADE DESC";
        command.BindByName = true;
        AddParam(command, "P_ID", OracleDbType.Int32, idEquipamento);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.HistoryUpgradeRow
            {
                DataUpgrade = reader["DATA_UPGRADE"] is DBNull ? null : Convert.ToDateTime(reader["DATA_UPGRADE"]),
                ComponentType = reader["COMPONENT_TYPE"] as string ?? "",
                ValueBefore = reader["VALUE_BEFORE"] as string,
                ValueAfter = reader["VALUE_AFTER"] as string,
                // SourceOrigem/CostUsd/PartSerial: no physical columns — the insert
                // path folds them into OBSERVACAO/Notes instead. Same as desktop.
                SourceOrigem = null,
                CostUsd = null,
                PartSerial = null,
                Technician = reader["TECHNICIAN"] as string,
                Notes = reader["NOTES"] as string
            });
        }

        return list;
    }

    // Port of selecionarShipmentEquipamento. RECIPIENT_NAME/DATA_ENVIO/DATA_ENTREGA
    // use the real TBL_REMESSA columns (DESTINATARIO/DATA_ENVIO/DATA_ENTREGA) —
    // the desktop's own query guesses RECIPIENT_NAME/ESTIMATED_DELIVERY/ACTUAL_DELIVERY
    // and falls back at runtime via its 3-tier try/catch when those don't exist.
    // SHIPPING_COST_USD and CONDITION_AT_SHIP have no backing columns (confirmed by
    // Remessa.ShippingCostUsd / ItemRemessa.ConditionAtShip) so stay null.
    public async Task<List<Models.HistoryShipmentRow>> GetShipmentsAsync(int idEquipamento)
    {
        var list = new List<Models.HistoryShipmentRow>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText =
            "SELECT R.CODIGO_REMESSA AS REMESSA_REF, R.CARRIER, R.TRACKING_NUMBER, " +
            "       R.DESTINATARIO AS RECIPIENT_NAME, " +
            "       R.DATA_ENVIO, R.DATA_ENTREGA, R.STATUS_REMESSA, " +
            "       RI.SALE_PRICE_USD, RI.NOTES AS ITEM_NOTES " +
            "  FROM TBL_REMESSA R " +
            "  JOIN TBL_REMESSA_ITEM RI ON RI.ID_REMESSA = R.ID_REMESSA " +
            " WHERE RI.ID_EQUIPAMENTO = :P_ID " +
            " ORDER BY R.ID_REMESSA DESC";
        command.BindByName = true;
        AddParam(command, "P_ID", OracleDbType.Int32, idEquipamento);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.HistoryShipmentRow
            {
                RemessaRef = reader["REMESSA_REF"] as string ?? "",
                Carrier = reader["CARRIER"] as string,
                TrackingNumber = reader["TRACKING_NUMBER"] as string,
                RecipientName = reader["RECIPIENT_NAME"] as string,
                DataEnvio = reader["DATA_ENVIO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_ENVIO"]),
                DataEntrega = reader["DATA_ENTREGA"] is DBNull ? null : Convert.ToDateTime(reader["DATA_ENTREGA"]),
                StatusRemessa = reader["STATUS_REMESSA"] as string,
                ShippingCostUsd = null,
                SalePriceUsd = reader["SALE_PRICE_USD"] is DBNull ? null : Convert.ToDecimal(reader["SALE_PRICE_USD"]),
                ConditionAtShip = null,
                ItemNotes = reader["ITEM_NOTES"] as string
            });
        }

        return list;
    }

    private static void AddParam(OracleCommand command, string name, OracleDbType type, object? value)
    {
        command.Parameters.Add(new OracleParameter(name, type) { Value = value ?? DBNull.Value });
    }
}

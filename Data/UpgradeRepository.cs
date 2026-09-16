using System.Data;
using System.Text;
using Oracle.ManagedDataAccess.Client;

namespace GBS_Web.Data;

public class UpgradeRepository
{
    private readonly string _connectionString;

    public UpgradeRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    public async Task<List<Models.Upgrade>> GetRecentUpgradesAsync(int days)
    {
        var list = new List<Models.Upgrade>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_UPGRADE.PROC_SELECT_UPGRADES_RECENTES";
        command.BindByName = true;
        AddParam(command, "V_DIAS", OracleDbType.Int32, days);
        AddParam(command, "V_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.Upgrade
            {
                IdUpgrade = Convert.ToInt32(reader["ID_UPGRADE"]),
                InternalUid = reader["INTERNAL_UID"] as string,
                DataUpgrade = reader["DATA_UPGRADE"] is DBNull ? null : Convert.ToDateTime(reader["DATA_UPGRADE"]),
                ComponentType = reader["COMPONENT_TYPE"] as string ?? "",
                ValueBefore = reader["VALUE_BEFORE"] as string,
                ValueAfter = reader["VALUE_AFTER"] as string,
                Technician = reader["TECHNICIAN"] as string,
                Notes = reader["NOTES"] as string
            });
        }

        return list;
    }

    public async Task<List<Models.Upgrade>> GetUpgradesForEquipmentAsync(int idEquipamento)
    {
        var list = new List<Models.Upgrade>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_UPGRADE.PROC_SELECT_UPGRADES_EQUIP";
        command.BindByName = true;
        AddParam(command, "V_ID_EQUIPAMENTO", OracleDbType.Int32, idEquipamento);
        AddParam(command, "V_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.Upgrade
            {
                IdUpgrade = Convert.ToInt32(reader["ID_UPGRADE"]),
                IdEquipamento = Convert.ToInt32(reader["ID_EQUIPAMENTO"]),
                DataUpgrade = reader["DATA_UPGRADE"] is DBNull ? null : Convert.ToDateTime(reader["DATA_UPGRADE"]),
                ComponentType = reader["COMPONENT_TYPE"] as string ?? "",
                ValueBefore = reader["VALUE_BEFORE"] as string,
                ValueAfter = reader["VALUE_AFTER"] as string,
                Technician = reader["TECHNICIAN"] as string,
                Notes = reader["NOTES"] as string
            });
        }

        return list;
    }

    // Port of clsLeituraUpgrade.selecionarOrigensDistintas.
    public async Task<List<string>> GetDistinctOriginsAsync()
    {
        var list = new List<string>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT DISTINCT SOURCE_BATCH FROM TBL_EQUIPAMENTO WHERE SOURCE_BATCH IS NOT NULL ORDER BY SOURCE_BATCH";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                list.Add(reader.GetString(0));
            }
        }

        return list;
    }

    // Port of clsGravacaoUpgrade.MontarObservacao — folds PartSerial/SourceOrigem/CostUsd
    // into the free-text OBSERVACAO blob, since no dedicated columns exist for them.
    private static string? MontarObservacao(Models.Upgrade upgrade)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(upgrade.Notes))
        {
            sb.Append(upgrade.Notes.Trim());
        }
        if (!string.IsNullOrWhiteSpace(upgrade.PartSerial))
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("PartSerial: ").Append(upgrade.PartSerial.Trim());
        }
        if (!string.IsNullOrWhiteSpace(upgrade.SourceOrigem))
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("Source: ").Append(upgrade.SourceOrigem.Trim());
        }
        if (upgrade.CostUsd.HasValue)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("CostUSD: ").Append(upgrade.CostUsd.Value.ToString("0.00"));
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    // Port of clsGravacaoUpgrade.incluirUpgrade + PACK_UPGRADE.PROC_INSERT_UPGRADE.
    // Fix vs. the desktop: RAM_ANTERIOR_GB/RAM_NOVA_GB/STORAGE_ANTERIOR_GB/STORAGE_NOVO_GB
    // are NOT NULL columns, but the desktop sends NULL for whichever pair doesn't apply
    // to the selected component type — that throws ORA-01400 for any type other than
    // RAM/SSD/HDD today. We send 0 instead (matches what those columns hold for the
    // applicable pair when there's no numeric value), which the package already handles
    // correctly (same as it always has for the pair that IS relevant).
    public async Task CreateUpgradeAsync(Models.Upgrade upgrade)
    {
        var tipo = (upgrade.ComponentType ?? "").Trim().ToUpperInvariant();
        var isRam = tipo == "RAM";
        var isStorage = tipo is "SSD" or "HDD" or "STORAGE";

        // REVERTED 2026-09-16: originally sent 0 instead of DBNull for the inapplicable
        // pair to dodge the ORA-01400 NOT NULL violation. That was wrong: PACK_UPGRADE's
        // cascade UPDATE uses "IS NOT NULL" to decide whether to touch RAM_GB/STORAGE_GB
        // on TBL_EQUIPAMENTO, and 0 satisfies that check — so every non-RAM/Storage
        // upgrade was silently zeroing out the equipment's RAM_GB/STORAGE_GB. Confirmed
        // live against WEBTEST001. Back to genuine null (crashes loudly instead of
        // corrupting data) until the real fix — relaxing the NOT NULL constraint on the
        // 4 log columns — is applied to the schema.
        int? ramAntes = isRam ? ExtrairNumeroInteiro(upgrade.ValueBefore) : null;
        int? ramDepois = isRam ? ExtrairNumeroInteiro(upgrade.ValueAfter) : null;
        int? storageAntes = isStorage ? ExtrairNumeroInteiro(upgrade.ValueBefore) : null;
        int? storageDepois = isStorage ? ExtrairNumeroInteiro(upgrade.ValueAfter) : null;

        var observacao = MontarObservacao(upgrade);

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_UPGRADE.PROC_INSERT_UPGRADE";
        command.BindByName = true;

        AddParam(command, "P_ID_EQUIPAMENTO", OracleDbType.Int32, upgrade.IdEquipamento);
        AddParam(command, "P_TIPO_UPGRADE", OracleDbType.Varchar2, tipo);
        AddParam(command, "P_RAM_ANTERIOR_GB", OracleDbType.Int32, ramAntes);
        AddParam(command, "P_RAM_NOVA_GB", OracleDbType.Int32, ramDepois);
        AddParam(command, "P_STORAGE_ANTERIOR_GB", OracleDbType.Int32, storageAntes);
        AddParam(command, "P_STORAGE_NOVO_GB", OracleDbType.Int32, storageDepois);
        AddParam(command, "P_TECNICO", OracleDbType.Varchar2, upgrade.Technician);
        AddParam(command, "P_OBSERVACAO", OracleDbType.Varchar2, observacao);
        AddParam(command, "P_ID_COMPONENT", OracleDbType.Int32, upgrade.IdComponent);
        AddParam(command, "P_ACTION_TYPE", OracleDbType.Varchar2, upgrade.ActionType);
        AddParam(command, "P_COMP_NEW_STATUS", OracleDbType.Varchar2, upgrade.CompNewStatus);

        await command.ExecuteNonQueryAsync();
    }

    // Port of clsGravacaoUpgrade.ExtrairNumeroInteiro.
    private static int ExtrairNumeroInteiro(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return 0;
        var match = System.Text.RegularExpressions.Regex.Match(valor, @"\d+");
        return match.Success && int.TryParse(match.Value, out var n) ? n : 0;
    }

    private static void AddParam(
        OracleCommand command, string name, OracleDbType type, object? value,
        ParameterDirection direction = ParameterDirection.Input)
    {
        var parameter = new OracleParameter(name, type) { Direction = direction, Value = value ?? DBNull.Value };
        command.Parameters.Add(parameter);
    }
}

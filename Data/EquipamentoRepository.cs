using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace GBS_Web.Data;

public class EquipamentoRepository
{
    private readonly string _connectionString;

    public EquipamentoRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    // Port of clsGravacaoEquipamento.upsertEquipamento (bulk-import path) via
    // PACK_EQUIPAMENTO.PROC_UPSERT_EQUIPAMENTO. The desktop's version always
    // reports "INSERTED" on success regardless of whether it inserted or
    // updated (the function never checks) — we check existence first so the
    // import summary's Inserted/Updated counts are actually accurate.
    public async Task<string> UpsertAsync(OracleConnection connection, OracleTransaction transaction, Models.Equipamento equipamento)
    {
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.CommandType = CommandType.Text;
            checkCommand.CommandText = "SELECT COUNT(*) FROM TBL_EQUIPAMENTO WHERE INTERNAL_UID = :P_UID";
            checkCommand.BindByName = true;
            AddParam(checkCommand, "P_UID", OracleDbType.Varchar2, equipamento.InternalUid);
            var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "PACK_EQUIPAMENTO.PROC_UPSERT_EQUIPAMENTO";
            command.BindByName = true;

            AddParam(command, "P_INTERNAL_UID", OracleDbType.Varchar2, equipamento.InternalUid);
            AddParam(command, "P_SERIAL_NUMBER", OracleDbType.Varchar2, equipamento.SerialNumber);
            AddParam(command, "P_MODEL", OracleDbType.Varchar2, equipamento.Model);
            AddParam(command, "P_MARCA", OracleDbType.Varchar2, equipamento.Manufacturer);
            AddParam(command, "P_PROCESSADOR", OracleDbType.Varchar2, equipamento.CpuModel);
            AddParam(command, "P_RAM_GB", OracleDbType.Decimal, equipamento.RamGb);
            AddParam(command, "P_STORAGE_GB", OracleDbType.Decimal, equipamento.StorageGb);
            AddParam(command, "P_CONDITION_STATUS", OracleDbType.Varchar2, equipamento.ConditionStatus);
            AddParam(command, "P_STATUS", OracleDbType.Varchar2, equipamento.Status);
            AddParam(command, "P_OBSERVACAO", OracleDbType.Varchar2, equipamento.Notes);

            await command.ExecuteNonQueryAsync();
            return exists ? "UPDATED" : "INSERTED";
        }
    }

    public async Task<List<Models.Equipamento>> GetAllAsync(string? filtro = null, string? status = null)
    {
        var list = new List<Models.Equipamento>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_EQUIPAMENTO.PROC_SELECT_FILTRO";
        command.BindByName = true;

        AddParam(command, "P_FILTRO", OracleDbType.Varchar2, filtro);
        AddParam(command, "P_STATUS_EQUIPAMENTO", OracleDbType.Varchar2, status);
        AddParam(command, "P_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapReader(reader));
        }

        return list;
    }

    public async Task AddAsync(Models.Equipamento equipamento)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_EQUIPAMENTO.PROC_INSERT";
        command.BindByName = true;

        AddParam(command, "P_INTERNAL_UID", OracleDbType.Varchar2, equipamento.InternalUid);
        AddParam(command, "P_SERIAL_NUMBER", OracleDbType.Varchar2, equipamento.SerialNumber);
        AddParam(command, "P_MODELO", OracleDbType.Varchar2, equipamento.Model);
        AddParam(command, "P_MARCA", OracleDbType.Varchar2, equipamento.Manufacturer);
        AddParam(command, "P_PROCESSADOR", OracleDbType.Varchar2, equipamento.CpuModel);
        AddParam(command, "P_RAM_GB", OracleDbType.Decimal, equipamento.RamGb);
        AddParam(command, "P_STORAGE_GB", OracleDbType.Decimal, equipamento.StorageGb);
        AddParam(command, "P_CONDITION_STATUS", OracleDbType.Varchar2, equipamento.ConditionStatus);
        AddParam(command, "P_STATUS_EQUIPAMENTO", OracleDbType.Varchar2, equipamento.Status);
        AddParam(command, "P_OBSERVACAO", OracleDbType.Varchar2, equipamento.Notes);
        AddParam(command, "P_BATTERY_CHECK", OracleDbType.Varchar2, equipamento.BatteryCheck);
        AddParam(command, "P_SOURCE_BATCH", OracleDbType.Varchar2, equipamento.SourceBatch);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateStatusAsync(string internalUid, string status)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_EQUIPAMENTO.PROC_UPDATE_STATUS";
        command.BindByName = true;

        AddParam(command, "P_INTERNAL_UID", OracleDbType.Varchar2, internalUid);
        AddParam(command, "P_STATUS", OracleDbType.Varchar2, status);

        await command.ExecuteNonQueryAsync();
    }

    // Used by the equipment history page (ReportService/frmHistoricoEquipamento port)
    // — no existing package proc fetches a single equipment by ID.
    public async Task<Models.Equipamento?> GetByIdAsync(int id)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText =
            "SELECT ID_EQUIPAMENTO, INTERNAL_UID, SERIAL_NUMBER, MARCA, MODEL, PROCESSADOR, " +
            "       RAM_GB, STORAGE_GB, CONDITION_STATUS, STATUS, OBSERVACAO, " +
            "       DATA_CADASTRO, DATA_ATUALIZACAO, SOURCE_BATCH " +
            "  FROM TBL_EQUIPAMENTO WHERE ID_EQUIPAMENTO = :P_ID";
        command.BindByName = true;
        AddParam(command, "P_ID", OracleDbType.Int32, id);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new Models.Equipamento
        {
            IdEquipamento = Convert.ToInt32(reader["ID_EQUIPAMENTO"]),
            InternalUid = reader["INTERNAL_UID"] as string ?? "",
            SerialNumber = reader["SERIAL_NUMBER"] as string,
            Manufacturer = reader["MARCA"] as string ?? "",
            Model = reader["MODEL"] as string ?? "",
            CpuModel = reader["PROCESSADOR"] as string,
            RamGb = reader["RAM_GB"] is DBNull ? null : Convert.ToInt32(reader["RAM_GB"]),
            StorageGb = reader["STORAGE_GB"] is DBNull ? null : Convert.ToInt32(reader["STORAGE_GB"]),
            ConditionStatus = reader["CONDITION_STATUS"] as string ?? "GOOD",
            Status = reader["STATUS"] as string ?? "IN_STOCK",
            Notes = reader["OBSERVACAO"] as string,
            DataCadastro = reader["DATA_CADASTRO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_CADASTRO"]),
            DataAtualizacao = reader["DATA_ATUALIZACAO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_ATUALIZACAO"]),
            SourceBatch = reader["SOURCE_BATCH"] as string
        };
    }

    // Port of frmImportacao.vb's CarregarGridAnalise + MontarMotivoProblema — pulls
    // back the just-imported rows by UID and flags data-quality issues (poor/fair
    // battery, IN_REPAIR status, or a non-empty observation).
    public async Task<List<Models.ImportAnalysisRow>> GetForImportAnalysisAsync(List<string> uids)
    {
        var list = new List<Models.ImportAnalysisRow>();
        if (uids.Count == 0) return list;

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        // Oracle caps a single IN-list at 1000 items — chunk like the desktop's
        // BuildInClause does.
        for (var offset = 0; offset < uids.Count; offset += 999)
        {
            var chunk = uids.Skip(offset).Take(999).ToList();

            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            var binds = string.Join(",", chunk.Select((_, i) => $":U{i}"));
            command.CommandText =
                "SELECT INTERNAL_UID, MARCA AS MANUFACTURER, MODEL, SERIAL_NUMBER, " +
                "       CONDITION_STATUS, STATUS, SOURCE_BATCH, OBSERVACAO " +
                "  FROM TBL_EQUIPAMENTO WHERE INTERNAL_UID IN (" + binds + ") " +
                " ORDER BY SOURCE_BATCH, INTERNAL_UID";
            command.BindByName = true;
            for (var i = 0; i < chunk.Count; i++)
            {
                AddParam(command, $"U{i}", OracleDbType.Varchar2, chunk[i]);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var condition = (reader["CONDITION_STATUS"] as string ?? "").Trim().ToUpperInvariant();
                var observation = reader["OBSERVACAO"] as string ?? "";
                var status = (reader["STATUS"] as string ?? "").Trim().ToUpperInvariant();
                var issueReason = MontarMotivoProblema(condition, observation, status);

                list.Add(new Models.ImportAnalysisRow
                {
                    InternalUid = reader["INTERNAL_UID"] as string ?? "",
                    Manufacturer = reader["MANUFACTURER"] as string,
                    Model = reader["MODEL"] as string,
                    SerialNumber = reader["SERIAL_NUMBER"] as string,
                    ConditionStatus = condition,
                    Status = status,
                    SourceBatch = reader["SOURCE_BATCH"] as string,
                    Observation = observation,
                    IssueReason = issueReason,
                    Problem = !string.IsNullOrWhiteSpace(issueReason)
                });
            }
        }

        return list;
    }

    private static string MontarMotivoProblema(string conditionStatus, string observacao, string status)
    {
        var motivos = new List<string>();
        if (conditionStatus is "FAIR" or "POOR")
        {
            motivos.Add("Battery condition: " + conditionStatus);
        }
        if (!string.IsNullOrWhiteSpace(observacao))
        {
            motivos.Add("Observation");
        }
        if (status == "IN_REPAIR")
        {
            motivos.Add("Status: IN_REPAIR");
        }
        return string.Join("; ", motivos);
    }

    public async Task<List<string>> GetManufacturersAsync()
    {
        var list = new List<string>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT DISTINCT MARCA FROM TBL_EQUIPAMENTO WHERE MARCA IS NOT NULL ORDER BY MARCA";

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

    private static OracleParameter AddParam(
        OracleCommand command, string name, OracleDbType type, object? value,
        ParameterDirection direction = ParameterDirection.Input, int size = 0)
    {
        var parameter = new OracleParameter(name, type) { Direction = direction, Value = value ?? DBNull.Value };
        if (size > 0)
        {
            parameter.Size = size;
        }
        command.Parameters.Add(parameter);
        return parameter;
    }

    private static Models.Equipamento MapReader(OracleDataReader reader)
    {
        return new Models.Equipamento
        {
            IdEquipamento = Convert.ToInt32(reader["ID_EQUIPAMENTO"]),
            InternalUid = reader["INTERNAL_UID"] as string ?? "",
            SerialNumber = reader["SERIAL_NUMBER"] as string,
            Manufacturer = reader["MARCA"] as string ?? "",
            Model = reader["MODEL"] as string ?? "",
            CpuModel = reader["PROCESSADOR"] as string,
            RamGb = reader["RAM_GB"] is DBNull ? null : Convert.ToInt32(reader["RAM_GB"]),
            StorageGb = reader["STORAGE_GB"] is DBNull ? null : Convert.ToInt32(reader["STORAGE_GB"]),
            ConditionStatus = reader["CONDITION_STATUS"] as string ?? "GOOD",
            Status = reader["STATUS"] as string ?? "IN_STOCK",
            Notes = reader["OBSERVACAO"] as string,
            DataCadastro = reader["DATA_CADASTRO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_CADASTRO"]),
            DataAtualizacao = reader["DATA_ATUALIZACAO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_ATUALIZACAO"]),
            StatusDescricao = reader["STATUS_DESCRICAO"] as string,
            SourceBatch = reader["SOURCE_BATCH"] as string
        };
    }
}

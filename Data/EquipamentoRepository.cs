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

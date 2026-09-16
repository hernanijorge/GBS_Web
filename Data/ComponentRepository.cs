using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace GBS_Web.Data;

public class ComponentRepository
{
    private readonly string _connectionString;

    public ComponentRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    public async Task<List<Models.Component>> GetAllAsync()
    {
        var list = new List<Models.Component>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_COMPONENT.PROC_SELECT_FILTER";
        command.BindByName = true;

        AddParam(command, "P_FILTER", OracleDbType.Varchar2, null);
        AddParam(command, "P_TYPE", OracleDbType.Varchar2, null);
        AddParam(command, "P_STATUS", OracleDbType.Varchar2, null);
        AddParam(command, "P_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapReader(reader));
        }

        return list;
    }

    public async Task<string> AddAsync(Models.Component component)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_COMPONENT.PROC_INSERT";
        command.BindByName = true;

        AddParam(command, "P_COMPONENT_TYPE", OracleDbType.Varchar2, component.ComponentType);
        AddParam(command, "P_CAPACITY_GB", OracleDbType.Int32, component.CapacityGb);
        AddParam(command, "P_SPEED_MHZ", OracleDbType.Int32, component.SpeedMhz);
        AddParam(command, "P_GENERATION", OracleDbType.Varchar2, component.Generation);
        AddParam(command, "P_BRAND", OracleDbType.Varchar2, component.Brand);
        AddParam(command, "P_PART_NUMBER", OracleDbType.Varchar2, component.PartNumber);
        AddParam(command, "P_CONDITION", OracleDbType.Varchar2, component.ConditionStatus);
        AddParam(command, "P_STATUS", OracleDbType.Varchar2, component.Status);
        AddParam(command, "P_SOURCE_BATCH", OracleDbType.Varchar2, component.SourceBatch);
        AddParam(command, "P_NOTES", OracleDbType.Varchar2, component.Notes);
        AddParam(command, "P_CPU", OracleDbType.Varchar2, component.Cpu);
        AddParam(command, "P_STORAGE_GB", OracleDbType.Int32, component.StorageGb);
        var uidOut = AddParam(command, "P_UID_OUT", OracleDbType.Varchar2, null, ParameterDirection.Output, size: 20);

        await command.ExecuteNonQueryAsync();

        return uidOut.Value?.ToString() ?? "";
    }

    public async Task UpdateStatusAsync(int id, string status)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_COMPONENT.PROC_UPDATE_STATUS";
        command.BindByName = true;

        AddParam(command, "P_ID", OracleDbType.Int32, id);
        AddParam(command, "P_STATUS", OracleDbType.Varchar2, status);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<string>> GetBrandsAsync()
    {
        var list = new List<string>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT DISTINCT BRAND FROM TBL_COMPONENT WHERE BRAND IS NOT NULL ORDER BY BRAND";

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

    private static Models.Component MapReader(OracleDataReader reader)
    {
        return new Models.Component
        {
            IdComponent = Convert.ToInt32(reader["ID_COMPONENT"]),
            InternalUid = reader["INTERNAL_UID"] as string ?? "",
            ComponentType = reader["COMPONENT_TYPE"] as string ?? "",
            CapacityGb = Convert.ToInt32(reader["CAPACITY_GB"]),
            SpeedMhz = reader["SPEED_MHZ"] is DBNull ? null : Convert.ToInt32(reader["SPEED_MHZ"]),
            Generation = reader["GENERATION"] as string,
            Brand = reader["BRAND"] as string,
            PartNumber = reader["PART_NUMBER"] as string,
            ConditionStatus = reader["CONDITION_STATUS"] as string ?? "GOOD",
            Status = reader["STATUS"] as string ?? "IN_STOCK",
            SourceBatch = reader["SOURCE_BATCH"] as string,
            Notes = reader["NOTES"] as string,
            Cpu = reader["CPU"] as string,
            StorageGb = reader["STORAGE_GB"] is DBNull ? null : Convert.ToInt32(reader["STORAGE_GB"]),
            DateCreated = reader["DATE_CREATED"] is DBNull ? null : Convert.ToDateTime(reader["DATE_CREATED"]),
            DateUpdated = reader["DATE_UPDATED"] is DBNull ? null : Convert.ToDateTime(reader["DATE_UPDATED"])
        };
    }
}

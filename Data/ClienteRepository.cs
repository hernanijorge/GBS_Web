using System.Data;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace GBS_Web.Data;

public class ClienteRepository
{
    private readonly string _connectionString;

    public ClienteRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    public async Task<List<Models.Cliente>> SearchAsync(string? search)
    {
        var list = new List<Models.Cliente>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_CLIENTE.PROC_SELECT_CLIENTES_FILTRO";
        command.BindByName = true;
        AddParam(command, "V_PESQUISA", OracleDbType.Varchar2, search);
        AddParam(command, "V_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapReader(reader));
        }

        return list;
    }

    public async Task<Models.Cliente?> GetByIdAsync(int idCliente)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_CLIENTE.PROC_SELECT_CLIENTE";
        command.BindByName = true;
        AddParam(command, "V_ID", OracleDbType.Int32, idCliente);
        AddParam(command, "V_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapReader(reader) : null;
    }

    public async Task<int> CreateAsync(Models.Cliente cliente)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_CLIENTE.PROC_INSERT_CLIENTE";
        command.BindByName = true;

        AddParam(command, "V_NOME_RAZAO", OracleDbType.Varchar2, cliente.NomeRazao);
        AddParam(command, "V_NOME_FANTASIA", OracleDbType.Varchar2, cliente.NomeFantasia);
        AddParam(command, "V_DOCUMENTO", OracleDbType.Varchar2, cliente.Documento);
        AddParam(command, "V_EMAIL", OracleDbType.Varchar2, cliente.Email);
        AddParam(command, "V_TELEFONE", OracleDbType.Varchar2, cliente.Telefone);
        AddParam(command, "V_ENDERECO1", OracleDbType.Varchar2, cliente.Endereco1);
        AddParam(command, "V_ENDERECO2", OracleDbType.Varchar2, cliente.Endereco2);
        AddParam(command, "V_CIDADE", OracleDbType.Varchar2, cliente.Cidade);
        AddParam(command, "V_ESTADO", OracleDbType.Varchar2, cliente.Estado);
        AddParam(command, "V_ZIP_CODE", OracleDbType.Varchar2, cliente.ZipCode);
        AddParam(command, "V_PAIS", OracleDbType.Varchar2, cliente.Pais);
        AddParam(command, "V_ATIVO", OracleDbType.Char, cliente.Ativo);
        AddParam(command, "V_OBSERVACOES", OracleDbType.Varchar2, cliente.Observacoes);
        var idOut = AddParam(command, "V_ID", OracleDbType.Int32, null, ParameterDirection.Output);

        await command.ExecuteNonQueryAsync();

        return Convert.ToInt32(((OracleDecimal)idOut.Value).Value);
    }

    public async Task UpdateAsync(Models.Cliente cliente)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_CLIENTE.PROC_UPDATE_CLIENTE";
        command.BindByName = true;

        AddParam(command, "V_ID", OracleDbType.Int32, cliente.IdCliente);
        AddParam(command, "V_NOME_RAZAO", OracleDbType.Varchar2, cliente.NomeRazao);
        AddParam(command, "V_NOME_FANTASIA", OracleDbType.Varchar2, cliente.NomeFantasia);
        AddParam(command, "V_DOCUMENTO", OracleDbType.Varchar2, cliente.Documento);
        AddParam(command, "V_EMAIL", OracleDbType.Varchar2, cliente.Email);
        AddParam(command, "V_TELEFONE", OracleDbType.Varchar2, cliente.Telefone);
        AddParam(command, "V_ENDERECO1", OracleDbType.Varchar2, cliente.Endereco1);
        AddParam(command, "V_ENDERECO2", OracleDbType.Varchar2, cliente.Endereco2);
        AddParam(command, "V_CIDADE", OracleDbType.Varchar2, cliente.Cidade);
        AddParam(command, "V_ESTADO", OracleDbType.Varchar2, cliente.Estado);
        AddParam(command, "V_ZIP_CODE", OracleDbType.Varchar2, cliente.ZipCode);
        AddParam(command, "V_PAIS", OracleDbType.Varchar2, cliente.Pais);
        AddParam(command, "V_ATIVO", OracleDbType.Char, cliente.Ativo);
        AddParam(command, "V_OBSERVACOES", OracleDbType.Varchar2, cliente.Observacoes);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int idCliente)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_CLIENTE.PROC_DELETE_CLIENTE";
        command.BindByName = true;
        AddParam(command, "V_ID", OracleDbType.Int32, idCliente);

        await command.ExecuteNonQueryAsync();
    }

    private static Models.Cliente MapReader(OracleDataReader reader)
    {
        return new Models.Cliente
        {
            IdCliente = Convert.ToInt32(reader["ID_CLIENTE"]),
            NomeRazao = reader["NOME_RAZAO"] as string ?? "",
            NomeFantasia = reader["NOME_FANTASIA"] as string,
            Documento = reader["DOCUMENTO"] as string,
            Email = reader["EMAIL"] as string,
            Telefone = reader["TELEFONE"] as string,
            Endereco1 = reader["ENDERECO1"] as string,
            Endereco2 = reader["ENDERECO2"] as string,
            Cidade = reader["CIDADE"] as string,
            Estado = reader["ESTADO"] as string,
            ZipCode = reader["ZIP_CODE"] as string,
            Pais = reader["PAIS"] as string ?? "USA",
            Ativo = reader["ATIVO"] as string ?? "Y",
            Observacoes = reader["OBSERVACOES"] as string,
            DataCadastro = reader["DATA_CADASTRO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_CADASTRO"]),
            DataAlteracao = reader["DATA_ALTERACAO"] is DBNull ? null : Convert.ToDateTime(reader["DATA_ALTERACAO"])
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

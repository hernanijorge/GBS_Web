using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace GBS_Web.Data;

public class DashboardTotals
{
    // Straight port of PACK_EQUIPAMENTO.PROC_DASHBOARD.
    public int TotalUnidades { get; set; }
    public int EmEstoque { get; set; }
    public int Upgrades30d { get; set; }
    public int RemessasAtivas { get; set; }

    // PROC_DASHBOARD (and its VB fallback) always hardcode 0 here — this metric was
    // never actually computed anywhere in GBS_Inventory. Computed for real below.
    public int CondicaoBoa { get; set; }
}

public class DashboardResumoCards
{
    // Port of frmPrincipal.vb's carregarDashboardResumoCards / CriarResumoAgrupado:
    // distinct Manufacturer/Model/CPU (Processador) counts among IN_STOCK-equivalent
    // equipment, computed client-side over the full equipment list (same as desktop).
    public int ManufacturerCount { get; set; }
    public int ModelCount { get; set; }
    public int CpuFamilyCount { get; set; }
}

public class DashboardRepository
{
    private readonly string _connectionString;

    public DashboardRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    public async Task<DashboardTotals> GetTotalsAsync()
    {
        var totals = new DashboardTotals();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "PACK_EQUIPAMENTO.PROC_DASHBOARD";
            command.BindByName = true;
            AddParam(command, "P_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                totals.TotalUnidades = Convert.ToInt32(reader["TOTAL_UNIDADES"]);
                totals.EmEstoque = Convert.ToInt32(reader["EM_ESTOQUE"]);
                totals.Upgrades30d = Convert.ToInt32(reader["UPGRADES_30D"]);
                totals.RemessasAtivas = Convert.ToInt32(reader["REMESSAS_ATIVAS"]);
            }
        }

        // Real implementation — PACK_EQUIPAMENTO never computed this (always 0).
        // Same rule as every other dashboard number: must filter STATUS = 'IN_STOCK'.
        using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.Text;
            command.CommandText =
                "SELECT COUNT(*) FROM TBL_EQUIPAMENTO " +
                "WHERE STATUS = 'IN_STOCK' AND CONDITION_STATUS IN ('EXCELLENT', 'GOOD')";
            totals.CondicaoBoa = Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        return totals;
    }

    public async Task<DashboardResumoCards> GetResumoCardsAsync()
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "PACK_EQUIPAMENTO.PROC_SELECT_FILTRO";
        command.BindByName = true;
        AddParam(command, "P_FILTRO", OracleDbType.Varchar2, null);
        AddParam(command, "P_STATUS_EQUIPAMENTO", OracleDbType.Varchar2, null);
        AddParam(command, "P_CURSOR", OracleDbType.RefCursor, null, ParameterDirection.Output);

        var manufacturersInStock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modelsInStock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cpuFamiliesInStock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var status = reader["STATUS"] as string ?? "";
            if (!EhStatusEstoque(NormalizarStatus(status)))
            {
                continue;
            }

            var manufacturer = (reader["MARCA"] as string ?? "").Trim();
            var model = (reader["MODEL"] as string ?? "").Trim();
            var cpu = (reader["PROCESSADOR"] as string ?? "").Trim();

            manufacturersInStock.Add(string.IsNullOrEmpty(manufacturer) ? "-" : manufacturer);
            modelsInStock.Add(string.IsNullOrEmpty(model) ? "-" : model);
            cpuFamiliesInStock.Add(string.IsNullOrEmpty(cpu) ? "-" : cpu);
        }

        return new DashboardResumoCards
        {
            ManufacturerCount = manufacturersInStock.Count,
            ModelCount = modelsInStock.Count,
            CpuFamilyCount = cpuFamiliesInStock.Count
        };
    }

    // Port of frmPrincipal.vb's EhStatusEstoque.
    private static bool EhStatusEstoque(string statusNormalizado) =>
        statusNormalizado is "INSTOCK" or "EMESTOQUE" or "ESTOQUE";

    // Port of frmPrincipal.vb's NormalizarStatus.
    private static string NormalizarStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "";
        }

        var s = status.Trim().ToUpperInvariant()
            .Replace("_", "").Replace("-", "").Replace(" ", "")
            .Replace("Á", "A").Replace("À", "A").Replace("Â", "A").Replace("Ã", "A")
            .Replace("É", "E").Replace("Ê", "E")
            .Replace("Í", "I")
            .Replace("Ó", "O").Replace("Ô", "O").Replace("Õ", "O")
            .Replace("Ú", "U")
            .Replace("Ç", "C");
        return s;
    }

    private static void AddParam(
        OracleCommand command, string name, OracleDbType type, object? value,
        ParameterDirection direction = ParameterDirection.Input)
    {
        var parameter = new OracleParameter(name, type) { Direction = direction, Value = value ?? DBNull.Value };
        command.Parameters.Add(parameter);
    }
}

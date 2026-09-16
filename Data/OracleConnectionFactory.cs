namespace GBS_Web.Data;

public static class OracleConnectionFactory
{
    public static string BuildConnectionString(IConfiguration configuration)
    {
        var fullOverride = Environment.GetEnvironmentVariable("GBS_ORACLE_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(fullOverride))
        {
            return fullOverride;
        }

        var dataSource = configuration["Oracle:DataSource"]
            ?? throw new InvalidOperationException("Oracle:DataSource não configurado em appsettings.json.");
        var userId = configuration["Oracle:UserId"]
            ?? throw new InvalidOperationException("Oracle:UserId não configurado em appsettings.json.");
        var password = Environment.GetEnvironmentVariable("GBS_ORACLE_PASSWORD")
            ?? throw new InvalidOperationException("Variável de ambiente GBS_ORACLE_PASSWORD não definida.");

        return $"User Id={userId};Password={password};Data Source={dataSource};";
    }
}

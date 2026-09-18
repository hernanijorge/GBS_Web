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

        var password = Environment.GetEnvironmentVariable("GBS_ORACLE_PASSWORD")
            ?? throw new InvalidOperationException("Variável de ambiente GBS_ORACLE_PASSWORD não definida.");
        var userId = configuration["Oracle:UserId"]
            ?? throw new InvalidOperationException("Oracle:UserId não configurado em appsettings.json.");

        var target = Environment.GetEnvironmentVariable("GBS_ORACLE_TARGET") ?? "LOCAL";

        return target.ToUpperInvariant() switch
        {
            "LOCAL" => BuildLocal(configuration, userId, password),
            "ADB_TLS" => BuildAdbTls(configuration, userId, password),
            "ADB_WALLET" => BuildAdbWallet(configuration, userId, password),
            _ => throw new InvalidOperationException(
                $"GBS_ORACLE_TARGET='{target}' inválido. Valores aceitos: LOCAL, ADB_TLS, ADB_WALLET.")
        };
    }

    // Local Oracle XE — same as always, host:port/service_name, no TLS.
    static string BuildLocal(IConfiguration configuration, string userId, string password)
    {
        var dataSource = configuration["Oracle:DataSource"]
            ?? throw new InvalidOperationException("Oracle:DataSource não configurado em appsettings.json.");

        return $"User Id={userId};Password={password};Data Source={dataSource};";
    }

    // Autonomous DB, TLS-only, no wallet — the mode actually validated end to
    // end (Passo 0 migration ran through this exact connect string shape).
    // Requires the ADB's "Mutual TLS (mTLS) authentication" set to
    // "Not required" and its Access Control List allowing this host's IP.
    static string BuildAdbTls(IConfiguration configuration, string userId, string password)
    {
        var host = configuration["Oracle:AdbHost"]
            ?? throw new InvalidOperationException("Oracle:AdbHost não configurado em appsettings.json.");
        var serviceName = configuration["Oracle:AdbServiceName"]
            ?? throw new InvalidOperationException("Oracle:AdbServiceName não configurado em appsettings.json.");

        var easyConnectPlus = $"tcps://{host}:1522/{serviceName}?ssl_server_dn_match=true";
        return $"User Id={userId};Password={password};Data Source={easyConnectPlus};";
    }

    // Autonomous DB, mTLS via wallet — NOT exercised against a real wallet
    // yet (this deployment currently runs with mTLS "Not required", so there
    // is no wallet to test against). Kept as a documented fallback for if
    // mTLS authentication is turned back on: set TNS_ADMIN to the wallet's
    // extracted folder and GBS_ORACLE_TARGET=ADB_WALLET, and
    // Oracle:AdbTnsAlias to the alias name from that wallet's tnsnames.ora
    // (e.g. "gbs_tp").
    static string BuildAdbWallet(IConfiguration configuration, string userId, string password)
    {
        var tnsAdmin = Environment.GetEnvironmentVariable("TNS_ADMIN")
            ?? throw new InvalidOperationException("Variável de ambiente TNS_ADMIN não definida (deve apontar pra pasta extraída do wallet).");
        var tnsAlias = configuration["Oracle:AdbTnsAlias"]
            ?? throw new InvalidOperationException("Oracle:AdbTnsAlias não configurado em appsettings.json.");

        return $"User Id={userId};Password={password};Data Source={tnsAlias};TNS_ADMIN={tnsAdmin};";
    }
}

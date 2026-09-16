using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;

namespace GBS_Web.Data;

public class BackupRunResult
{
    public bool Success { get; set; }
    public string? DumpName { get; set; }
    public int ExitCode { get; set; }
}

public class BackupRepository
{
    private readonly string _connectionString;

    // Same physical script the desktop app runs — not a copy. Its default
    // -AppConfigPath resolves relative to its own folder to GBS_Inventory's
    // App.config, which already has the working Oracle credentials.
    private const string BackupScriptPath = @"C:\Users\herna\Desktop\GBS\GBS_Inventory\Database\backup_gbs_weekly.ps1";

    public BackupRepository(IConfiguration configuration)
    {
        _connectionString = OracleConnectionFactory.BuildConnectionString(configuration);
    }

    // Port of clsReadBackupLog.selectAll — no package exists for this, matches
    // the desktop's own plain-SQL read.
    public async Task<List<Models.BackupLog>> GetLogAsync()
    {
        var list = new List<Models.BackupLog>();

        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText =
            "SELECT ID_BACKUP_LOG, DATA_EXECUCAO, ARQUIVO_DMP, STATUS, MENSAGEM " +
            "  FROM TBL_BACKUP_LOG ORDER BY DATA_EXECUCAO DESC";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Models.BackupLog
            {
                IdBackupLog = Convert.ToInt32(reader["ID_BACKUP_LOG"]),
                DataExecucao = Convert.ToDateTime(reader["DATA_EXECUCAO"]),
                ArquivoDmp = reader["ARQUIVO_DMP"] as string,
                Status = reader["STATUS"] as string ?? "",
                Mensagem = reader["MENSAGEM"] as string
            });
        }

        return list;
    }

    // Port of frmPrincipal.ExecutarBackupScript — same script, same "OK: dump
    // created - (.+\.dmp)" success marker. The script itself writes to
    // TBL_BACKUP_LOG; we just run it and report the result.
    public async Task<BackupRunResult> RunBackupAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{BackupScriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var output = new StringBuilder();

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        var exitCode = process.ExitCode;
        var dumpName = "";
        var match = Regex.Match(output.ToString(), @"OK: dump created - (.+\.dmp)");
        if (match.Success)
        {
            dumpName = Path.GetFileName(match.Groups[1].Value.Trim());
        }

        return new BackupRunResult { Success = exitCode == 0, DumpName = dumpName, ExitCode = exitCode };
    }
}

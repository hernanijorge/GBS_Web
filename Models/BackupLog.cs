namespace GBS_Web.Models;

public class BackupLog
{
    public int IdBackupLog { get; set; }
    public DateTime DataExecucao { get; set; }
    public string? ArquivoDmp { get; set; }
    public string Status { get; set; } = "";
    public string? Mensagem { get; set; }
}

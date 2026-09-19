namespace GBS_Web.Models;

// Port of frmImportacao.vb's post-import analysis grid (CarregarGridAnalise) —
// one row per just-imported equipment, with a computed data-quality flag.
public class ImportAnalysisRow
{
    public string InternalUid { get; set; } = "";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? ConditionStatus { get; set; }
    public string? Status { get; set; }
    public string? SourceBatch { get; set; }
    public string? Observation { get; set; }

    // Auto-computed by MontarMotivoProblema; not affected by a manual Problem
    // override — same behavior as the desktop (ISSUE_REASON keeps showing the
    // original computed reason even after the user re-flags the row).
    public string IssueReason { get; set; } = "";

    public bool Problem { get; set; }
}

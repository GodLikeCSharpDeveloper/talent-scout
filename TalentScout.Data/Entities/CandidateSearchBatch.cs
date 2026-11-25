namespace TalentScout.Data.Entities;

public sealed class CandidateSearchBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public int RequestedCount { get; set; }
    public int AddedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ScannedCount { get; set; }
    public int PagesScanned { get; set; }
    public string? NextCursor { get; set; }
    public CandidateSearchBatchStatus Status { get; set; }
    public CandidateSearchStopReason? StopReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
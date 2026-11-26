namespace TalentScout.Data.Entities;

public enum CampaignStatus
{
    Draft,
    Active,
    Paused,
    Stopped
}

public sealed class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Description { get; set; } = "";
    public string Requirements { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string SearchQuery { get; set; } = "";
    public string Location { get; set; } = "";
    public string? LocationId { get; set; }
    public string Recruiter { get; set; } = "";
    public string InterviewUrl { get; set; } = "";
    public decimal MinimumFitScore { get; set; } = 0.7m;
    public bool AutoApproveCandidates { get; set; }
    public bool AutoApproveInterviewInvitations { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NextOutboundAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
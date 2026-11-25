namespace TalentScout.Api.Features.Pipeline;

public sealed class PipelineOptions
{
    public bool WorkerEnabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 10;
    public int MinimumSendIntervalSeconds { get; set; } = 60;
    public int MaximumDailyMessagesPerCampaign { get; set; } = 50;
}
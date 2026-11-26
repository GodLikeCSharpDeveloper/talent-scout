namespace TalentScout.Api.Features.Realtime;

public sealed record WorkspaceNotification(
    Guid Id,
    string Type,
    Guid CampaignId,
    string CampaignName,
    int Count,
    bool AutomaticApproval);
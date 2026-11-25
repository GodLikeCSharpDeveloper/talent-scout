namespace TalentScout.Api.Features.Campaigns.Requests;

public sealed record CreateCampaignRequest(
    string Name,
    string JobTitle,
    string Description,
    string Requirements,
    string SearchQuery,
    string Location,
    string Recruiter,
    string? InterviewUrl,
    decimal MinimumFitScore = 0.7m,
    bool AutoApproveCandidates = false);
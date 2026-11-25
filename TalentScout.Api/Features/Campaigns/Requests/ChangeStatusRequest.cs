using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Campaigns.Requests;

public sealed record ChangeStatusRequest(CampaignStatus Status);
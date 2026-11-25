namespace TalentScout.Api.Features.Candidates.Requests;

public sealed record SearchCandidatesRequest(int Limit = 25, string? Cursor = null, int MaxProfilesToScan = 1000);
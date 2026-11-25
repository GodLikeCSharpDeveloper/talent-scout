namespace TalentScout.Api.Features.Candidates.Requests;

public sealed record ImportCandidateRequest(string ExternalId, string ProfileUrl, string Name, string Headline, string Location, string Experience);
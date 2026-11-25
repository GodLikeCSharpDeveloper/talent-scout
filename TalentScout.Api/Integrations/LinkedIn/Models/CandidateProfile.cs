namespace TalentScout.Api.Integrations.LinkedIn.Models;

public sealed record CandidateProfile(string ExternalId, string ProfileUrl, string Name, string Headline, string Location, string Experience);
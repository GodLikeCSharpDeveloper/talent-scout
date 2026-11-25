namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

public sealed class UnipileProfile
{
    public string ProviderId { get; init; } = "";
    public string? PublicIdentifier { get; init; }
    public string? PublicProfileUrl { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Headline { get; init; }
    public string? Location { get; init; }
    public string? Summary { get; init; }
    public string? NetworkDistance { get; init; }
    public bool IsRelationship { get; init; }
    public UnipileInvitation? Invitation { get; init; }
    public List<UnipileExperience>? WorkExperience { get; init; }
}
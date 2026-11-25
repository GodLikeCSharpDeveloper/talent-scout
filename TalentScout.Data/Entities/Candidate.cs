namespace TalentScout.Data.Entities;

public sealed class Candidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; } = "";
    public string ProfileUrl { get; set; } = "";
    public string Name { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Location { get; set; } = "";
    public string Experience { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
namespace TalentScout.Data.Entities;

public sealed class LinkedInLocationCache
{
    public string Id { get; set; } = "";
    public string ResultsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAt { get; set; }
}
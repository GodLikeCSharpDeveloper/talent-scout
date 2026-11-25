namespace TalentScout.Api.Integrations.Ai.Options;

public sealed class AiOptions
{
    public string Provider { get; set; } = "Disabled";
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public decimal MinimumInterestConfidence { get; set; } = 0.8m;
}
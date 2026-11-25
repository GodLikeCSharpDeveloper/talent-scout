namespace TalentScout.Api.Integrations.Ai.Options;

public sealed class AiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4.1-mini";
    public decimal MinimumInterestConfidence { get; set; } = 0.8m;
}
using OpenAI.Chat;

namespace TalentScout.Api.Integrations.Ai.Providers.OpenAi;

internal static class OpenAiResponseFormats
{
    public static readonly ChatResponseFormat ProfileAssessment = ChatResponseFormat.CreateJsonSchemaFormat(
        "profile_assessment",
        BinaryData.FromString(
            """
            {
              "type": "object",
              "properties": {
                "meetsRequiredCriteria": { "type": "boolean" },
                "score": { "type": "number", "minimum": 0, "maximum": 1 },
                "reason": { "type": "string" }
              },
              "required": ["meetsRequiredCriteria", "score", "reason"],
              "additionalProperties": false
            }
            """),
        jsonSchemaIsStrict: true);

    public static readonly ChatResponseFormat Introduction = ChatResponseFormat.CreateJsonSchemaFormat(
        "introduction",
        BinaryData.FromString(
            """
            {
              "type": "object",
              "properties": { "text": { "type": "string" } },
              "required": ["text"],
              "additionalProperties": false
            }
            """),
        jsonSchemaIsStrict: true);

    public static readonly ChatResponseFormat ReplyAssessment = ChatResponseFormat.CreateJsonSchemaFormat(
        "reply_assessment",
        BinaryData.FromString(
            """
            {
              "type": "object",
              "properties": {
                "interest": { "type": "string", "enum": ["Interested", "NotInterested", "Ambiguous"] },
                "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
                "reason": { "type": "string" }
              },
              "required": ["interest", "confidence", "reason"],
              "additionalProperties": false
            }
            """),
        jsonSchemaIsStrict: true);
}
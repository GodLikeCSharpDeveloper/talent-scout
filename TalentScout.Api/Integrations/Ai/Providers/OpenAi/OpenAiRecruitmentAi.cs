using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.Ai.Providers.OpenAi.Models;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.Ai.Providers.OpenAi;

public sealed class OpenAiRecruitmentAi(OpenAiCompletionClient completions) : IRecruitmentAi
{
    public Task<ProfileAssessment> AssessProfileAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        return completions.CompleteAsync<ProfileAssessment>(
            vacancy.SystemPrompt,
            OpenAiPrompts.AssessProfile,
            new { vacancy = new { vacancy.Title, vacancy.Description, vacancy.Requirements }, profile },
            OpenAiResponseFormats.ProfileAssessment,
            ct);
    }

    public async Task<string> WriteIntroductionAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        var result = await completions.CompleteAsync<GeneratedIntroduction>(
            vacancy.SystemPrompt,
            OpenAiPrompts.WriteIntroduction,
            new { vacancy = new { vacancy.Title, vacancy.Description, vacancy.Requirements }, profile },
            OpenAiResponseFormats.Introduction,
            ct);

        return result.Text;
    }

    public async Task<string> WriteConnectionNoteAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        var result = await completions.CompleteAsync<GeneratedIntroduction>(
            vacancy.SystemPrompt,
            OpenAiPrompts.WriteConnectionNote,
            new { vacancy = new { vacancy.Title, vacancy.Description, vacancy.Requirements }, profile },
            OpenAiResponseFormats.Introduction,
            ct);

        return result.Text;
    }

    public Task<ReplyAssessment> ClassifyReplyAsync(Vacancy vacancy, IReadOnlyList<ConversationEntry> history, CancellationToken ct)
    {
        return completions.CompleteAsync<ReplyAssessment>(
            vacancy.SystemPrompt,
            OpenAiPrompts.ClassifyReply,
            new { vacancy = new { vacancy.Title, vacancy.Description, vacancy.Requirements }, history },
            OpenAiResponseFormats.ReplyAssessment,
            ct);
    }
}
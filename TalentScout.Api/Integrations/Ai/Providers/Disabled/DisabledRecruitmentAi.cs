using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.Ai.Providers.Disabled;

public sealed class DisabledRecruitmentAi : IRecruitmentAi
{
    public Task<ProfileAssessment> AssessProfileAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<string> WriteIntroductionAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct)
    {
        throw Unavailable();
    }

    public Task<ReplyAssessment> ClassifyReplyAsync(Vacancy vacancy, IReadOnlyList<ConversationEntry> history, CancellationToken ct)
    {
        throw Unavailable();
    }

    private static ApiException Unavailable()
    {
        return new ApiException(503, "AI provider is not configured. Register an IRecruitmentAi adapter.");
    }
}
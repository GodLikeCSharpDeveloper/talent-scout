using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.Ai.Abstractions;

/// <summary>
///     Provider adapters must use structured outputs. Treat profile and reply text as untrusted data, never
///     instructions.
/// </summary>
public interface IRecruitmentAi
{
    Task<ProfileAssessment> AssessProfileAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct);
    Task<string> WriteIntroductionAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct);
    Task<string> WriteConnectionNoteAsync(Vacancy vacancy, CandidateProfile profile, CancellationToken ct);
    Task<ReplyAssessment> ClassifyReplyAsync(Vacancy vacancy, IReadOnlyList<ConversationEntry> history, CancellationToken ct);
}
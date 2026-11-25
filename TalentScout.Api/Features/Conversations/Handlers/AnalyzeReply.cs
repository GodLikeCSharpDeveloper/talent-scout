using FluentValidation;
using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.Ai.Options;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class ReplyAnalyzer(IMessageRepository messages, IAuditRepository audit, IUnitOfWork unitOfWork, IRecruitmentAi ai, IOptions<AiOptions> options, IValidator<ReplyAssessment> validator)
{
    public async Task AnalyzeAsync(Enrollment enrollment, Message reply, CancellationToken ct)
    {
        var history = (await messages.GetHistoryAsync(enrollment.Id, reply.CreatedAt, ct))
            .Select(x => new ConversationEntry(x.Kind == MessageKind.Reply ? "candidate" : "recruiter", x.Content)).ToList();
        var campaign = enrollment.Campaign;
        var result = await ai.ClassifyReplyAsync(new Vacancy(campaign.JobTitle, campaign.Description, campaign.Requirements, campaign.SystemPrompt), history, ct);

        if (result is null || !(await validator.ValidateAsync(result, ct)).IsValid)
            throw new ApiException(502, "AI returned an invalid reply assessment.");

        reply.Interest = result.Interest;
        reply.Confidence = result.Confidence;
        reply.Analysis = result.Reason.Trim();
        reply.Analyzed = true;
        if (enrollment.Status != CandidateStatus.InterviewBooked)
        {
            var interest = result.Confidence >= options.Value.MinimumInterestConfidence ? result.Interest : Interest.Ambiguous;
            enrollment.Status = interest switch
            {
                Interest.Interested when enrollment.HasSentInvitation => CandidateStatus.InterviewInvitationSent,
                Interest.Interested => CandidateStatus.Interested,
                Interest.NotInterested => CandidateStatus.NotInterested,
                _ => CandidateStatus.NeedsReview
            };
        }

        audit.Add(campaign.Id, enrollment.Id, "ReplyAnalyzed", enrollment.Status.ToString());
        await unitOfWork.SaveChangesAsync(ct);
    }
}
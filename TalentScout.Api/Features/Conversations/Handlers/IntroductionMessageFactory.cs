using FluentValidation;
using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class IntroductionMessageFactory(
    IMessageRepository messages,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    IRecruitmentAi ai,
    IValidator<Message> validator)
{
    public async Task<Message> GetOrCreateAsync(Enrollment enrollment, CancellationToken ct)
    {
        var existing = await messages.FindOutgoingAsync(enrollment.Id, MessageKind.Initial, ct);

        if (existing is not null)
            return existing;

        var campaign = enrollment.Campaign;
        var candidate = enrollment.Candidate;
        var text = await ai.WriteIntroductionAsync(
            new Vacancy(campaign.JobTitle, campaign.Description, campaign.Requirements, campaign.SystemPrompt),
            new CandidateProfile(candidate.ExternalId, candidate.ProfileUrl, candidate.Name, candidate.Headline, candidate.Location, candidate.Experience),
            ct);
        var message = new Message { EnrollmentId = enrollment.Id, Kind = MessageKind.Initial, Content = text };

        if (!(await validator.ValidateAsync(message, ct)).IsValid)
            throw new ApiException(502, "The generated message content is invalid.");

        message.Content = message.Content.Trim();
        messages.Add(message);
        audit.Add(campaign.Id, enrollment.Id, "MessageQueued", MessageKind.Initial.ToString());
        await unitOfWork.SaveChangesAsync(ct);

        return message;
    }
}
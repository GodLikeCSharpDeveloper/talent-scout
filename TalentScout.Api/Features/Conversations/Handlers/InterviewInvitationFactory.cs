using FluentValidation;
using TalentScout.Api.Integrations.Scheduling;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class InterviewInvitationFactory(
    IMessageRepository messages,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    IInterviewScheduler scheduler,
    IValidator<Message> validator)
{
    public async Task<Message> GetOrCreateAsync(Enrollment enrollment, CancellationToken ct)
    {
        var existing = await messages.FindOutgoingAsync(enrollment.Id, MessageKind.Invitation, ct);

        if (existing is not null)
            return existing;

        var campaign = enrollment.Campaign;
        var candidate = enrollment.Candidate;
        var text = $"Здравствуйте, {candidate.Name}! Спасибо за интерес к позиции {campaign.JobTitle}. Выберите удобное время для интервью: {await scheduler.GetBookingUrlAsync(campaign, enrollment, ct)}";
        var message = new Message { EnrollmentId = enrollment.Id, Kind = MessageKind.Invitation, Content = text };

        if (!(await validator.ValidateAsync(message, ct)).IsValid)
            throw new ApiException(502, "The generated message content is invalid.");

        message.Content = message.Content.Trim();
        messages.Add(message);
        audit.Add(campaign.Id, enrollment.Id, "MessageQueued", MessageKind.Invitation.ToString());
        await unitOfWork.SaveChangesAsync(ct);

        return message;
    }
}
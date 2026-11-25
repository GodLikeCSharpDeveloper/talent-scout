using Ardalis.Result;
using TalentScout.Api.Features.Conversations.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class SendIntroductionHandler(
    IEnrollmentRepository enrollments,
    IMessageRepository messages,
    IntroductionMessageFactory factory,
    MessageDispatcher dispatcher)
{
    public async Task<Result<SendMessageResponse>> HandleAsync(Guid id, CancellationToken ct)
    {
        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<SendMessageResponse>.NotFound("Enrollment was not found.");
        if (enrollment.Campaign.Status != CampaignStatus.Active &&
            !(enrollment.Campaign.Status == CampaignStatus.Draft && enrollment.Campaign.AutoApproveCandidates))
            return Result<SendMessageResponse>.Conflict("Campaign must be active or have automatic outreach enabled.");
        if (enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded)
            return Result<SendMessageResponse>.Conflict("Candidate was excluded from outreach.");
        if (!enrollment.HasSentInitialMessage && enrollment.Status != CandidateStatus.Screened)
            return Result<SendMessageResponse>.Conflict("Candidate must have status Screened.");

        if (!enrollment.HasSentInitialMessage && !enrollment.Campaign.AutoApproveCandidates &&
            (enrollment.ApprovalStatus != OutreachApprovalStatus.Approved || enrollment.OutreachStartedAt is null))
            return Result<SendMessageResponse>.Conflict("Approve this candidate and start outreach before sending an introduction.");

        var message = await factory.GetOrCreateAsync(enrollment, ct);
        if (message.Delivery == DeliveryStatus.Pending)
            await dispatcher.DispatchAsync(enrollment, message, ct);
        await messages.ReloadAsync(message, ct);
        await enrollments.ReloadAsync(enrollment, ct);

        return Result<SendMessageResponse>.Success(
            new SendMessageResponse(
                enrollment.Id,
                message.Id,
                message.Kind,
                message.Delivery,
                enrollment.Status,
                message.Content,
                message.SentAt));
    }
}
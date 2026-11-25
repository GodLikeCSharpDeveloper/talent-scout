using Ardalis.Result;
using TalentScout.Api.Features.Conversations.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class SendInterviewInvitationHandler(
    IEnrollmentRepository enrollments,
    IMessageRepository messages,
    InterviewInvitationFactory factory,
    MessageDispatcher dispatcher)
{
    public async Task<Result<SendMessageResponse>> HandleAsync(Guid id, CancellationToken ct)
    {
        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<SendMessageResponse>.NotFound("Enrollment was not found.");
        if (enrollment.Campaign.Status != CampaignStatus.Active)
            return Result<SendMessageResponse>.Conflict("Campaign must be active.");
        if (enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded)
            return Result<SendMessageResponse>.Conflict("Candidate was excluded from outreach.");
        if (!enrollment.HasSentInvitation && enrollment.Status != CandidateStatus.Interested)
            return Result<SendMessageResponse>.Conflict("Candidate must have status Interested.");
        if (!enrollment.HasSentInitialMessage)
            return Result<SendMessageResponse>.Conflict("The introduction must be sent first.");
        if (await messages.GetNextUnanalyzedReplyAsync(id, ct) is not null)
            return Result<SendMessageResponse>.Conflict("Analyze pending replies before sending an interview invitation.");

        if (!enrollment.HasSentInvitation && enrollment.InvitationApprovalStatus == InvitationApprovalStatus.Rejected)
            return Result<SendMessageResponse>.Conflict("The interview invitation was rejected by the recruiter.");
        if (!enrollment.HasSentInvitation && !enrollment.Campaign.AutoApproveInterviewInvitations &&
            enrollment.InvitationApprovalStatus != InvitationApprovalStatus.Approved)
            return Result<SendMessageResponse>.Conflict("Approve this interview invitation or enable automatic interview invitations.");

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
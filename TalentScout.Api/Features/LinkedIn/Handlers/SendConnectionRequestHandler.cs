using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class SendConnectionRequestHandler(
    IEnrollmentRepository enrollments,
    ILinkedInConversationRepository conversations,
    IOptions<LinkedInOptions> options,
    ILinkedInContacts contacts,
    GetLinkedInConversationHandler getConversation,
    IValidator<SendConnectionRequest> validator)
{
    public async Task<Result<LinkedInConversationResponse>> HandleAsync(Guid id, SendConnectionRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<LinkedInConversationResponse>.Invalid(validation.AsErrors());
        if (!options.Value.IsConfigured)
            return Result<LinkedInConversationResponse>.Unavailable("LinkedIn provider is not configured.");

        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<LinkedInConversationResponse>.NotFound("Enrollment was not found.");
        if (enrollment.Campaign.Status is CampaignStatus.Paused or CampaignStatus.Stopped ||
            enrollment.Status != CandidateStatus.Screened || enrollment.ApprovalStatus == OutreachApprovalStatus.Excluded ||
            (!enrollment.Campaign.AutoApproveCandidates && enrollment.ApprovalStatus != OutreachApprovalStatus.Approved))
            return Result<LinkedInConversationResponse>.Conflict("Approve this screened candidate before sending a connection request.");

        var conversation = await conversations.ReserveAsync(id, options.Value.AccountId, enrollment.Candidate.ExternalId, ct);

        if (conversation.EnrollmentId != id || conversation.AccountId != options.Value.AccountId)
            return Result<LinkedInConversationResponse>.Conflict("This candidate's LinkedIn conversation belongs to another campaign.");

        var status = await contacts.GetStatusAsync(enrollment.Candidate.ExternalId, ct);

        if (status.Connected || status.InvitationPending || conversation.ConnectionRequestDelivery.HasValue)
            return await getConversation.HandleAsync(id, ct);
        if (!await conversations.TryClaimInvitationAsync(conversation.Id, DateTimeOffset.UtcNow, ct))
            return Result<LinkedInConversationResponse>.Conflict("A connection request is already being processed.");

        try
        {
            var invitationId = await contacts.SendInvitationAsync(enrollment.Candidate.ExternalId, request.Note?.Trim(), ct);
            await conversations.CompleteInvitationAsync(conversation.Id, invitationId, DeliveryStatus.Sent, CancellationToken.None);
        }
        catch
        {
            await conversations.CompleteInvitationAsync(conversation.Id, null, DeliveryStatus.Unknown, CancellationToken.None);

            throw new ApiException(503, "Connection request outcome is unknown. Check LinkedIn before taking further action; no automatic retry.");
        }

        return await getConversation.HandleAsync(id, ct);
    }
}
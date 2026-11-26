using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
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
    IValidator<SendConnectionRequest> validator,
    IRecruitmentAi ai,
    IUnitOfWork unitOfWork,
    IAuditRepository audit,
    IOptions<PipelineOptions> pipeline)
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

        if (conversation.ConnectionState is "Declined" or "Withdrawn" or "Inactive" ||
            (!status.Connected && status.SentInvitationStatus == ConnectionInvitationStatus.Declined))
            return Result<LinkedInConversationResponse>.Conflict("Connection request cannot be resent. A confirmed withdrawal requires an explicit retry.");

        if (status.Connected || status.InvitationPending || conversation.ConnectionRequestDelivery.HasValue)
            return await getConversation.HandleAsync(id, ct);

        if (conversation.ConnectionNote is null)
        {
            var note = request.Note?.Trim();
            if (request.GenerateNote)
            {
                var campaign = enrollment.Campaign;
                var candidate = enrollment.Candidate;
                note = (await ai.WriteConnectionNoteAsync(
                    new Vacancy(campaign.JobTitle, campaign.Description, campaign.Requirements, campaign.SystemPrompt),
                    new CandidateProfile(candidate.ExternalId, candidate.ProfileUrl, candidate.Name, candidate.Headline, candidate.Location, candidate.Experience),
                    ct))?.Trim();

                if (string.IsNullOrWhiteSpace(note) || note.Length > 200)
                    throw new ApiException(502, "AI connection note must contain 1 to 200 characters.");
            }

            conversation.ConnectionNote = note;
            await unitOfWork.SaveChangesAsync(ct);
        }

        if (!await conversations.TryClaimInvitationAsync(
                conversation.Id,
                enrollment,
                DateTimeOffset.UtcNow,
                pipeline.Value.MinimumSendIntervalSeconds,
                pipeline.Value.MaximumDailyMessagesPerCampaign,
                ct))
            return await getConversation.HandleAsync(id, ct);

        try
        {
            var invitationId = await contacts.SendInvitationAsync(enrollment.Candidate.ExternalId, conversation.ConnectionNote, ct);
            await conversations.CompleteInvitationAsync(conversation.Id, invitationId, DeliveryStatus.Sent, CancellationToken.None);
            conversation.ConnectionState = "Pending";
            audit.Add(enrollment.CampaignId, id, "LinkedInConnectionRequestSent", conversation.ConnectionNote ?? "");
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            await conversations.CompleteInvitationAsync(conversation.Id, null, DeliveryStatus.Unknown, CancellationToken.None);
            conversation.ConnectionState = "ActionRequired";
            var reason = exception is ApiException ? exception.Message : "Check LinkedIn before retrying.";
            audit.Add(enrollment.CampaignId, id, "LinkedInConnectionRequestUnknown", reason);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            throw new ApiException(
                exception is ApiException provider ? provider.Status : 503,
                $"{reason} Connection request delivery is not confirmed; no automatic retry.");
        }

        return await getConversation.HandleAsync(id, ct);
    }
}
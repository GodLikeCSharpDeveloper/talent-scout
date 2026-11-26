using Ardalis.Result;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class SynchronizeLinkedInHandler(
    IEnrollmentRepository enrollments,
    ILinkedInConversationRepository conversations,
    LinkedInSynchronizationService synchronization,
    IOptions<LinkedInOptions> options,
    IOptions<PipelineOptions> pipeline,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    GetLinkedInConversationHandler getConversation)
{
    public async Task<Result<LinkedInConversationResponse>> HandleAsync(Guid id, CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
            return Result<LinkedInConversationResponse>.Unavailable("LinkedIn provider is not configured.");

        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<LinkedInConversationResponse>.NotFound("Enrollment was not found.");

        var conversation = await conversations.ReserveAsync(id, options.Value.AccountId, enrollment.Candidate.ExternalId, ct);

        if (conversation.EnrollmentId != id || conversation.AccountId != options.Value.AccountId)
            return Result<LinkedInConversationResponse>.Conflict("This LinkedIn conversation belongs to another campaign.");
        if (!await conversations.TryAcquireSynchronizationAsync(conversation.Id, DateTimeOffset.UtcNow, ct))
            return Result<LinkedInConversationResponse>.Conflict("Synchronization is already running for this candidate.");

        var lease = conversation.SynchronizationLeaseUntil!.Value;
        var next = DateTimeOffset.UtcNow.AddMinutes(pipeline.Value.SynchronizationIntervalMinutes);
        try
        {
            await synchronization.SynchronizeAsync(enrollment, conversation, ct);
            conversation.LastSynchronizedAt = DateTimeOffset.UtcNow;
            conversation.NextSynchronizationAt = next;
            conversation.SynchronizationError = null;
            conversation.SynchronizationLeaseUntil = null;
            audit.Add(enrollment.CampaignId, id, "LinkedInSynchronized");
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception exception)
        {
            var reason = exception is ApiException ? exception.Message : "LinkedIn synchronization failed. Try again.";
            unitOfWork.Clear();
            await conversations.ReleaseSynchronizationAsync(conversation.Id, lease, next, reason, CancellationToken.None);
            audit.Add(enrollment.CampaignId, id, "LinkedInSynchronizationFailed", reason);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);

            throw;
        }

        return await getConversation.HandleAsync(id, ct);
    }
}
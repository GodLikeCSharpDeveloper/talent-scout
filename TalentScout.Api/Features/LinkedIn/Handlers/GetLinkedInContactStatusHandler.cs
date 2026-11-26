using Ardalis.Result;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class GetLinkedInContactStatusHandler(IEnrollmentRepository enrollments, IOptions<LinkedInOptions> options, ILinkedInContacts contacts, ILinkedInConversationRepository conversations, IUnitOfWork unitOfWork)
{
    public async Task<Result<LinkedInContactStatus>> HandleAsync(Guid id, CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
            return Result<LinkedInContactStatus>.Unavailable("LinkedIn provider is not configured.");

        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<LinkedInContactStatus>.NotFound("Enrollment was not found.");

        var status = await contacts.GetStatusAsync(enrollment.Candidate.ExternalId, ct);
        var conversation = await conversations.FindForEnrollmentAsync(id, ct);
        if (conversation?.ConnectionConfirmedAt is not null)
            status = status with { Connected = true, CanMessage = true, InvitationPending = false };
        if (conversation is not null)
        {
            if (status.Connected && conversation.ConnectionConfirmedAt is null)
                conversation.ConnectionConfirmedAt = DateTimeOffset.UtcNow;
            if (status.Connected)
                conversation.ConnectionState = "Connected";
            if (status.CanMessage && enrollment.NextContactAt is not null)
                enrollment.NextContactAt = null;
            await unitOfWork.SaveChangesAsync(ct);
        }

        return Result<LinkedInContactStatus>.Success(
            status with
            {
                InvitationPending = !status.Connected && (status.InvitationPending || (conversation?.ConnectionState is not ("Inactive" or "Declined" or "Withdrawn") && conversation?.ConnectionRequestDelivery == DeliveryStatus.Sent)),
                ConnectionRequestDelivery = conversation?.ConnectionRequestDelivery
            });
    }
}
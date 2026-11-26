using TalentScout.Api.Features.Conversations.Handlers;
using TalentScout.Api.Features.LinkedIn.Handlers;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;
using IResult = Ardalis.Result.IResult;

namespace TalentScout.Api.Features.Pipeline;

public sealed class StartFirstContact(
    ILinkedInContacts contacts,
    ILinkedInConversationRepository conversations,
    IEnrollmentRepository enrollments,
    IOptions<LinkedInOptions> options,
    SendConnectionRequestHandler connect,
    SendIntroductionHandler introduce)
{
    public async Task<IResult?> HandleAsync(Enrollment enrollment, CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
            throw new ApiException(503, "LinkedIn provider is not configured.");

        var conversation = await conversations.FindForEnrollmentAsync(enrollment.Id, ct);

        if (conversation?.ConnectionState is "Inactive" or "Declined" or "Withdrawn")
            return null;

        // An accepted-connection webhook is authoritative even while the profile endpoint lags.
        if (conversation?.ChatId is not null || conversation?.ConnectionConfirmedAt is not null)
            return await introduce.HandleAsync(enrollment.Id, ct);

        var status = await contacts.GetStatusAsync(enrollment.Candidate.ExternalId, ct);

        if (!status.Connected && status.SentInvitationStatus is ConnectionInvitationStatus.Declined or ConnectionInvitationStatus.Withdrawn)
            return null;

        if (status.CanMessage)
            return await introduce.HandleAsync(enrollment.Id, ct);

        if (status.InvitationPending || conversation?.ConnectionRequestDelivery is not null)
        {
            await enrollments.ScheduleNextAttemptAsync(enrollment.Id, DateTimeOffset.UtcNow.AddHours(6), ct);

            return null;
        }

        var result = await connect.HandleAsync(enrollment.Id, new SendConnectionRequest(GenerateNote: true), ct);
        if (result.IsSuccess && result.Value.ConnectionRequestDelivery is not null)
            await enrollments.ScheduleNextAttemptAsync(enrollment.Id, DateTimeOffset.UtcNow.AddHours(6), ct);

        return result;
    }
}
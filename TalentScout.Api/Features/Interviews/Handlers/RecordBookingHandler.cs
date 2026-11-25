using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Interviews.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Interviews.Handlers;

public sealed class RecordBookingHandler(IEnrollmentRepository enrollments, IInterviewRepository interviews, IAuditRepository audit, IUnitOfWork unitOfWork, IValidator<RecordBookingRequest> validator)
{
    public async Task<Result<Interview>> HandleAsync(RecordBookingRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<Interview>.Invalid(validation.AsErrors());

        var provider = request.Provider.Trim().ToLowerInvariant();
        var externalId = request.ExternalBookingId.Trim();

        var startsAt = new DateTimeOffset(request.StartsAt.UtcTicks / 10 * 10, TimeSpan.Zero);
        var endsAt = new DateTimeOffset(request.EndsAt.UtcTicks / 10 * 10, TimeSpan.Zero);

        var duplicate = await interviews.FindBookingAsync(provider, externalId, ct);
        if (duplicate is not null)
        {
            if (duplicate.EnrollmentId != request.EnrollmentId || duplicate.StartsAt != startsAt || duplicate.EndsAt != endsAt)
                return Result<Interview>.Conflict("Booking ID already belongs to another payload.");

            return Result<Interview>.Success(duplicate);
        }

        var enrollment = await enrollments.FindAsync(request.EnrollmentId, ct);

        if (enrollment is null)
            return Result<Interview>.NotFound("Enrollment was not found.");

        if (!enrollment.HasSentInvitation)
            return Result<Interview>.Conflict("An interview invitation must be confirmed as sent before recording the booking.");
        if (await interviews.ExistsForEnrollmentAsync(request.EnrollmentId, ct))
            return Result<Interview>.Conflict("This candidate already has an interview for this campaign.");

        var interview = new Interview { EnrollmentId = request.EnrollmentId, Provider = provider, ExternalBookingId = externalId, StartsAt = startsAt, EndsAt = endsAt };
        interviews.Add(interview);
        enrollment.Status = CandidateStatus.InterviewBooked;
        audit.Add(enrollment.CampaignId, enrollment.Id, "InterviewBooked");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Interview>.Success(interview);
    }
}
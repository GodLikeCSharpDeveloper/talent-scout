using Ardalis.Result;
using TalentScout.Api.Features.Pipeline.Responses;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class GetEnrollmentStatusHandler(IEnrollmentRepository enrollments)
{
    public async Task<Result<EnrollmentStatusResponse>> HandleAsync(Guid id, CancellationToken ct)
    {
        var enrollment = await enrollments.FindAsync(id, ct);

        if (enrollment is null)
            return Result<EnrollmentStatusResponse>.NotFound("Enrollment was not found.");

        return Result<EnrollmentStatusResponse>.Success(
            new EnrollmentStatusResponse(
                enrollment.Id,
                enrollment.CampaignId,
                enrollment.CandidateId,
                enrollment.Status,
                enrollment.FitScore,
                enrollment.Assessment,
                enrollment.HasSentInitialMessage,
                enrollment.HasSentInvitation,
                enrollment.SearchBatchId,
                enrollment.ApprovalStatus,
                enrollment.ApprovedAt,
                enrollment.OutreachStartedAt));
    }
}
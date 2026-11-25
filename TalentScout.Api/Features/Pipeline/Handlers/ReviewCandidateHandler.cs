using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Features.Pipeline.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class ReviewCandidateHandler(IEnrollmentRepository enrollments, IAuditRepository audit, IUnitOfWork unitOfWork, IValidator<ReviewCandidateRequest> validator)
{
    public async Task<Result<ReviewCandidateResponse>> HandleAsync(Guid id, ReviewCandidateRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<ReviewCandidateResponse>.Invalid(validation.AsErrors());

        var enrollment = await enrollments.FindAsync(id, ct);

        if (enrollment is null)
            return Result<ReviewCandidateResponse>.NotFound("Enrollment was not found.");

        if (enrollment.Status != CandidateStatus.NeedsReview)
            return Result<ReviewCandidateResponse>.Conflict("Only candidates requiring review can receive a manual decision.");

        var reason = request.Reason.Trim();
        enrollment.Status = request.Decision == Interest.NotInterested ? CandidateStatus.NotInterested :
            enrollment.HasSentInvitation ? CandidateStatus.InterviewInvitationSent : CandidateStatus.Interested;
        audit.Add(enrollment.CampaignId, id, "RecruiterDecision", $"{request.Decision}: {reason}");
        await unitOfWork.SaveChangesAsync(ct);

        return Result<ReviewCandidateResponse>.Success(new ReviewCandidateResponse(enrollment.Id, enrollment.Status));
    }
}
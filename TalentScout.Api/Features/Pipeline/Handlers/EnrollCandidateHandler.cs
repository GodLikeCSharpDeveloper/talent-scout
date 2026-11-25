using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Features.Pipeline.Responses;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class EnrollCandidateHandler(CandidateEnroller enroller, IValidator<EnrollCandidateRequest> validator)
{
    public async Task<Result<EnrollmentResponse>> HandleAsync(Guid id, EnrollCandidateRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<EnrollmentResponse>.Invalid(validation.AsErrors());

        var enrollment = await enroller.EnrollAsync(id, request.CandidateId, ct);

        return Result<EnrollmentResponse>.Success(new EnrollmentResponse(enrollment.Id, enrollment.CampaignId, enrollment.CandidateId, enrollment.Status));
    }
}
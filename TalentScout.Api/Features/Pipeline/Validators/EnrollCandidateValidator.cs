using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;

namespace TalentScout.Api.Features.Pipeline.Validators;

public sealed class EnrollCandidateValidator : AbstractValidator<EnrollCandidateRequest>
{
    public EnrollCandidateValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
    }
}
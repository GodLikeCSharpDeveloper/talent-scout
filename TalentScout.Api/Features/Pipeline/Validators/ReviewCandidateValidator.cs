using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Pipeline.Validators;

public sealed class ReviewCandidateValidator : AbstractValidator<ReviewCandidateRequest>
{
    public ReviewCandidateValidator()
    {
        RuleFor(x => x.Decision).Must(x => x is Interest.Interested or Interest.NotInterested)
            .WithMessage("Choose Interested or NotInterested.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
    }
}
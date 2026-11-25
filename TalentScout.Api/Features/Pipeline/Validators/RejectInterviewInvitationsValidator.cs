using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;

namespace TalentScout.Api.Features.Pipeline.Validators;

public sealed class RejectInterviewInvitationsValidator : AbstractValidator<RejectInterviewInvitationsRequest>
{
    public RejectInterviewInvitationsValidator()
    {
        RuleFor(x => x.EnrollmentIds).NotNull().Must(ids => ids is { Count: >= 1 and <= 100 } && ids.Distinct().Count() == ids.Count)
            .WithMessage("Select 1..100 distinct enrollment IDs.");
        RuleForEach(x => x.EnrollmentIds).NotEmpty();
    }
}
using FluentValidation;
using TalentScout.Api.Features.Interviews.Requests;

namespace TalentScout.Api.Features.Interviews.Validators;

public sealed class RecordBookingValidator : AbstractValidator<RecordBookingRequest>
{
    public RecordBookingValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExternalBookingId).NotEmpty().MaximumLength(500);
        RuleFor(x => x.StartsAt).NotEqual(default(DateTimeOffset));
        RuleFor(x => x.EndsAt).Must((request, endsAt) => endsAt.UtcTicks / 10 > request.StartsAt.UtcTicks / 10)
            .WithMessage("EndsAt must be later than StartsAt at PostgreSQL timestamp precision.");
    }
}
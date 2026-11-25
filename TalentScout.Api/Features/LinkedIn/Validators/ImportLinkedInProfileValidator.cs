using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;

namespace TalentScout.Api.Features.LinkedIn.Validators;

public sealed class ImportLinkedInProfileValidator : AbstractValidator<ImportLinkedInProfileRequest>
{
    public ImportLinkedInProfileValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(300);
    }
}
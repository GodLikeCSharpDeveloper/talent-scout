using FluentValidation;
using TalentScout.Api.Features.Campaigns.Requests;

namespace TalentScout.Api.Features.Campaigns.Validators;

public sealed class ChangeStatusValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
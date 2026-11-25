using FluentValidation;
using TalentScout.Api.Features.Conversations.Requests;

namespace TalentScout.Api.Features.Conversations.Validators;

public sealed class ReconcileDeliveryValidator : AbstractValidator<ReconcileDeliveryRequest>
{
    public ReconcileDeliveryValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ExternalMessageId).NotEmpty().MaximumLength(300).When(x => x.WasSent);
    }
}
using FluentValidation;
using TalentScout.Api.Shared.Requests;

namespace TalentScout.Api.Shared.Validation;

public class PaginationValidator<T> : AbstractValidator<T> where T : PaginationRequest
{
    public PaginationValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
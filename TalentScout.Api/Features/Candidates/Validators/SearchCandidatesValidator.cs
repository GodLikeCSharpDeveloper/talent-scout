using FluentValidation;
using TalentScout.Api.Features.Candidates.Requests;

namespace TalentScout.Api.Features.Candidates.Validators;

public sealed class SearchCandidatesValidator : AbstractValidator<SearchCandidatesRequest>
{
    public SearchCandidatesValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
        RuleFor(x => x.MaxProfilesToScan).InclusiveBetween(1, 10000).GreaterThanOrEqualTo(x => x.Limit);
        RuleFor(x => x.Cursor).MaximumLength(32768);
    }
}
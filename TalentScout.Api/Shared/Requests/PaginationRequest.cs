namespace TalentScout.Api.Shared.Requests;

public class PaginationRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
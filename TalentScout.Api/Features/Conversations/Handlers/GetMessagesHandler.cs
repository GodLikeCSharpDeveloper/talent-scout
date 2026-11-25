using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class GetMessagesHandler(IEnrollmentRepository enrollments, IMessageRepository messages, IValidator<PaginationRequest> validator)
{
    public async Task<Result<List<Message>>> HandleAsync(Guid id, PaginationRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<List<Message>>.Invalid(validation.AsErrors());

        var skip = (request.Page - 1) * request.PageSize;
        var take = request.PageSize;

        if (!await enrollments.ExistsAsync(id, ct))
            return Result<List<Message>>.NotFound("Enrollment was not found.");

        return Result<List<Message>>.Success(await messages.GetPageAsync(id, skip, take, ct));
    }
}
using Ardalis.Result;
using TalentScout.Api.Features.Conversations.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Conversations.Handlers;

public sealed class AnalyzeReplyHandler(IEnrollmentRepository enrollments, IMessageRepository messages, ReplyAnalyzer analyzer)
{
    public async Task<Result<AnalyzeReplyResponse>> HandleAsync(Guid id, CancellationToken ct)
    {
        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<AnalyzeReplyResponse>.NotFound("Enrollment was not found.");
        if (enrollment.Campaign.Status != CampaignStatus.Active)
            return Result<AnalyzeReplyResponse>.Conflict("Campaign must be active.");
        if (!enrollment.HasSentInitialMessage)
            return Result<AnalyzeReplyResponse>.Conflict("The introduction must be sent before analyzing a reply.");

        var reply = await messages.GetNextUnanalyzedReplyAsync(id, ct);

        if (reply is null)
            return Result<AnalyzeReplyResponse>.NotFound("No unanalyzed reply exists.");

        await analyzer.AnalyzeAsync(enrollment, reply, ct);

        return Result<AnalyzeReplyResponse>.Success(new AnalyzeReplyResponse(id, reply.Id, enrollment.Status, reply.Interest, reply.Confidence, reply.Analysis));
    }
}
using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.Conversations.Handlers;
using TalentScout.Api.Features.Conversations.Requests;
using TalentScout.Api.Features.Conversations.Responses;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Conversations;

[ApiController]
[TranslateResultToActionResult]
[Route("api/enrollments/{id:guid}/messages")]
public sealed class ConversationsController(
    ReceiveReplyHandler receiveReplyHandler,
    GetMessagesHandler getMessagesHandler,
    ReconcileDeliveryHandler reconcileDeliveryHandler,
    SendIntroductionHandler sendIntroductionHandler,
    AnalyzeReplyHandler analyzeReplyHandler,
    SendInterviewInvitationHandler sendInterviewInvitationHandler) : ControllerBase
{
    [HttpPost("replies")]
    public async Task<Result<Message>> ReceiveReply(Guid id, ReceiveReplyRequest request, CancellationToken ct)
    {
        return await receiveReplyHandler.HandleAsync(id, request, ct);
    }

    [HttpGet]
    public async Task<Result<List<Message>>> GetAll(Guid id, [FromQuery] PaginationRequest request, CancellationToken ct)
    {
        return await getMessagesHandler.HandleAsync(id, request, ct);
    }

    [HttpPost("/api/messages/{id:guid}/reconcile")]
    public async Task<Result<Message>> Reconcile(Guid id, ReconcileDeliveryRequest request, CancellationToken ct)
    {
        return await reconcileDeliveryHandler.HandleAsync(id, request, ct);
    }

    [HttpPost("/api/enrollments/{id:guid}/send-introduction")]
    public async Task<Result<SendMessageResponse>> SendIntroduction(Guid id, CancellationToken ct)
    {
        return await sendIntroductionHandler.HandleAsync(id, ct);
    }

    [HttpPost("/api/enrollments/{id:guid}/analyze-reply")]
    public async Task<Result<AnalyzeReplyResponse>> AnalyzeReply(Guid id, CancellationToken ct)
    {
        return await analyzeReplyHandler.HandleAsync(id, ct);
    }

    [HttpPost("/api/enrollments/{id:guid}/send-interview-invitation")]
    public async Task<Result<SendMessageResponse>> SendInterviewInvitation(Guid id, CancellationToken ct)
    {
        return await sendInterviewInvitationHandler.HandleAsync(id, ct);
    }
}
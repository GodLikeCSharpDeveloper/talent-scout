namespace TalentScout.Api.Features.Conversations.Requests;

public sealed record ReceiveReplyRequest(string ExternalMessageId, string Content);
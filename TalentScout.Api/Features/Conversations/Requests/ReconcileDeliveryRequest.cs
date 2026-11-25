namespace TalentScout.Api.Features.Conversations.Requests;

public sealed record ReconcileDeliveryRequest(bool WasSent, string? ExternalMessageId, string Reason);
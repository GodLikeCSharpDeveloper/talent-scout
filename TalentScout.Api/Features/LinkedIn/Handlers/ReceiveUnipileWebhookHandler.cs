using System.Text.Json;
using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class ReceiveUnipileWebhookHandler(
    ILinkedInWebhookRepository inbox,
    ILinkedInConversationRepository conversations,
    IOptions<LinkedInOptions> options,
    IValidator<UnipileWebhookRequest> validator)
{
    public async Task<Result<UnipileWebhookReceipt>> HandleAsync(UnipileWebhookRequest request, CancellationToken ct)
    {
        if (request.AccountId != options.Value.AccountId)
            return Result<UnipileWebhookReceipt>.Forbidden();

        if (request.Event == "new_relation" && request.AccountType == "LINKEDIN")
        {
            var relationValidation = await validator.ValidateAsync(request, rules => rules.IncludeRuleSets("Relation"), ct);

            if (!relationValidation.IsValid)
                return Result<UnipileWebhookReceipt>.Invalid(relationValidation.AsErrors());
            if (await conversations.FindByRecipientAsync(request.AccountId, request.UserProviderId!, ct) is null)
                return Result<UnipileWebhookReceipt>.Success(new UnipileWebhookReceipt(false, "UnrelatedConnection"));

            var storedRelation = await inbox.TryAddAsync(
                new LinkedInWebhookEvent
                {
                    EventType = "new_relation", AccountId = request.AccountId, SenderId = request.UserProviderId!,
                    ExternalMessageId = UnipileIdentifiers.Message(request.AccountId, "relation:" + request.UserProviderId),
                    OccurredAt = DateTimeOffset.UtcNow
                },
                ct);

            return Result<UnipileWebhookReceipt>.Success(new UnipileWebhookReceipt(storedRelation, storedRelation ? "Stored" : "Duplicate"));
        }

        if (request.Event != "message_received" || request.AccountType != "LINKEDIN" || IsTrue(request.IsEvent) || IsTrue(request.IsGroup))
            return Result<UnipileWebhookReceipt>.Success(new UnipileWebhookReceipt(false, "IgnoredEvent"));

        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<UnipileWebhookReceipt>.Invalid(validation.AsErrors());
        if (IsTrue(request.IsSender) || request.Sender!.ProviderId == request.AccountInfo?.UserId)
            return Result<UnipileWebhookReceipt>.Success(new UnipileWebhookReceipt(false, "OutgoingMessage"));
        if (string.IsNullOrWhiteSpace(request.AccountInfo?.UserId) && !IsFalse(request.IsSender))
            return Result<UnipileWebhookReceipt>.Invalid(new ValidationError("AccountInfo", "Cannot determine message direction."));

        var stored = await inbox.TryAddAsync(
            new LinkedInWebhookEvent
            {
                AccountId = request.AccountId,
                ExternalMessageId = UnipileIdentifiers.Message(request.AccountId, request.MessageId),
                ChatId = request.ChatId,
                ProviderChatId = request.ProviderChatId,
                SenderId = request.Sender.ProviderId,
                Content = request.Message?.Trim() ?? "",
                HasAttachments = request.Attachments is { Count: > 0 },
                OccurredAt = request.Timestamp.ToUniversalTime()
            },
            ct);

        // Acknowledge only after durable storage; no AI or provider calls in the webhook request.
        return Result<UnipileWebhookReceipt>.Success(new UnipileWebhookReceipt(stored, stored ? "Stored" : "Duplicate"));
    }

    private static bool IsTrue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number == 1);
    }

    private static bool IsFalse(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.False ||
               (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number == 0);
    }
}
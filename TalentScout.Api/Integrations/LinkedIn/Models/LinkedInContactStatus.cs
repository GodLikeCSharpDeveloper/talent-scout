using TalentScout.Data.Entities;

namespace TalentScout.Api.Integrations.LinkedIn.Models;

public enum ConnectionInvitationStatus
{
    Pending,
    Declined,
    Withdrawn
}

public sealed record LinkedInContactStatus(
    bool CanMessage,
    bool Connected,
    bool InvitationPending,
    DeliveryStatus? ConnectionRequestDelivery = null,
    ConnectionInvitationStatus? SentInvitationStatus = null);
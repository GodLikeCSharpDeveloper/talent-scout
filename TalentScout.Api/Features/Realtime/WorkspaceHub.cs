using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using TalentScout.Api.Infrastructure.Security;

namespace TalentScout.Api.Features.Realtime;

public sealed class WorkspaceHub : Hub;

public sealed class RealtimeTickets(IDataProtectionProvider protection, IOptions<SecurityOptions> security)
{
    private readonly ITimeLimitedDataProtector protector = protection.CreateProtector("TalentScout.Realtime.v1").ToTimeLimitedDataProtector();
    private string KeyHash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(security.Value.ApiKey)));

    public string Issue()
    {
        return protector.Protect(KeyHash, TimeSpan.FromMinutes(1));
    }

    public bool Validate(string ticket)
    {
        try
        {
            return !string.IsNullOrEmpty(ticket) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(protector.Unprotect(ticket)), Encoding.UTF8.GetBytes(KeyHash));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

public sealed class WorkspaceEvents(IHubContext<WorkspaceHub> hub, ILogger<WorkspaceEvents> logger)
{
    public async Task NotifyAsync(WorkspaceNotification notification)
    {
        try
        {
            await hub.Clients.All.SendAsync("WorkspaceNotification", notification);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Realtime notification failed: {ExceptionType}", ex.GetType().Name);
        }
    }

    public async Task ChangedAsync(string[] entities)
    {
        try
        {
            await hub.Clients.All.SendAsync("WorkspaceChanged", new { entities });
        }
        catch (Exception ex)
        {
            logger.LogWarning("Realtime notification failed: {ExceptionType}", ex.GetType().Name);
        }
    }

    public async Task ProgressAsync(Guid campaignId, Guid? enrollmentId, string operation, string state, int? added = null, int? scanned = null)
    {
        try
        {
            await hub.Clients.All.SendAsync("OperationProgress", new { campaignId, enrollmentId, operation, state, added, scanned, occurredAt = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            logger.LogWarning("Realtime progress failed: {ExceptionType}", ex.GetType().Name);
        }
    }
}
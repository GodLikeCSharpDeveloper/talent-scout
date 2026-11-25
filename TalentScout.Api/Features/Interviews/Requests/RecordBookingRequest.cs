namespace TalentScout.Api.Features.Interviews.Requests;

public sealed record RecordBookingRequest(Guid EnrollmentId, string Provider, string ExternalBookingId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
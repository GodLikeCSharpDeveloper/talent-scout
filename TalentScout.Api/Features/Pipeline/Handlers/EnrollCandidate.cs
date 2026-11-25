using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class CandidateEnroller(
    ICampaignRepository campaigns,
    ICandidateRepository candidates,
    IEnrollmentRepository enrollments,
    IAuditRepository audit,
    IUnitOfWork unitOfWork)
{
    public async Task<Enrollment> EnrollAsync(Guid campaignId, Guid candidateId, CancellationToken ct)
    {
        var created = await EnrollNewAsync(campaignId, candidateId, null, ct);

        return created ?? await enrollments.FindForCandidateAsync(campaignId, candidateId, ct)
            ?? throw ApiException.Conflict("Candidate enrollment changed concurrently.");
    }

    public async Task<Enrollment?> EnrollNewAsync(Guid campaignId, Guid candidateId, Guid? searchBatchId, CancellationToken ct)
    {
        var campaign = await campaigns.FindAsync(campaignId, ct) ?? throw ApiException.NotFound("Campaign");

        if (campaign.Status == CampaignStatus.Stopped)
            throw ApiException.Conflict("Campaign is stopped.");
        if (!await candidates.ExistsAsync(candidateId, ct))
            throw ApiException.NotFound("Candidate");

        var enrollment = new Enrollment { CampaignId = campaignId, CandidateId = candidateId, SearchBatchId = searchBatchId };

        if (!await enrollments.TryAddAsync(enrollment, ct))
            return null;

        audit.Add(campaignId, enrollment.Id, "CandidateEnrolled");
        await unitOfWork.SaveChangesAsync(ct);

        return enrollment;
    }
}
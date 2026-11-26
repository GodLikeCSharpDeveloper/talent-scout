using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Candidates.Requests;
using TalentScout.Api.Features.Candidates.Responses;
using TalentScout.Api.Features.Pipeline.Handlers;
using TalentScout.Api.Features.Realtime;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Candidates.Handlers;

public sealed class SearchCandidatesHandler(
    ICampaignRepository campaigns,
    ISearchBatchRepository batches,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    SearchCandidatePageHandler search,
    ILinkedInProfiles profiles,
    ICandidateRepository candidates,
    CandidateImporter importer,
    CandidateEnroller enroller,
    IValidator<SearchCandidatesRequest> validator,
    ILogger<SearchCandidatesHandler> logger,
    WorkspaceEvents events)
{
    public async Task<Result<SearchCandidatesResponse>> HandleAsync(Guid id, SearchCandidatesRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<SearchCandidatesResponse>.Invalid(validation.AsErrors());

        var campaign = await campaigns.FindAsync(id, ct);

        if (campaign is null)
            return Result<SearchCandidatesResponse>.NotFound("Campaign was not found.");
        if (campaign.Status == CampaignStatus.Stopped)
            return Result<SearchCandidatesResponse>.Conflict("Campaign is stopped.");

        var batch = new CandidateSearchBatch { CampaignId = id, RequestedCount = request.Limit, NextCursor = request.Cursor };
        batches.Add(batch);
        await unitOfWork.SaveChangesAsync(ct);
        await events.ProgressAsync(id, null, "Search", "Running", 0, 0);
        var ids = new List<Guid>();
        var seenCandidates = new HashSet<Guid>();
        var visitedCursors = new HashSet<string>(StringComparer.Ordinal);
        var cursor = request.Cursor;
        var reason = CandidateSearchStopReason.PageLimitReached;
        try
        {
            while (ids.Count < request.Limit && batch.ScannedCount < request.MaxProfilesToScan && batch.PagesScanned < 100)
            {
                ct.ThrowIfCancellationRequested();
                if (!visitedCursors.Add(cursor ?? string.Empty))
                {
                    reason = CandidateSearchStopReason.RepeatedCursor;

                    break;
                }

                var pageLimit = Math.Min(request.Limit - ids.Count, request.MaxProfilesToScan - batch.ScannedCount);
                var page = await search.HandleAsync(campaign.SearchQuery, campaign.Location, pageLimit, cursor, ct, campaign.LocationId);

                if (page is null || page.Profiles is null || page.Profiles.Count > pageLimit)
                    throw new ApiException(502, "LinkedIn returned an invalid search page.");

                batch.PagesScanned++;
                foreach (var reference in page.Profiles)
                {
                    batch.ScannedCount++;
                    var candidate = await candidates.FindByExternalIdAsync(reference.ExternalId, ct);
                    if (candidate is null)
                    {
                        var profile = await profiles.GetProfileAsync(reference.ExternalId, ct);

                        if (profile.ExternalId != reference.ExternalId)
                            throw new ApiException(502, "LinkedIn returned a profile for a different search result.");

                        candidate = await importer.ImportAsync(profile, ct);
                    }

                    if (!seenCandidates.Add(candidate.Id))
                    {
                        batch.SkippedCount++;

                        continue;
                    }

                    var enrollment = await enroller.EnrollNewAsync(id, candidate.Id, batch.Id, ct);
                    if (enrollment is null)
                    {
                        batch.SkippedCount++;

                        continue;
                    }

                    ids.Add(enrollment.Id);
                }

                await events.ProgressAsync(id, null, "Search", "Running", ids.Count, batch.ScannedCount);
                cursor = page.NextCursor;
                batch.NextCursor = cursor;
                if (ids.Count == request.Limit)
                {
                    reason = CandidateSearchStopReason.TargetReached;

                    break;
                }

                if (string.IsNullOrEmpty(cursor))
                {
                    reason = CandidateSearchStopReason.ResultsExhausted;

                    break;
                }

                if (batch.ScannedCount >= request.MaxProfilesToScan)
                    reason = CandidateSearchStopReason.ScanLimitReached;
            }

            batch.AddedCount = ids.Count;
            batch.Status = CandidateSearchBatchStatus.Completed;
            batch.StopReason = reason;
            batch.CompletedAt = DateTimeOffset.UtcNow;
            audit.Add(id, null, "CandidateSearchCompleted", $"Batch={batch.Id}; Added={ids.Count}; Skipped={batch.SkippedCount}; Reason={reason}");
            await unitOfWork.SaveChangesAsync(ct);
            await events.ProgressAsync(id, null, "Search", "Completed", ids.Count, batch.ScannedCount);
            await events.NotifyAsync(new WorkspaceNotification(batch.Id, "SearchCompleted", id, campaign.Name, ids.Count, campaign.AutoApproveCandidates));

            return Result<SearchCandidatesResponse>.Success(
                new SearchCandidatesResponse(
                    batch.Id,
                    ids,
                    request.Limit,
                    ids.Count,
                    batch.SkippedCount,
                    batch.ScannedCount,
                    batch.PagesScanned,
                    cursor,
                    reason));
        }
        catch
        {
            await events.ProgressAsync(id, null, "Search", "Failed", ids.Count, batch.ScannedCount);
            // Individual imports are durable. Record a partial failed batch without replaying failed tracked inserts.
            try
            {
                unitOfWork.Clear();
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var failed = await batches.FindAsync(batch.Id, recovery.Token);
                if (failed is not null)
                {
                    failed.Status = CandidateSearchBatchStatus.Failed;
                    failed.AddedCount = await batches.CountEnrollmentsAsync(batch.Id, recovery.Token);
                    failed.SkippedCount = batch.SkippedCount;
                    failed.ScannedCount = batch.ScannedCount;
                    failed.PagesScanned = batch.PagesScanned;
                    failed.NextCursor = cursor;
                    failed.CompletedAt = DateTimeOffset.UtcNow;
                    audit.Add(id, null, "CandidateSearchFailed", $"Batch={batch.Id}; Added={failed.AddedCount}");
                    await unitOfWork.SaveChangesAsync(recovery.Token);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning("Could not record failed search batch {BatchId}: {ExceptionType}", batch.Id, exception.GetType().Name);
            }

            throw;
        }
    }
}
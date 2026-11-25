using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Api.Features.Pipeline.Handlers;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Features.Pipeline.Responses;

namespace TalentScout.Api.Features.Pipeline;

[ApiController]
[TranslateResultToActionResult]
[Route("api/enrollments")]
public sealed class PipelineController(
    EnrollCandidateHandler enrollCandidateHandler,
    GetCampaignCandidatesHandler getCampaignCandidatesHandler,
    GetEnrollmentStatusHandler getEnrollmentStatusHandler,
    ReviewCandidateHandler reviewCandidateHandler,
    StartOutreachHandler startOutreachHandler,
    ExcludeCandidatesHandler excludeCandidatesHandler,
    ApproveInterviewInvitationsHandler approveInterviewInvitationsHandler,
    RejectInterviewInvitationsHandler rejectInterviewInvitationsHandler) : ControllerBase
{
    [HttpPost("/api/campaigns/{id:guid}/candidates")]
    public async Task<Result<EnrollmentResponse>> Enroll(Guid id, EnrollCandidateRequest request, CancellationToken ct)
    {
        return await enrollCandidateHandler.HandleAsync(id, request, ct);
    }

    [HttpGet("/api/campaigns/{id:guid}/candidates")]
    public async Task<Result<List<CampaignCandidateResponse>>> GetCandidates(Guid id, [FromQuery] GetCampaignCandidatesRequest request, CancellationToken ct)
    {
        return await getCampaignCandidatesHandler.HandleAsync(id, request, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<Result<EnrollmentStatusResponse>> GetStatus(Guid id, CancellationToken ct)
    {
        return await getEnrollmentStatusHandler.HandleAsync(id, ct);
    }

    [HttpPost("{id:guid}/review")]
    public async Task<Result<ReviewCandidateResponse>> Review(Guid id, ReviewCandidateRequest request, CancellationToken ct)
    {
        return await reviewCandidateHandler.HandleAsync(id, request, ct);
    }

    [HttpPost("/api/campaigns/{id:guid}/outreach/start")]
    public async Task<Result<StartOutreachResponse>> StartOutreach(Guid id, StartOutreachRequest request, CancellationToken ct)
    {
        return await startOutreachHandler.HandleAsync(id, request, ct);
    }

    [HttpPost("/api/campaigns/{id:guid}/outreach/exclude")]
    public async Task<Result<ExcludeCandidatesResponse>> ExcludeCandidates(Guid id, ExcludeCandidatesRequest request, CancellationToken ct)
    {
        return await excludeCandidatesHandler.HandleAsync(id, request, ct);
    }

    [HttpPost("/api/campaigns/{id:guid}/interview-invitations/approve")]
    public async Task<Result<InterviewInvitationDecisionResponse>> ApproveInterviewInvitations(Guid id, ApproveInterviewInvitationsRequest request, CancellationToken ct)
    {
        return await approveInterviewInvitationsHandler.HandleAsync(id, request, ct);
    }

    [HttpPost("/api/campaigns/{id:guid}/interview-invitations/reject")]
    public async Task<Result<InterviewInvitationDecisionResponse>> RejectInterviewInvitations(Guid id, RejectInterviewInvitationsRequest request, CancellationToken ct)
    {
        return await rejectInterviewInvitationsHandler.HandleAsync(id, request, ct);
    }
}
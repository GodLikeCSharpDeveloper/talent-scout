using System.Text.Json.Serialization;
using Ardalis.Result.AspNetCore;
using FluentValidation;
using TalentScout.Api.Features.Audit.Handlers;
using TalentScout.Api.Features.Campaigns.Handlers;
using TalentScout.Api.Features.Campaigns.Requests;
using TalentScout.Api.Features.Campaigns.Validators;
using TalentScout.Api.Features.Candidates.Handlers;
using TalentScout.Api.Features.Candidates.Requests;
using TalentScout.Api.Features.Candidates.Validators;
using TalentScout.Api.Features.Conversations.Handlers;
using TalentScout.Api.Features.Conversations.Requests;
using TalentScout.Api.Features.Conversations.Validators;
using TalentScout.Api.Features.Interviews.Handlers;
using TalentScout.Api.Features.Interviews.Requests;
using TalentScout.Api.Features.Interviews.Validators;
using TalentScout.Api.Features.Pipeline;
using TalentScout.Api.Features.Pipeline.Handlers;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Features.Pipeline.Validators;
using TalentScout.Api.Infrastructure.Http;
using TalentScout.Api.Infrastructure.Security;
using TalentScout.Api.Integrations.Ai;
using TalentScout.Api.Integrations.LinkedIn;
using TalentScout.Api.Integrations.Scheduling;
using TalentScout.Api.Integrations.Scheduling.Validators;
using TalentScout.Api.Shared.Requests;
using TalentScout.Api.Shared.Validation;
using TalentScout.Data;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(options => options.AddResultConvention(ResultConventionConfiguration.Configure)).AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddOpenApi(options => options.AddDocumentTransformer<ApiDocumentTransformer>());
        services.AddOptions<SecurityOptions>().BindConfiguration("Security")
            .Validate(x => x.ApiKey.Length >= 32, "Security:ApiKey must contain at least 32 characters.").ValidateOnStart();
        services.AddLinkedInIntegration(configuration);
        services.AddAiIntegration(configuration);
        services.AddOptions<SchedulingOptions>().BindConfiguration("Scheduling");
        services.AddOptions<PipelineOptions>().BindConfiguration("Pipeline")
            .Validate(x => x.PollIntervalSeconds >= 1 && x.BatchSize is >= 1 and <= 100 && x.MinimumSendIntervalSeconds >= 1 && x.MaximumDailyMessagesPerCampaign >= 1, "Invalid pipeline limits.")
            .Validate(x => !x.WorkerEnabled || (configuration["LinkedIn:Provider"] != "Disabled" && configuration["Ai:Provider"] != "Disabled"), "Configure real adapters before enabling the worker.")
            .ValidateOnStart();
        services.AddData(configuration);
        services.AddSingleton<IInterviewScheduler, ConfiguredInterviewScheduler>();
        services.AddScoped<CandidateImporter>();
        services.AddScoped<CandidateEnroller>();
        services.AddScoped<ProfileScreener>();
        services.AddScoped<ReplyAnalyzer>();
        services.AddScoped<IntroductionMessageFactory>();
        services.AddScoped<InterviewInvitationFactory>();
        services.AddScoped<PipelineCoordinator>();
        services.AddScoped<MessageDispatcher>();
        services.AddHostedService<PipelineWorker>();

        services.AddScoped<IValidator<CreateCampaignRequest>, CreateCampaignValidator>();
        services.AddScoped<CreateCampaignHandler>();
        services.AddScoped<ChangeCampaignStatusHandler>();
        services.AddScoped<GetCampaignsHandler>();
        services.AddScoped<GetCampaignHandler>();
        services.AddScoped<ImportCandidateHandler>();
        services.AddScoped<GetCandidatesHandler>();
        services.AddScoped<GetCandidateHandler>();
        services.AddScoped<SearchCandidatesHandler>();
        services.AddScoped<EnrollCandidateHandler>();
        services.AddScoped<GetCampaignCandidatesHandler>();
        services.AddScoped<GetEnrollmentStatusHandler>();
        services.AddScoped<ScreenProfileHandler>();
        services.AddScoped<SendIntroductionHandler>();
        services.AddScoped<AnalyzeReplyHandler>();
        services.AddScoped<SendInterviewInvitationHandler>();
        services.AddScoped<ReviewCandidateHandler>();
        services.AddScoped<ReceiveReplyHandler>();
        services.AddScoped<GetMessagesHandler>();
        services.AddScoped<ReconcileDeliveryHandler>();
        services.AddScoped<RecordBookingHandler>();
        services.AddScoped<GetInterviewsHandler>();
        services.AddScoped<GetAuditEventsHandler>();
        services.AddScoped<IValidator<PaginationRequest>, PaginationValidator<PaginationRequest>>();
        services.AddScoped<IValidator<ImportCandidateRequest>, ImportCandidateValidator>();
        services.AddScoped<IValidator<SearchCandidatesRequest>, SearchCandidatesValidator>();
        services.AddScoped<IValidator<ChangeStatusRequest>, ChangeStatusValidator>();
        services.AddScoped<IValidator<EnrollCandidateRequest>, EnrollCandidateValidator>();
        services.AddScoped<IValidator<ReviewCandidateRequest>, ReviewCandidateValidator>();
        services.AddScoped<IValidator<GetCampaignCandidatesRequest>, GetCampaignCandidatesValidator>();
        services.AddScoped<IValidator<GetInterviewsRequest>, GetInterviewsValidator>();
        services.AddScoped<IValidator<RecordBookingRequest>, RecordBookingValidator>();
        services.AddScoped<IValidator<ReceiveReplyRequest>, ReceiveReplyValidator>();
        services.AddScoped<IValidator<ReconcileDeliveryRequest>, ReconcileDeliveryValidator>();
        services.AddScoped<IValidator<Message>, MessageContentValidator>();
        services.AddSingleton<IValidator<string>, InterviewUrlValidator>();
        services.AddScoped<SetApprovalSettingsHandler>();
        services.AddScoped<GetSearchBatchesHandler>();
        services.AddScoped<StartOutreachHandler>();
        services.AddScoped<ExcludeCandidatesHandler>();
        services.AddScoped<IValidator<StartOutreachRequest>, StartOutreachValidator>();
        services.AddScoped<IValidator<ExcludeCandidatesRequest>, ExcludeCandidatesValidator>();
        services.AddHealthChecks();

        return services;
    }
}
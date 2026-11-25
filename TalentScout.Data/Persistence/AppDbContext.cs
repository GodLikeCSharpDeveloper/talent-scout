using Microsoft.EntityFrameworkCore;
using TalentScout.Data.Entities;

namespace TalentScout.Data.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CandidateSearchBatch> SearchBatches => Set<CandidateSearchBatch>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<CandidateSearchBatch>().HasIndex(x => new { x.CampaignId, x.CreatedAt });
        model.Entity<CandidateSearchBatch>().HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrollment>().HasOne<CandidateSearchBatch>().WithMany().HasForeignKey(x => x.SearchBatchId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrollment>().HasIndex(x => new { x.CampaignId, x.ApprovalStatus, x.Status });
        model.Entity<Enrollment>().HasIndex(x => new { x.CampaignId, x.InvitationApprovalStatus, x.Status });
        model.Entity<Candidate>().HasIndex(x => x.ExternalId).IsUnique();
        model.Entity<Candidate>().HasIndex(x => x.ProfileUrl).IsUnique();
        model.Entity<Candidate>().Property(x => x.ExternalId).HasMaxLength(300);
        model.Entity<Candidate>().Property(x => x.ProfileUrl).HasMaxLength(2000);
        model.Entity<Enrollment>().HasIndex(x => new { x.CampaignId, x.CandidateId }).IsUnique();
        model.Entity<Enrollment>().HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrollment>().HasOne(x => x.Candidate).WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Message>().HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Message>().HasIndex(x => x.ExternalId).IsUnique();
        model.Entity<Message>().HasIndex(x => new { x.EnrollmentId, x.Kind }).IsUnique().HasFilter("\"Kind\" <> 2");
        model.Entity<Interview>().HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Interview>().HasIndex(x => new { x.Provider, x.ExternalBookingId }).IsUnique();
        model.Entity<Interview>().HasIndex(x => x.EnrollmentId).IsUnique();
        model.Entity<AuditEvent>().HasIndex(x => new { x.CampaignId, x.OccurredAt });
        model.Entity<AuditEvent>().HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrollment>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<Campaign>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<Message>().Property(x => x.Version).IsConcurrencyToken();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Modified))
        {
            if (entry.Entity is Enrollment e)
                e.Version = Guid.NewGuid();
            if (entry.Entity is Campaign c)
                c.Version = Guid.NewGuid();
            if (entry.Entity is Message m)
                m.Version = Guid.NewGuid();
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
namespace TalentScout.Data.Repositories;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
    void Clear();
}
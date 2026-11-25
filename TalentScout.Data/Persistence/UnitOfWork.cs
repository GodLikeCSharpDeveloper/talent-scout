using Microsoft.EntityFrameworkCore;
using Npgsql;
using TalentScout.Data.Exceptions;
using TalentScout.Data.Repositories;

namespace TalentScout.Data.Persistence;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConflictException("The resource changed concurrently. Reload it before retrying.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" or "23503" })
        {
            throw new PersistenceConflictException("A duplicate record or conflicting relationship already exists.", exception);
        }
    }

    public void Clear()
    {
        db.ChangeTracker.Clear();
    }
}
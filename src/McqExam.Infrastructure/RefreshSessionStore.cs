using McqExam.Application;
using McqExam.Domain;
using Microsoft.EntityFrameworkCore;

namespace McqExam.Infrastructure;

public sealed class RefreshSessionStore(ExamDb db) : IRefreshSessionStore
{
    public async Task Create(RefreshSession session, DateTime now, CancellationToken ct)
    {
        await db.RefreshSessions.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(ct);
    }

    public Task<RefreshSession?> Find(string hash, DateTime now, CancellationToken ct) =>
        db.RefreshSessions.AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash && x.ExpiresAt > now && x.User.IsActive, ct);

    public async Task<bool> Rotate(Guid id, string oldHash, string newHash, DateTime now, CancellationToken ct) =>
        await db.RefreshSessions.Where(x => x.Id == id && x.TokenHash == oldHash && x.ExpiresAt > now && x.User.IsActive)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.TokenHash, newHash), ct) == 1;

    public Task Revoke(string hash, CancellationToken ct) =>
        db.RefreshSessions.Where(x => x.TokenHash == hash).ExecuteDeleteAsync(ct);

    public Task<bool> IsActive(Guid id, int userId, DateTime now, CancellationToken ct) =>
        db.RefreshSessions.AnyAsync(x => x.Id == id && x.UserId == userId && x.ExpiresAt > now && x.User.IsActive, ct);
}

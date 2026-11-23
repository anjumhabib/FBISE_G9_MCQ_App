using McqExam.Application;
using McqExam.Domain;
using Microsoft.EntityFrameworkCore;

namespace McqExam.Infrastructure;

public sealed class UserVisitStore(ExamDb db) : IUserVisitStore
{
    public async Task Save(IReadOnlyList<UserVisitLog> visits, CancellationToken ct)
    {
        var userIds = visits.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var students = (await db.Users.Where(x => userIds.Contains(x.Id) && x.Role == UserRole.Student)
            .Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var entries = visits.Where(x => x.UserId is not { } id || students.Contains(id)).ToArray();
        db.UserVisitLogs.AddRange(entries);
        await db.SaveChangesAsync(ct);

        var logins = entries.Where(x => x.EventType == UserVisitEventType.Login && x.UserId.HasValue)
            .GroupBy(x => x.UserId!.Value);
        foreach (var login in logins)
        {
            var seen = login.Max(x => x.VisitedAt);
            await db.Users.Where(x => x.Id == login.Key && (x.LastLoginAt == null || x.LastLoginAt < seen))
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.LastLoginAt, seen), ct);
        }
        await transaction.CommitAsync(ct);
    }
}

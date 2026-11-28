using McqExam.Application;
using McqExam.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace McqExam.Infrastructure;

public sealed class UserStore(ExamDb db) : IUserStore
{
    public Task<User?> Find(string email, CancellationToken ct) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email, ct);

    public Task<bool> IsActiveAdmin(int id, CancellationToken ct) =>
        db.Users.AnyAsync(x => x.Id == id && x.Role == UserRole.Admin && x.IsActive, ct);

    public async Task<bool> Create(User user, CancellationToken ct)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            return false;
        }
    }
}

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

public sealed class ExamStore(ExamDb db) : IExamStore
{
    public async Task<IReadOnlyList<SubjectInfo>> Subjects(CancellationToken ct) =>
        await db.Subjects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id)
            .Select(x => new SubjectInfo(x.Id, x.Name, x.Code,
                x.Chapters.OrderBy(c => c.ChapterNo)
                    .Select(c => new ChapterInfo(c.Id, c.ChapterNo, c.Title)).ToArray()))
            .ToListAsync(ct);

    public async Task<bool> ValidFilter(ExamFilter filter, CancellationToken ct) =>
        await db.Subjects.Where(x => x.Id == filter.SubjectId && x.IsActive)
            .SelectMany(x => x.Chapters).CountAsync(x => filter.ChapterIds.Contains(x.Id), ct) == filter.ChapterIds.Length;

    private IQueryable<Question> Matching(ExamFilter filter) => db.Questions.Where(x =>
        x.Chapter.SubjectId == filter.SubjectId && filter.ChapterIds.Contains(x.ChapterId) &&
        (filter.BloomLevel == null || x.BloomLevel == filter.BloomLevel) && (filter.IncludeDiagrams || !x.HasDiagram));

    public Task<List<Question>> Select(int userId, ExamFilter filter, int count, CancellationToken ct)
    {
        var completed = from answer in db.ExamAnswers
                        join attempt in db.ExamAttempts on answer.ExamAttemptId equals attempt.Id
                        where attempt.UserId == userId && attempt.CompletedAt != null
                        orderby attempt.CompletedAt descending, attempt.Id descending
                        select answer;
        return Matching(filter).Include(x => x.Chapter)
            .OrderBy(question => completed.Where(answer => answer.QuestionId == question.Id)
                .Select(answer => answer.IsCorrect ? 2 : 1).FirstOrDefault())
            .ThenBy(_ => EF.Functions.Random()).Take(count).ToListAsync(ct);
    }

    public async Task Create(ExamAttempt attempt, CancellationToken ct)
    {
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);
        await db.Entry(attempt).Reference(x => x.Subject).LoadAsync(ct);
    }

    private IQueryable<ExamAttempt> WithAnswers() => db.ExamAttempts.Include(x => x.Subject).Include(x => x.Finalization)
        .Include(x => x.Answers).ThenInclude(x => x.Question).ThenInclude(x => x.Chapter);

    public Task<ExamAttempt?> Find(Guid id, int userId, CancellationToken ct) =>
        WithAnswers().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

    public async Task<bool> SaveDraft(Guid id, int userId, AnswerInput answer, CancellationToken ct) =>
        await db.ExamAnswers.Where(x => x.ExamAttemptId == id && x.QuestionId == answer.QuestionId &&
                db.ExamAttempts.Any(attempt => attempt.Id == id && attempt.UserId == userId && attempt.CompletedAt == null))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.SelectedOption, answer.SelectedOption), ct) == 1;

    public async Task<List<(Guid Id, int UserId)>> Overdue(DateTime now, int limit, CancellationToken ct)
    {
        var rows = await db.TimedExamDeadlines.AsNoTracking().Where(x => x.FinalizeAfter < now)
            .OrderBy(x => x.FinalizeAfter).ThenBy(x => x.AttemptId).Take(limit)
            .Join(db.ExamAttempts, deadline => deadline.AttemptId, attempt => attempt.Id,
                (deadline, attempt) => new { attempt.Id, attempt.UserId }).ToListAsync(ct);
        return rows.Select(x => (x.Id, x.UserId)).ToList();
    }

    public async Task<T> WithinSubmission<T>(Func<Task<T>> action, CancellationToken ct)
    {
        // Reserve the SQLite writer before reading the answers to grade.
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task SaveSubmission(ExamAttempt attempt, CancellationToken ct)
    {
        if (attempt.Finalization is { } deadline) db.TimedExamDeadlines.Remove(deadline);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<ExamAttempt>> History(int userId, CancellationToken ct) =>
        WithAnswers().AsNoTracking().Where(x => x.UserId == userId && x.CompletedAt != null)
            .OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
}

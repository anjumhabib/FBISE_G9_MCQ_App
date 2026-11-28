using McqExam.Domain;

namespace McqExam.Application;

public interface IUserStore
{
    Task<User?> Find(string email, CancellationToken ct);
    Task<bool> Create(User user, CancellationToken ct);
    Task<bool> IsActiveAdmin(int id, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ISessionIssuer
{
    AuthSession Issue(User user, Guid sessionId, DateTime sessionExpiresAt);
}

public interface IExamStore
{
    Task<IReadOnlyList<SubjectInfo>> Subjects(CancellationToken ct);
    Task<bool> ValidFilter(ExamFilter filter, CancellationToken ct);
    Task<List<Question>> Select(int userId, ExamFilter filter, int count, CancellationToken ct);
    Task Create(ExamAttempt attempt, CancellationToken ct);
    Task<ExamAttempt?> Find(Guid id, int userId, CancellationToken ct);
    Task<bool> SaveDraft(Guid id, int userId, AnswerInput answer, CancellationToken ct);
    Task<List<(Guid Id, int UserId)>> Overdue(DateTime now, int limit, CancellationToken ct);
    Task<T> WithinSubmission<T>(Func<Task<T>> action, CancellationToken ct);
    Task SaveSubmission(ExamAttempt attempt, CancellationToken ct);
    Task<List<ExamAttempt>> History(int userId, CancellationToken ct);
}

public sealed class RequestFailure(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

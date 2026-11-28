using System.Security.Cryptography;
using System.Text;
using McqExam.Domain;

namespace McqExam.Application;

public interface IRefreshSessionStore
{
    Task Create(RefreshSession session, DateTime now, CancellationToken ct);
    Task<RefreshSession?> Find(string hash, DateTime now, CancellationToken ct);
    Task<bool> Rotate(Guid id, string oldHash, string newHash, DateTime now, CancellationToken ct);
    Task Revoke(string hash, CancellationToken ct);
    Task<bool> IsActive(Guid id, int userId, DateTime now, CancellationToken ct);
}

public sealed record RenewedSession(AuthSession Access, string Token, DateTime ExpiresAt);

public sealed class RefreshSessions(IRefreshSessionStore store, ISessionIssuer issuer, TimeProvider clock)
{
    public async Task<RenewedSession> Start(User user, string? previousToken, CancellationToken ct)
    {
        await Logout(previousToken, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var token = NewToken();
        var session = new RefreshSession
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Hash(token), ExpiresAt = now.AddHours(24)
        };
        await store.Create(session, now, ct);
        return new(issuer.Issue(user, session.Id, session.ExpiresAt), token, session.ExpiresAt);
    }

    public async Task<RenewedSession?> Renew(string? presented, CancellationToken ct)
    {
        if (presented is not { Length: 64 }) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Hash(presented);
        var session = await store.Find(hash, now, ct);
        if (session is null) return null;

        var token = NewToken();
        return await store.Rotate(session.Id, hash, Hash(token), now, ct)
            ? new(issuer.Issue(session.User, session.Id, session.ExpiresAt), token, session.ExpiresAt) : null;
    }

    public Task Logout(string? token, CancellationToken ct) =>
        token is { Length: 64 } ? store.Revoke(Hash(token), ct) : Task.CompletedTask;

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

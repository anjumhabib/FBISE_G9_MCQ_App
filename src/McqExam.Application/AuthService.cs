using System.Text;
using McqExam.Domain;

namespace McqExam.Application;

public sealed class AuthService(IUserStore users, IPasswordHasher passwords, ISessionIssuer sessions, TimeProvider clock)
{
    public async Task<AuthSession> Register(RegisterRequest request, CancellationToken ct)
    {
        var name = request.FullName.Trim();
        if (name.Length < 2)
            throw new RequestFailure("Enter a full name with at least two characters.");
        if (Encoding.UTF8.GetByteCount(request.Password) > 72)
            throw new RequestFailure("The password exceeds the 72-byte limit.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.Find(email, ct) is not null)
            throw new RequestFailure("An account already exists for this email.", 409);
        var user = new User
        {
            FullName = name, Email = email, PasswordHash = passwords.Hash(request.Password),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        if (!await users.Create(user, ct))
            throw new RequestFailure("An account already exists for this email.", 409);
        return sessions.Issue(user);
    }

    public async Task<AuthSession> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.Find(request.Email.Trim().ToLowerInvariant(), ct);
        const string dummyHash = "$2a$11$atnp9EBOw2npwHBHQmQEZeQroaHr.QPqAfLD8m2FKB6lA3/ZBOblG";
        if (Encoding.UTF8.GetByteCount(request.Password) > 72 ||
            !passwords.Verify(request.Password, user?.PasswordHash ?? dummyHash) || user is null)
            throw new RequestFailure("The email or password is incorrect.", 401);
        return sessions.Issue(user);
    }
}

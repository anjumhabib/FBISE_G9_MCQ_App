using System.Text;
using McqExam.Domain;

namespace McqExam.Application;

public sealed class AuthService(IUserStore users, IPasswordHasher passwords, TimeProvider clock)
{
    public async Task<User> Register(RegisterRequest request, CancellationToken ct)
    {
        var name = request.FullName.Trim();
        if (name.Length < 2)
            throw new RequestFailure("Enter a full name with at least two characters.");
        var mobileNo = request.MobileNo.Trim();
        var phone = mobileNo.StartsWith('+') ? mobileNo[1..] : mobileNo;
        if (phone.Count(char.IsAsciiDigit) is < 7 or > 15 ||
            phone.Any(x => !char.IsAsciiDigit(x) && x is not (' ' or '-' or '(' or ')' or '.')))
            throw new RequestFailure("Enter a valid mobile number with 7 to 15 digits.");
        var schoolName = request.SchoolName.Trim();
        if (schoolName.Length == 0)
            throw new RequestFailure("Enter your school name.");
        if (Encoding.UTF8.GetByteCount(request.Password) > 72)
            throw new RequestFailure("The password exceeds the 72-byte limit.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.Find(email, ct) is not null)
            throw new RequestFailure("An account already exists for this email.", 409);
        var user = new User
        {
            FullName = name, MobileNo = mobileNo, SchoolName = schoolName,
            Email = email, PasswordHash = passwords.Hash(request.Password),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        if (!await users.Create(user, ct))
            throw new RequestFailure("An account already exists for this email.", 409);
        return user;
    }

    public async Task<User> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.Find(request.Email.Trim().ToLowerInvariant(), ct);
        const string dummyHash = "$2a$11$atnp9EBOw2npwHBHQmQEZeQroaHr.QPqAfLD8m2FKB6lA3/ZBOblG";
        if (Encoding.UTF8.GetByteCount(request.Password) > 72 ||
            !passwords.Verify(request.Password, user?.PasswordHash ?? dummyHash) || user is not { IsActive: true })
            throw new RequestFailure("The email or password is incorrect.", 401);
        return user;
    }
}

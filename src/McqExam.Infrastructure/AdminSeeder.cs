using System.Text;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.EntityFrameworkCore;

namespace McqExam.Infrastructure;

public sealed class AdminSeeder(ExamDb db, IPasswordHasher passwords, TimeProvider clock)
{
    public async Task Seed(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        email = email.Trim().ToLowerInvariant();
        if (Encoding.UTF8.GetByteCount(password) > 72)
            throw new InvalidOperationException("Admin__Password exceeds the 72-byte limit.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (user is null)
        {
            db.Users.Add(new User
            {
                FullName = "Administrator", Email = email, PasswordHash = passwords.Hash(password),
                Role = UserRole.Admin, CreatedAt = clock.GetUtcNow().UtcDateTime
            });
        }
        else if (user.Role != UserRole.Admin)
        {
            if (!passwords.Verify(password, user.PasswordHash))
                throw new InvalidOperationException("Admin__Password must match the existing account password to promote it.");
            user.Role = UserRole.Admin;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}

using System.Data.Common;
using McqExam.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace McqExam.Infrastructure;

public sealed class ExamDb(DbContextOptions<ExamDb> options) : DbContext(options)
{
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamAnswer> ExamAnswers => Set<ExamAnswer>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Subject>().HasIndex(x => x.Code).IsUnique();
        model.Entity<Subject>().HasData(
            new Subject { Id = 1, Name = "Biology", Code = "BIO" },
            new Subject { Id = 2, Name = "Chemistry", Code = "CHEM" },
            new Subject { Id = 3, Name = "Physics", Code = "PHY" },
            new Subject { Id = 4, Name = "Mathematics", Code = "MATH" },
            new Subject { Id = 5, Name = "Computer Science", Code = "CS" });
        model.Entity<Chapter>().HasIndex(x => new { x.SubjectId, x.ChapterNo }).IsUnique();
        model.Entity<Question>().HasIndex(x => new { x.ChapterId, x.QNo }).IsUnique();
        model.Entity<Question>().Property(x => x.BloomLevel).HasConversion<string>();
        model.Entity<Question>().HasOne(x => x.Chapter).WithMany(x => x.Questions).HasForeignKey(x => x.ChapterId);
        model.Entity<User>().HasIndex(x => x.Email).IsUnique();
        model.Entity<ExamAttempt>().Property(x => x.Mode).HasConversion<string>();
        model.Entity<ExamAttempt>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        model.Entity<ExamAttempt>().Property(x => x.CompletedAt).IsConcurrencyToken();
        model.Entity<ExamAttempt>().HasIndex(x => new { x.UserId, x.CompletedAt });
        model.Entity<ExamAnswer>().HasIndex(x => new { x.ExamAttemptId, x.QuestionId }).IsUnique();
        model.Entity<ExamAnswer>().HasIndex(x => new { x.ExamAttemptId, x.Position }).IsUnique();
        model.Entity<ExamAnswer>().Property(x => x.BloomLevel).HasConversion<string>();
        model.Entity<ExamAnswer>().HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SqliteConnectionSettings : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

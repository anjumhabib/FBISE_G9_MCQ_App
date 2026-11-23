using System.Data.Common;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace McqExam.Infrastructure;

public sealed class ExamDb(DbContextOptions<ExamDb> options) : DbContext(options)
{
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserVisitLog> UserVisitLogs => Set<UserVisitLog>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamAnswer> ExamAnswers => Set<ExamAnswer>();
    public DbSet<AdGlobalSettings> AdGlobalSettings => Set<AdGlobalSettings>();
    public DbSet<AdPlacement> AdPlacements => Set<AdPlacement>();
    public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();

    [DbFunction("mcq_visit_device", IsBuiltIn = true)]
    internal static DeviceType VisitDevice(string? userAgent) => ClientDevice.Classify(userAgent);

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
        model.Entity<User>().Property(x => x.MobileNo).HasMaxLength(32);
        model.Entity<User>().Property(x => x.SchoolName).HasMaxLength(150);
        model.Entity<User>().Property(x => x.Role).HasConversion<string>().HasDefaultValue(UserRole.Student);
        model.Entity<User>().Property(x => x.IsActive).HasDefaultValue(true);
        model.Entity<UserVisitLog>().Property(x => x.EventType).HasConversion<string>();
        model.Entity<UserVisitLog>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        model.Entity<UserVisitLog>().HasIndex(x => x.VisitedAt);
        model.Entity<UserVisitLog>().HasIndex(x => x.UserId);
        model.Entity<UserVisitLog>().HasIndex(x => x.EventType);
        model.Entity<ExamAttempt>().Property(x => x.Mode).HasConversion<string>();
        model.Entity<ExamAttempt>().Property(x => x.Device).HasConversion<string>().HasDefaultValue(DeviceType.Unknown);
        model.Entity<ExamAttempt>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        model.Entity<ExamAttempt>().Property(x => x.CompletedAt).IsConcurrencyToken();
        model.Entity<ExamAttempt>().HasIndex(x => new { x.UserId, x.CompletedAt });
        model.Entity<ExamAnswer>().HasIndex(x => new { x.ExamAttemptId, x.QuestionId }).IsUnique();
        model.Entity<ExamAnswer>().HasIndex(x => new { x.ExamAttemptId, x.Position }).IsUnique();
        model.Entity<ExamAnswer>().Property(x => x.BloomLevel).HasConversion<string>();
        model.Entity<ExamAnswer>().HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
        AdModelConfiguration.Configure(model);
        model.Entity<ContactInquiry>().Property(x => x.Name).HasMaxLength(100);
        model.Entity<ContactInquiry>().Property(x => x.Email).HasMaxLength(254);
        model.Entity<ContactInquiry>().Property(x => x.Message).HasMaxLength(2000);
    }
}

public sealed class SqliteConnectionSettings : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        RegisterDeviceFunction(connection);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RegisterDeviceFunction(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void RegisterDeviceFunction(DbConnection connection)
    {
        if (connection is SqliteConnection sqlite)
            sqlite.CreateFunction<string?, int>("mcq_visit_device", agent => (int)ClientDevice.Classify(agent),
                isDeterministic: true);
    }
}

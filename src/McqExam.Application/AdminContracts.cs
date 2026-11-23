using System.ComponentModel.DataAnnotations;
using McqExam.Domain;

namespace McqExam.Application;

public class AdminExportFilter
{
    [RegularExpression("(?i)^csv$")] public string? Export { get; init; }
    public bool ExportCsv => string.Equals(Export, "csv", StringComparison.OrdinalIgnoreCase);
}

public class AdminReportFilter : AdminExportFilter, IValidatableObject
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)] public int? SubjectId { get; init; }
    [Range(1, int.MaxValue)] public int? ChapterId { get; init; }
    [Range(1, int.MaxValue)] public int? StudentId { get; init; }
    public DeviceType? Device { get; init; }
    [StringLength(250)] public string? Search { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 25;

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromDate > ToDate)
            yield return new("FromDate must be on or before ToDate.", [nameof(FromDate), nameof(ToDate)]);
    }
}

public sealed class AdminStudentFilter : AdminReportFilter
{
    public bool AtRisk { get; init; }
    public int? InactiveDays { get; init; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;
        if (InactiveDays is not (null or 7 or 14 or 30))
            yield return new("InactiveDays must be 7, 14, or 30.", [nameof(InactiveDays)]);
    }
}

public sealed class AdminQuestionFilter : AdminReportFilter
{
    public BloomLevel? BloomLevel { get; init; }
    [Range(typeof(decimal), "0", "100")] public decimal? MaxAccuracy { get; init; }
    public bool SuspectedWrongKey { get; init; }
    public bool NeverAttempted { get; init; }
}

public sealed class AdminVisitFilter : AdminReportFilter
{
    public UserVisitEventType? EventType { get; init; }
}

public sealed record AdminPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems);
public sealed record AdminDeviceCounts(int Mobile = 0, int Desktop = 0, int Tablet = 0, int Bot = 0, int Unknown = 0);
public sealed record AdminStudentIdentity(int Id, string FullName, string Email, DateTime CreatedAt, DeviceType LastVisitDevice);
public sealed record AdminAttemptRow(Guid Id, int StudentId, string StudentName, string Email, int SubjectId,
    string Subject, ExamMode Mode, int TotalQuestions, int CorrectCount, decimal ScorePercent,
    int TimeSpentSeconds, DateTime StartedAt, DateTime? CompletedAt, DeviceType Device);
public sealed record AdminStudentRow(AdminStudentIdentity Student, bool IsActive, DateTime? LastLoginAt,
    int TotalTests, int CompletedTests, int AbandonedTests, decimal? AverageScorePercent,
    string? BestSubject, string? WeakestSubject, long TotalTimeSpentSeconds, bool AtRisk, AdminDeviceCounts AttemptDevices);
public sealed record AdminStudentSubjectRow(AdminStudentIdentity Student, int SubjectId, string Subject,
    int Attempts, int CompletedAttempts, decimal? AverageScorePercent, decimal? HighestScorePercent,
    decimal? LowestScorePercent, decimal? AverageSecondsPerQuestion, AdminDeviceCounts AttemptDevices);
public sealed record AdminChapterRow(int ChapterId, int ChapterNo, string Title, Accuracy Accuracy, AdminDeviceCounts AnswerDevices);
public sealed record AdminSubjectRow(int SubjectId, string Subject, int Attempts, int UniqueStudents,
    decimal? AverageScorePercent, decimal? PassRatePercent, IReadOnlyList<BloomResult> Bloom,
    IReadOnlyList<AdminChapterRow> WeakestChapters, AdminDeviceCounts AttemptDevices);
public sealed record AdminDailyAttempts(DateOnly Date, int Attempts);
public sealed record AdminDashboard(int TotalStudents, int ActiveStudentsLast7Days, int ActiveStudentsLast30Days,
    int TotalAttempts, decimal? OverallAverageScorePercent, int TotalQuestions, int AbandonedTests,
    IReadOnlyList<AdminDailyAttempts> DailyAttempts, IReadOnlyList<SubjectAverage> SubjectAverages,
    AdminPage<AdminAttemptRow> RecentAttempts, AdminDeviceCounts AttemptDevices);
public sealed record AdminAttemptChapter(int Id, int ChapterNo, string Title);
public enum AdminAnswerStatus { Correct, Incorrect, Skipped }
public sealed record AdminAttemptQuestion(ExamQuestion Question, char? SelectedOption, char CorrectOption,
    AdminAnswerStatus Status);
public sealed record AdminAttemptDetail(AdminStudentIdentity Student, AdminAttemptRow Attempt,
    IReadOnlyList<AdminAttemptChapter> Chapters, IReadOnlyList<BloomResult> Bloom,
    IReadOnlyList<AdminAttemptQuestion> Questions);

public sealed record AdminQuestionItem(int Id, int SubjectId, string Subject, int ChapterId, int ChapterNo,
    string Chapter, int QNo, string QuestionText, BloomLevel BloomLevel);
public sealed record AdminOptionDistribution(decimal A, decimal B, decimal C, decimal D);
public sealed record AdminQuestionRow(AdminQuestionItem Question, char CorrectOption, int TimesServed,
    int CompletedPresentations, int CorrectCount, decimal? AccuracyPercent, int SkippedCount,
    AdminOptionDistribution OptionDistribution, bool SuspectedWrongKey, AdminDeviceCounts PresentationDevices);
public sealed record AdminDuplicateItem(AdminQuestionItem Question, int AttemptCount, AdminDeviceCounts PresentationDevices);
public sealed record AdminDuplicateGroup(string NormalizedText, bool HasExactDuplicates,
    IReadOnlyList<AdminDuplicateItem> Questions);
public sealed record AdminMediaIssue(AdminQuestionItem Question, bool HasDiagram, string? ImagePath,
    bool BrokenDiagram, IReadOnlyList<string> EmptyOptions, IReadOnlyList<string> TruncatedOptions,
    AdminDeviceCounts PresentationDevices);
public sealed record AdminAnomaly(AdminAttemptRow Attempt, bool Abandoned, bool FastSubmission,
    decimal? SecondsPerQuestion);

public sealed record AdminVisitRow(int Id, DateTime VisitedAt, int? StudentId, string? StudentName,
    UserVisitEventType EventType, string Path, string? IpAddress, DeviceType Device, string Browser);
public sealed record AdminDailyVisitors(DateOnly Date, int UniqueVisitors);
public sealed record AdminHourlyTraffic(int Hour, int Visits);
public sealed record AdminWeekdayTraffic(DayOfWeek Day, int Visits);
public sealed record AdminActiveStudent(int StudentId, string StudentName, int Visits, AdminDeviceCounts VisitDevices);
public sealed record AdminVisitsReport(IReadOnlyList<AdminDailyVisitors> DailyUniqueVisitors, int TotalLogins,
    IReadOnlyList<AdminHourlyTraffic> HourlyTraffic, IReadOnlyList<AdminWeekdayTraffic> WeekdayTraffic,
    IReadOnlyList<AdminActiveStudent> TopActiveStudents, AdminPage<AdminVisitRow> Visits, AdminDeviceCounts VisitDevices);

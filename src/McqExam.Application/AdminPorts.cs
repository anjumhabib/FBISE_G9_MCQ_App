using McqExam.Domain;

namespace McqExam.Application;

public sealed record AdminStudentData(AdminStudentIdentity Student, bool IsActive, DateTime? LastLoginAt);
public sealed record AdminChapterData(int Id, int ChapterNo, string Title);
public sealed record AdminSubjectData(int Id, string Name, IReadOnlyList<AdminChapterData> Chapters);
public sealed record AdminAnswerTotals(int SubjectId, int ChapterId, BloomLevel BloomLevel, DeviceType Device, int Total, int Correct);
public sealed record AdminQuestionData(AdminQuestionItem Question, char CorrectOption, bool HasDiagram,
    string? ImagePath, string[] Options);
public sealed record AdminQuestionTotals(int QuestionId, int TimesServed, int Completed, int Correct, int Skipped,
    int A, int B, int C, int D, AdminDeviceCounts PresentationDevices);
public sealed record AdminQuestionChoice(int QuestionId, char? Option, int Students);
public sealed record AdminAttemptData(ExamAttempt Attempt, AdminAttemptRow Row, AdminStudentIdentity Student,
    IReadOnlyList<AdminChapterData> Chapters);
public sealed record AdminVisitData(int Id, DateTime VisitedAt, int? StudentId, string? StudentName,
    UserVisitEventType EventType, string Path, string? IpAddress, string? UserAgent);
public sealed record AdminVisitActivity(DateTime Day, int Hour, int? StudentId, string? StudentName,
    string SessionKey, DeviceType Device, int Visits, int Logins);
public sealed record AdminVisitResults(IReadOnlyList<AdminVisitData> Rows, IReadOnlyList<AdminVisitActivity> Activity,
    int TotalItems);

public interface IAdminReportStore
{
    Task<List<AdminStudentData>> Students(AdminReportFilter filter, bool matchSearch, CancellationToken ct);
    Task<List<AdminSubjectData>> Subjects(AdminReportFilter filter, CancellationToken ct);
    Task<List<AdminAttemptRow>> Attempts(AdminReportFilter filter, bool matchSearch, CancellationToken ct);
    Task<AdminAttemptData?> Attempt(Guid attemptId, CancellationToken ct);
    Task<List<AdminAnswerTotals>> AnswerTotals(AdminReportFilter filter, CancellationToken ct);
    Task<List<AdminQuestionData>> Questions(AdminReportFilter filter, bool matchSearch, CancellationToken ct);
    Task<int> QuestionCount(AdminReportFilter filter, CancellationToken ct);
    Task<List<AdminQuestionTotals>> QuestionTotals(AdminReportFilter filter, bool matchSearch, CancellationToken ct);
    Task<List<AdminQuestionChoice>> QuestionChoices(AdminQuestionFilter filter, CancellationToken ct);
    Task<AdminVisitResults> Visits(AdminVisitFilter filter, CancellationToken ct);
    Task<List<int>> ActiveStudents(AdminReportFilter filter, DateTime since, CancellationToken ct);
}

public interface IDiagramFiles
{
    bool Exists(string? imagePath);
}

public interface IUserVisitStore
{
    Task Save(IReadOnlyList<UserVisitLog> visits, CancellationToken ct);
}

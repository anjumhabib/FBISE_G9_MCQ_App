using System.ComponentModel.DataAnnotations;
using McqExam.Domain;

namespace McqExam.Application;

public sealed record RegisterRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(72, MinimumLength = 8)] string Password);
public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(72)] string Password);
public sealed record Profile(int Id, string FullName, string Email);
public sealed record AuthSession(string Token, Profile User, DateTime ExpiresAt);
public sealed record ChapterInfo(int Id, int ChapterNo, string Title, int QuestionCount);
public sealed record SubjectInfo(int Id, string Name, string Code, int QuestionCount, IReadOnlyList<ChapterInfo> Chapters);
public class ExamFilter
{
    [Range(1, int.MaxValue)] public int SubjectId { get; init; }
    [Required, MinLength(1), MaxLength(100)] public int[] ChapterIds { get; init; } = [];
    public BloomLevel? BloomLevel { get; init; }
    public bool IncludeDiagrams { get; init; }
}

public sealed class StartRequest : ExamFilter
{
    [Range(1, 200)] public int QuestionCount { get; init; }
    [Required] public ExamMode? Mode { get; init; }
}
public sealed record AnswerInput(int QuestionId, char? SelectedOption);
public sealed record SubmitRequest([Range(0, int.MaxValue)] int TimeSpentSeconds,
    [Required, MaxLength(200)] AnswerInput[] Answers);
public sealed record ExamQuestion(int Id, int Position, int ChapterNo, string ChapterTitle, BloomLevel BloomLevel,
    string QuestionText, string[] Options, string? ImagePath);
public sealed record ActiveExam(Guid AttemptId, string Subject, ExamMode Mode, DateTime StartedAt,
    DateTime? Deadline, DateTime ServerTime, IReadOnlyList<ExamQuestion> Questions, IReadOnlyList<AnswerInput> SavedAnswers);
public sealed record Accuracy(int Correct, int Total, decimal Percent);
public sealed record BloomResult(BloomLevel Level, Accuracy Accuracy);
public sealed record AttemptSummary(Guid Id, string Subject, ExamMode Mode, int TotalQuestions,
    int CorrectCount, decimal ScorePercent, int TimeSpentSeconds, DateTime CompletedAt);
public sealed record Result(AttemptSummary Attempt, IReadOnlyList<BloomResult> Bloom);
public sealed record ReviewedQuestion(ExamQuestion Question, char? SelectedOption, char CorrectOption, bool IsCorrect);
public sealed record Review(Result Result, IReadOnlyList<ReviewedQuestion> Questions);
public sealed record SubjectAverage(string Subject, decimal Average);
public sealed record ChapterResult(string Subject, int ChapterNo, string Title, Accuracy Accuracy);
public sealed record History(IReadOnlyList<AttemptSummary> Attempts, decimal OverallAverage,
    IReadOnlyList<SubjectAverage> SubjectAverages, IReadOnlyList<ChapterResult> WeakestChapters,
    IReadOnlyList<BloomResult> Bloom);

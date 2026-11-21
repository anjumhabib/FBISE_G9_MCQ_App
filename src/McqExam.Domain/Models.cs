namespace McqExam.Domain;

public enum BloomLevel { Knowledge, Conceptual, Application, Analyzing }
public enum ExamMode { Exam, Practice }

public sealed class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<Chapter> Chapters { get; } = [];
}

public sealed class Chapter
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public int ChapterNo { get; set; }
    public string Title { get; set; } = "";
    public List<Question> Questions { get; } = [];
}

public sealed class Question
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
    public int QNo { get; set; }
    public string QuestionText { get; set; } = "";
    public string OptionA { get; set; } = "";
    public string OptionB { get; set; } = "";
    public string OptionC { get; set; } = "";
    public string OptionD { get; set; } = "";
    public char CorrectOption { get; set; }
    public BloomLevel BloomLevel { get; set; }
    public bool HasDiagram { get; set; }
    public string? ImagePath { get; set; }
}

public sealed class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class ExamAttempt
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public ExamMode Mode { get; set; }
    public string ChaptersJson { get; set; } = "";
    public string? BloomFilter { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public decimal ScorePercent { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<ExamAnswer> Answers { get; } = [];
}

public sealed class ExamAnswer
{
    public int Id { get; set; }
    public Guid ExamAttemptId { get; set; }
    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int Position { get; set; }
    public char? SelectedOption { get; set; }
    public bool IsCorrect { get; set; }
    public char CorrectOption { get; set; }
    public BloomLevel BloomLevel { get; set; }
}

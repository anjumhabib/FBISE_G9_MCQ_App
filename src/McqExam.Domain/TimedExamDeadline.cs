namespace McqExam.Domain;

public sealed class TimedExamDeadline
{
    public Guid AttemptId { get; init; }
    public DateTime FinalizeAfter { get; init; }
}

using System.ComponentModel.DataAnnotations;
using McqExam.Domain;

namespace McqExam.Application;

public sealed record PublicSubject(string Code, IReadOnlyList<ChapterInfo> Chapters, int ChaptersWithDiagrams);
public sealed record BloomCoverage(BloomLevel Level, decimal Percent);
public sealed record SampleQuiz(string Ticket, IReadOnlyList<ExamQuestion> Questions);
public sealed record SampleQuizSubmission([Required, StringLength(4096)] string Ticket,
    [Required, MaxLength(5)] AnswerInput[] Answers);
public sealed record SampleQuizResult(int Correct, int Total, decimal Percent, IReadOnlyList<BloomResult> Bloom,
    IReadOnlyList<ReviewedQuestion> Questions);
public sealed record ContactRequest([Required, StringLength(100)] string Name,
    [Required, EmailAddress, StringLength(254)] string Email, [Required, StringLength(2000)] string Message);

public interface IPublicStudyStore
{
    Task<List<PublicSubject>> Subjects(CancellationToken ct);
    Task<IReadOnlyList<BloomCoverage>> Coverage(string code, CancellationToken ct);
    Task<List<Question>> Sample(string code, int count, CancellationToken ct);
    Task<List<Question>> Questions(int[] ids, CancellationToken ct);
    Task SaveContact(ContactInquiry inquiry, CancellationToken ct);
}

public interface ISampleQuizTickets
{
    string Issue(int[] questionIds);
    int[] Read(string ticket);
}

public sealed class PublicStudyService(IPublicStudyStore store, ISampleQuizTickets tickets, TimeProvider clock)
{
    private const int SampleLength = 5;

    public Task<List<PublicSubject>> Subjects(CancellationToken ct) => store.Subjects(ct);
    public Task<IReadOnlyList<BloomCoverage>> Coverage(string code, CancellationToken ct) => store.Coverage(code, ct);

    public async Task<SampleQuiz> Start(string code, CancellationToken ct)
    {
        var questions = await store.Sample(code, SampleLength, ct);
        if (questions.Count != SampleLength)
            throw new RequestFailure("A sample quiz is currently unavailable for this subject.", 404);

        return new(tickets.Issue(questions.Select(x => x.Id).ToArray()),
            questions.Select((q, index) => ExamService.Question(Answer(q, index, null))).ToArray());
    }

    public async Task<SampleQuizResult> Grade(SampleQuizSubmission request, CancellationToken ct)
    {
        var ids = tickets.Read(request.Ticket);
        var allowed = ids.ToHashSet();
        if (ids.Length != SampleLength || allowed.Count != SampleLength)
            throw new RequestFailure("Start a new sample quiz.");

        var selections = new Dictionary<int, char?>();
        foreach (var item in request.Answers)
        {
            if (item is null || !allowed.Contains(item.QuestionId) || item.SelectedOption is not (null or 'A' or 'B' or 'C' or 'D') ||
                !selections.TryAdd(item.QuestionId, item.SelectedOption))
                throw new RequestFailure("Each answer must select A to D for a unique question in this quiz.");
        }

        var questions = (await store.Questions(ids, ct)).ToDictionary(x => x.Id);
        if (questions.Count != SampleLength)
            throw new RequestFailure("This sample has changed. Start a new quiz.", 409);

        var answers = ids.Select((id, index) => Answer(questions[id], index, selections.GetValueOrDefault(id))).ToArray();
        var correct = answers.Count(x => x.IsCorrect);
        return new(correct, SampleLength, 100m * correct / SampleLength, ExamService.Bloom(answers),
            answers.Select(x => new ReviewedQuestion(ExamService.Question(x), x.SelectedOption, x.CorrectOption, x.IsCorrect)).ToArray());
    }

    public Task Contact(ContactRequest request, CancellationToken ct) => store.SaveContact(new ContactInquiry
    {
        Name = request.Name.Trim(), Email = request.Email.Trim(), Message = request.Message.Trim(),
        ReceivedAt = clock.GetUtcNow().UtcDateTime
    }, ct);

    private static ExamAnswer Answer(Question question, int index, char? choice) => new()
    {
        Question = question, Position = index + 1, BloomLevel = question.BloomLevel,
        SelectedOption = choice, CorrectOption = question.CorrectOption, IsCorrect = choice == question.CorrectOption
    };
}

using System.Text.Json;
using McqExam.Domain;

namespace McqExam.Application;

public sealed class ExamService(IExamStore store, TimeProvider clock)
{
    public Task<IReadOnlyList<SubjectInfo>> Subjects(CancellationToken ct) => store.Subjects(ct);

    public async Task<ActiveExam> Start(int userId, StartRequest request, CancellationToken ct, DeviceType device = DeviceType.Unknown)
    {
        await ValidateFilter(request, ct);
        var mode = request.Mode ?? throw new RequestFailure("Choose a test mode.");
        var questions = await store.Select(userId, request, request.QuestionCount, ct);
        if (questions.Count != request.QuestionCount)
            throw new RequestFailure("Try fewer questions or broaden your filters to start a test.");

        var attempt = new ExamAttempt
        {
            Id = Guid.NewGuid(), UserId = userId, SubjectId = request.SubjectId,
            Mode = mode, Device = device, ChaptersJson = JsonSerializer.Serialize(request.ChapterIds),
            BloomFilter = request.BloomLevel?.ToString(), TotalQuestions = questions.Count,
            StartedAt = clock.GetUtcNow().UtcDateTime
        };
        attempt.Answers.AddRange(questions.Select((question, index) => new ExamAnswer
        {
            QuestionId = question.Id, Question = question, Position = index + 1,
            CorrectOption = question.CorrectOption, BloomLevel = question.BloomLevel
        }));
        await store.Create(attempt, ct);
        return Active(attempt);
    }

    public async Task<ActiveExam> Resume(Guid id, int userId, CancellationToken ct)
    {
        var attempt = await Find(id, userId, ct);
        if (attempt.CompletedAt is not null)
            throw new RequestFailure("This test has already been submitted.", 409);
        return Active(attempt);
    }

    public async Task SaveDraft(Guid id, int userId, AnswerInput answer, CancellationToken ct)
    {
        var attempt = await Find(id, userId, ct);
        if (attempt.CompletedAt is not null || Deadline(attempt) <= clock.GetUtcNow().UtcDateTime)
            throw new RequestFailure("The test is closed for editing.", 409);
        if (!ValidOption(answer.SelectedOption) || !attempt.Answers.Any(x => x.QuestionId == answer.QuestionId))
            throw new RequestFailure("Select an option from A to D for a question assigned to this test.");
        if (!await store.SaveDraft(id, userId, answer, ct))
            throw new RequestFailure("The test is closed for editing.", 409);
    }

    public async Task<Result> Submit(Guid id, int userId, SubmitRequest request, CancellationToken ct)
    {
        var attempt = await Find(id, userId, ct);
        if (attempt.CompletedAt is not null)
            return Summary(attempt);

        var submitted = new Dictionary<int, char?>();
        var assigned = attempt.Answers.Select(answer => answer.QuestionId).ToHashSet();
        foreach (var answer in request.Answers)
        {
            if (answer is null || !assigned.Contains(answer.QuestionId) ||
                !ValidOption(answer.SelectedOption) ||
                !submitted.TryAdd(answer.QuestionId, answer.SelectedOption))
                throw new RequestFailure("Each answer must contain a unique question from this test and an option from A to D.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var deadline = Deadline(attempt);
        var onTime = deadline is null || now <= deadline.Value.AddSeconds(5);
        foreach (var answer in attempt.Answers)
        {
            if (onTime) answer.SelectedOption = submitted.GetValueOrDefault(answer.QuestionId);
            answer.IsCorrect = answer.SelectedOption == answer.CorrectOption;
        }
        attempt.CorrectCount = attempt.Answers.Count(answer => answer.IsCorrect);
        attempt.ScorePercent = Percent(attempt.CorrectCount, attempt.TotalQuestions);
        var elapsed = (int)Math.Clamp((now - Utc(attempt.StartedAt)).TotalSeconds, 0, int.MaxValue);
        attempt.TimeSpentSeconds = deadline is null ? elapsed : Math.Min(elapsed, attempt.TotalQuestions * 60);
        attempt.CompletedAt = now;
        if (!await store.SaveSubmission(ct))
            attempt = await Find(id, userId, ct);
        return Summary(attempt);
    }

    public async Task<Review> Review(Guid id, int userId, CancellationToken ct)
    {
        var attempt = await Find(id, userId, ct);
        if (attempt.CompletedAt is null)
            throw new RequestFailure("Submit this test before opening the answer review.", 409);
        return new Review(Summary(attempt), attempt.Answers.OrderBy(x => x.Position)
            .Select(x => new ReviewedQuestion(Question(x), x.SelectedOption, x.CorrectOption, x.IsCorrect)).ToArray());
    }

    public async Task<History> History(int userId, CancellationToken ct)
    {
        var attempts = await store.History(userId, ct);
        var answers = attempts.SelectMany(x => x.Answers).ToArray();
        return new History(attempts.Select(Attempt).ToArray(),
            attempts.Count == 0 ? 0 : Math.Round(attempts.Average(x => x.ScorePercent), 2),
            attempts.GroupBy(x => x.Subject.Name)
                .Select(x => new SubjectAverage(x.Key, Math.Round(x.Average(a => a.ScorePercent), 2)))
                .OrderByDescending(x => x.Average).ThenBy(x => x.Subject).ToArray(),
            attempts.SelectMany(attempt => attempt.Answers.Select(answer => (attempt.Subject.Name, Answer: answer)))
                .GroupBy(x => (x.Name, x.Answer.Question.Chapter.ChapterNo, x.Answer.Question.Chapter.Title))
                .Select(x => new ChapterResult(x.Key.Name, x.Key.ChapterNo, x.Key.Title, Measure(x.Select(a => a.Answer))))
                .OrderBy(x => x.Accuracy.Percent).ThenByDescending(x => x.Accuracy.Total)
                .ThenBy(x => x.Subject).ThenBy(x => x.ChapterNo).Take(5).ToArray(), Bloom(answers));
    }

    private async Task ValidateFilter(ExamFilter filter, CancellationToken ct)
    {
        if (filter.ChapterIds.Distinct().Count() != filter.ChapterIds.Length || !await store.ValidFilter(filter, ct))
            throw new RequestFailure("Choose chapters belonging to an active subject.");
    }

    private async Task<ExamAttempt> Find(Guid id, int userId, CancellationToken ct) =>
        await store.Find(id, userId, ct) ?? throw new RequestFailure("Test not found.", 404);

    private ActiveExam Active(ExamAttempt attempt) => new(attempt.Id, attempt.Subject.Name, attempt.Mode,
        Utc(attempt.StartedAt), Deadline(attempt), clock.GetUtcNow().UtcDateTime,
        attempt.Answers.OrderBy(x => x.Position).Select(Question).ToArray(),
        attempt.Answers.Where(x => x.SelectedOption is not null)
            .Select(x => new AnswerInput(x.QuestionId, x.SelectedOption)).ToArray());

    internal static ExamQuestion Question(ExamAnswer answer)
    {
        var q = answer.Question;
        return new(q.Id, answer.Position, q.Chapter.ChapterNo, q.Chapter.Title, answer.BloomLevel,
            q.QuestionText, [q.OptionA, q.OptionB, q.OptionC, q.OptionD], q.ImagePath);
    }

    private static DateTime Utc(DateTime time) => DateTime.SpecifyKind(time, DateTimeKind.Utc);
    private static bool ValidOption(char? option) => option is null or 'A' or 'B' or 'C' or 'D';
    private static DateTime? Deadline(ExamAttempt attempt) =>
        attempt.Mode == ExamMode.Exam ? Utc(attempt.StartedAt).AddMinutes(attempt.TotalQuestions) : null;
    private static Result Summary(ExamAttempt attempt) => new(Attempt(attempt), Bloom(attempt.Answers));
    private static AttemptSummary Attempt(ExamAttempt x) => new(x.Id, x.Subject.Name, x.Mode, x.TotalQuestions,
        x.CorrectCount, x.ScorePercent, x.TimeSpentSeconds, Utc(x.CompletedAt!.Value));
    private static decimal Percent(int correct, int total) => total == 0 ? 0 : Math.Round(100m * correct / total, 2);

    private static Accuracy Measure(IEnumerable<ExamAnswer> answers)
    {
        var total = 0;
        var correct = 0;
        foreach (var answer in answers)
        {
            total++;
            if (answer.IsCorrect) correct++;
        }
        return new(correct, total, Percent(correct, total));
    }

    internal static BloomResult[] Bloom(IEnumerable<ExamAnswer> answers) =>
        Enum.GetValues<BloomLevel>().Select(level => new BloomResult(level,
            Measure(answers.Where(x => x.BloomLevel == level)))).ToArray();
}

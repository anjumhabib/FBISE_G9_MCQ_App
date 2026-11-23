using System.Text.Json;
using McqExam.Domain;
using static McqExam.Application.AdminReportValues;

namespace McqExam.Application;

public sealed class AdminReportService(IAdminReportStore store, TimeProvider clock)
{
    public async Task<AdminDashboard> Dashboard(AdminReportFilter filter, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var attempts = await store.Attempts(filter, matchSearch: true, ct);
        var students = await store.Students(filter, matchSearch: true, ct);
        var questionCount = await store.QuestionCount(filter, ct);
        var activity30 = await ActiveStudents(filter, attempts, now.AddDays(-30), ct);
        var activity7 = await ActiveStudents(filter, attempts, now.AddDays(-7), ct);

        var knownStudents = students.Where(x => filter.Device is null || x.Student.LastVisitDevice == filter.Device ||
                activity30.Contains(x.Student.Id))
            .Select(x => x.Student.Id).ToHashSet();
        knownStudents.UnionWith(attempts.Select(x => x.StudentId));
        if (filter.SubjectId.HasValue || filter.ChapterId.HasValue)
        {
            var subjectStudents = attempts.Select(x => x.StudentId).Concat(activity30).ToHashSet();
            knownStudents.IntersectWith(subjectStudents);
        }

        var completed = Completed(attempts);
        var daily = attempts.ToLookup(x => DateOnly.FromDateTime(x.StartedAt));
        var trend = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29))
            .Select(day => new AdminDailyAttempts(day, daily[day].Count())).ToArray();
        var subjects = completed.GroupBy(x => x.Subject)
            .Select(x => new SubjectAverage(x.Key, Round(x.Average(a => a.ScorePercent))))
            .OrderByDescending(x => x.Average).ThenBy(x => x.Subject).ToArray();

        return new(knownStudents.Count, activity7.Count(knownStudents.Contains), activity30.Count(knownStudents.Contains),
            attempts.Count, Average(completed), questionCount, attempts.Count - completed.Length, trend, subjects,
            Page(attempts.Select(Utc).ToArray(), filter), Devices(attempts));
    }

    private async Task<HashSet<int>> ActiveStudents(AdminReportFilter filter, List<AdminAttemptRow> attempts,
        DateTime since, CancellationToken ct)
    {
        var visited = await store.ActiveStudents(filter, since, ct);
        return visited.Concat(attempts.Where(x => x.StartedAt >= since || x.CompletedAt >= since)
            .Select(x => x.StudentId)).ToHashSet();
    }

    public async Task<AdminPage<AdminStudentRow>> Students(AdminStudentFilter filter, CancellationToken ct)
    {
        var students = await store.Students(filter, matchSearch: true, ct);
        var attempts = (await store.Attempts(filter, matchSearch: false, ct)).ToLookup(x => x.StudentId);
        var cutoff = filter.InactiveDays is { } days ? clock.GetUtcNow().UtcDateTime.AddDays(-days) : (DateTime?)null;
        var rows = new List<AdminStudentRow>();
        foreach (var entry in students)
        {
            var tests = attempts[entry.Student.Id];
            if ((filter.SubjectId.HasValue || filter.ChapterId.HasValue) && !tests.Any())
                continue;
            if (filter.Device is { } device && entry.Student.LastVisitDevice != device && !tests.Any())
                continue;
            if (cutoff.HasValue && entry.LastLoginAt >= cutoff)
                continue;

            var completed = Completed(tests);
            var rawAverage = completed.Length == 0 ? (decimal?)null : completed.Average(x => x.ScorePercent);
            var recent = completed.OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.Id)
                .Take(3).Select(x => x.ScorePercent).ToArray();
            var atRisk = rawAverage < 50 || (recent.Length == 3 && recent[0] < recent[1] && recent[1] < recent[2]);
            if (filter.AtRisk && !atRisk)
                continue;

            var subjects = completed.GroupBy(x => x.Subject)
                .Select(x => new SubjectAverage(x.Key, x.Average(a => a.ScorePercent)))
                .OrderByDescending(x => x.Average).ThenBy(x => x.Subject).ToArray();
            rows.Add(new(Utc(entry.Student), entry.IsActive, AdminReportValues.Utc(entry.LastLoginAt), tests.Count(),
                completed.Length, tests.Count() - completed.Length, rawAverage is { } avg ? Round(avg) : null,
                subjects.FirstOrDefault()?.Subject, subjects.LastOrDefault()?.Subject,
                completed.Sum(x => (long)x.TimeSpentSeconds), atRisk, Devices(tests)));
        }
        return Page(rows, filter);
    }

    public async Task<AdminPage<AdminStudentSubjectRow>> StudentSubjects(AdminReportFilter filter, CancellationToken ct)
    {
        var students = await store.Students(filter, matchSearch: false, ct);
        var subjects = await store.Subjects(filter, ct);
        var attempts = (await store.Attempts(filter, matchSearch: false, ct)).ToLookup(x => (x.StudentId, x.SubjectId));
        var matchingStudents = attempts.Select(x => x.Key.StudentId).ToHashSet();
        var rows = from entry in students
                   where filter.Device is null || entry.Student.LastVisitDevice == filter.Device ||
                         matchingStudents.Contains(entry.Student.Id)
                   from subject in subjects
                   where Matches(filter, entry.Student.FullName, entry.Student.Email, subject.Name) ||
                         subject.Chapters.Any(x => Matches(filter, x.Title))
                   select StudentSubject(Utc(entry.Student), subject, attempts[(entry.Student.Id, subject.Id)]);
        return Page(rows.ToArray(), filter);
    }

    public async Task<AdminPage<AdminSubjectRow>> Subjects(AdminReportFilter filter, CancellationToken ct)
    {
        var subjects = await store.Subjects(filter, ct);
        var attempts = (await store.Attempts(filter, matchSearch: false, ct)).ToLookup(x => x.SubjectId);
        var answers = (await store.AnswerTotals(filter, ct)).ToLookup(x => x.SubjectId);
        var rows = new List<AdminSubjectRow>();
        foreach (var subject in subjects.Where(s => Matches(filter, s.Name) || s.Chapters.Any(c => Matches(filter, c.Title))))
        {
            var tests = attempts[subject.Id];
            var completed = Completed(tests);
            var stats = answers[subject.Id];
            var bloom = Bloom(level => Measure(stats.Where(x => x.BloomLevel == level)));
            var chapters = stats.ToLookup(x => x.ChapterId);
            var heatmap = subject.Chapters
                .Select(x => new AdminChapterRow(x.Id, x.ChapterNo, x.Title, Measure(chapters[x.Id]),
                    DeviceCounts(chapters[x.Id].Select(a => (a.Device, a.Total)))))
                .OrderBy(x => x.Accuracy.Total == 0).ThenBy(x => x.Accuracy.Percent).ThenBy(x => x.ChapterNo)
                .ToArray();
            rows.Add(new(subject.Id, subject.Name, tests.Count(), tests.Select(x => x.StudentId).Distinct().Count(),
                Average(completed), completed.Length == 0 ? null : Percent(completed.Count(x => x.ScorePercent >= 50), completed.Length),
                bloom, heatmap, Devices(tests)));
        }
        return Page(rows, filter);
    }

    public async Task<AdminAttemptDetail> Attempt(Guid id, CancellationToken ct)
    {
        var data = await store.Attempt(id, ct) ?? throw new RequestFailure("Test not found.", 404);
        var chapterIds = JsonSerializer.Deserialize<int[]>(data.Attempt.ChaptersJson) ?? [];
        var chapters = data.Chapters.Where(x => chapterIds.Contains(x.Id))
            .Select(x => new AdminAttemptChapter(x.Id, x.ChapterNo, x.Title)).ToArray();
        var questions = data.Attempt.Answers.OrderBy(x => x.Position)
            .Select(x => new AdminAttemptQuestion(ExamService.Question(x), x.SelectedOption, x.CorrectOption,
                x.SelectedOption is null ? AdminAnswerStatus.Skipped :
                x.SelectedOption == x.CorrectOption ? AdminAnswerStatus.Correct : AdminAnswerStatus.Incorrect)).ToArray();
        var bloom = Bloom(level =>
        {
            var answers = questions.Where(x => x.Question.BloomLevel == level).ToArray();
            var correct = answers.Count(x => x.Status == AdminAnswerStatus.Correct);
            return new(correct, answers.Length, Percent(correct, answers.Length));
        });
        return new(Utc(data.Student), Utc(data.Row), chapters, bloom, questions);
    }

    public async Task<AdminPage<AdminAnomaly>> Anomalies(AdminReportFilter filter, CancellationToken ct)
    {
        var attempts = await store.Attempts(filter, matchSearch: true, ct);
        var rows = new List<AdminAnomaly>();
        foreach (var attempt in attempts)
        {
            if (attempt.CompletedAt is null)
            {
                rows.Add(new(Utc(attempt), true, false, null));
                continue;
            }

            if (attempt.TotalQuestions > 0 && (decimal)attempt.TimeSpentSeconds / attempt.TotalQuestions < 5)
                rows.Add(new(Utc(attempt), false, true, Round((decimal)attempt.TimeSpentSeconds / attempt.TotalQuestions)));
        }
        return Page(rows, filter);
    }

    private static AdminStudentSubjectRow StudentSubject(AdminStudentIdentity student, AdminSubjectData subject,
        IEnumerable<AdminAttemptRow> attempts)
    {
        var completed = Completed(attempts);
        var devices = Devices(attempts);
        if (completed.Length == 0)
            return new(student, subject.Id, subject.Name, attempts.Count(), 0, null, null, null, null, devices);

        var questions = completed.Sum(x => (long)x.TotalQuestions);
        return new(student, subject.Id, subject.Name, attempts.Count(), completed.Length, Average(completed),
            completed.Max(x => x.ScorePercent), completed.Min(x => x.ScorePercent),
            questions == 0 ? null : Round((decimal)completed.Sum(x => (long)x.TimeSpentSeconds) / questions), devices);
    }

    private static AdminDeviceCounts Devices(IEnumerable<AdminAttemptRow> attempts) =>
        DeviceCounts(attempts.Select(x => (x.Device, 1)));

    private static AdminAttemptRow[] Completed(IEnumerable<AdminAttemptRow> attempts) =>
        attempts.Where(x => x.CompletedAt.HasValue).ToArray();

    private static decimal? Average(IReadOnlyCollection<AdminAttemptRow> completed) =>
        completed.Count == 0 ? null : Round(completed.Average(x => x.ScorePercent));

    private static Accuracy Measure(IEnumerable<AdminAnswerTotals> stats)
    {
        var total = 0;
        var correct = 0;
        foreach (var row in stats)
        {
            total += row.Total;
            correct += row.Correct;
        }
        return new(correct, total, Percent(correct, total));
    }

    private static BloomResult[] Bloom(Func<BloomLevel, Accuracy> measure) =>
        Enum.GetValues<BloomLevel>().Select(level => new BloomResult(level, measure(level))).ToArray();

    private static AdminStudentIdentity Utc(AdminStudentIdentity student) =>
        student with { CreatedAt = AdminReportValues.Utc(student.CreatedAt) };

    private static AdminAttemptRow Utc(AdminAttemptRow attempt) =>
        attempt with { StartedAt = AdminReportValues.Utc(attempt.StartedAt), CompletedAt = AdminReportValues.Utc(attempt.CompletedAt) };
}

internal static class AdminReportValues
{
    internal static AdminDeviceCounts DeviceCounts(IEnumerable<(DeviceType Device, int Count)> items)
    {
        var groups = items.ToLookup(x => x.Device, x => x.Count);
        return new(groups[DeviceType.Mobile].Sum(), groups[DeviceType.Desktop].Sum(),
            groups[DeviceType.Tablet].Sum(), groups[DeviceType.Bot].Sum(), groups[DeviceType.Unknown].Sum());
    }

    internal static DateTime Utc(DateTime time) => DateTime.SpecifyKind(time, DateTimeKind.Utc);
    internal static DateTime? Utc(DateTime? time) => time is { } value ? Utc(value) : null;
    internal static decimal Round(decimal value) => Math.Round(value, 2);
    internal static decimal Percent(int count, int total) => total == 0 ? 0 : Round(100m * count / total);

    internal static bool Matches(AdminReportFilter filter, params string[] values) =>
        string.IsNullOrWhiteSpace(filter.Search) ||
        values.Any(x => x.Contains(filter.Search.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static AdminPage<T> Page<T>(IReadOnlyList<T> rows, AdminReportFilter filter)
    {
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        var items = filter.ExportCsv ? rows :
            offset >= rows.Count ? [] : rows.Skip((int)offset).Take(filter.PageSize).ToArray();
        return new(items, filter.Page, filter.PageSize, rows.Count);
    }
}

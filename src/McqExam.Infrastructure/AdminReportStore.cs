using System.Linq.Expressions;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.EntityFrameworkCore;

namespace McqExam.Infrastructure;

public sealed class AdminReportStore(ExamDb db) : IAdminReportStore
{
    private sealed class AttemptRecord
    {
        public required ExamAttempt Attempt { get; init; }
        public required User Student { get; init; }
    }

    private sealed class AnswerRecord
    {
        public required ExamAnswer Answer { get; init; }
        public required ExamAttempt Attempt { get; init; }
    }

    private sealed class VisitRecord
    {
        public required UserVisitLog Visit { get; init; }
        public User? Student { get; init; }
    }

    public Task<List<AdminStudentData>> Students(AdminReportFilter filter, bool matchSearch, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking().Where(x => x.Role == UserRole.Student);
        if (filter.StudentId is { } studentId)
            users = users.Where(x => x.Id == studentId);
        if (matchSearch && Search(filter) is { } search)
            users = users.Where(x => x.FullName.ToLower().Contains(search) || x.Email.Contains(search));

        return users.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Select(x => new AdminStudentData(new AdminStudentIdentity(x.Id, x.FullName, x.Email, x.CreatedAt,
                db.UserVisitLogs.Where(v => v.UserId == x.Id).OrderByDescending(v => v.VisitedAt).ThenByDescending(v => v.Id)
                    .Select(v => (DeviceType?)ExamDb.VisitDevice(v.UserAgent)).FirstOrDefault() ?? DeviceType.Unknown),
                x.IsActive, x.LastLoginAt)).ToListAsync(ct);
    }

    public async Task<List<AdminSubjectData>> Subjects(AdminReportFilter filter, CancellationToken ct)
    {
        var chapters = db.Chapters.AsNoTracking();
        if (filter.ChapterId is { } chapterId)
            chapters = chapters.Where(x => x.Id == chapterId);
        var subjects = db.Subjects.AsNoTracking();
        if (filter.SubjectId is { } subjectId)
            subjects = subjects.Where(x => x.Id == subjectId);
        if (filter.ChapterId.HasValue)
            subjects = subjects.Where(x => chapters.Any(c => c.SubjectId == x.Id));

        return await subjects.OrderBy(x => x.Id)
            .Select(x => new AdminSubjectData(x.Id, x.Name, chapters.Where(c => c.SubjectId == x.Id)
                .OrderBy(c => c.ChapterNo).Select(c => new AdminChapterData(c.Id, c.ChapterNo, c.Title))
                .ToList())).ToListAsync(ct);
    }

    public Task<List<AdminAttemptRow>> Attempts(AdminReportFilter filter, bool matchSearch, CancellationToken ct) =>
        AttemptRows(AttemptRecords(filter, matchSearch)).ToListAsync(ct);

    public async Task<AdminAttemptData?> Attempt(Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.ExamAttempts.AsNoTracking().Include(x => x.Subject).Include(x => x.Answers)
            .ThenInclude(x => x.Question).ThenInclude(x => x.Chapter).SingleOrDefaultAsync(x => x.Id == attemptId, ct);
        if (attempt is null) return null;

        var student = await db.Users.Where(x => x.Id == attempt.UserId)
            .Select(x => new AdminStudentIdentity(x.Id, x.FullName, x.Email, x.CreatedAt,
                db.UserVisitLogs.Where(v => v.UserId == x.Id).OrderByDescending(v => v.VisitedAt).ThenByDescending(v => v.Id)
                    .Select(v => (DeviceType?)ExamDb.VisitDevice(v.UserAgent)).FirstOrDefault() ?? DeviceType.Unknown)).SingleAsync(ct);
        var row = new AdminAttemptRow(attempt.Id, student.Id, student.FullName, student.Email, attempt.SubjectId,
            attempt.Subject.Name, attempt.Mode, attempt.TotalQuestions, attempt.CorrectCount, attempt.ScorePercent,
            attempt.TimeSpentSeconds, attempt.StartedAt, attempt.CompletedAt, attempt.Device);
        var chapters = await db.Chapters.Where(x => x.SubjectId == attempt.SubjectId)
            .OrderBy(x => x.ChapterNo).Select(x => new AdminChapterData(x.Id, x.ChapterNo, x.Title))
            .ToListAsync(ct);
        return new(attempt, row, student, chapters);
    }

    public Task<List<AdminAnswerTotals>> AnswerTotals(AdminReportFilter filter, CancellationToken ct) =>
        Answers(filter).Where(x => x.Attempt.CompletedAt != null)
            .GroupBy(x => new { x.Attempt.SubjectId, x.Answer.Question.ChapterId, x.Answer.BloomLevel, x.Attempt.Device })
            .Select(g => new AdminAnswerTotals(g.Key.SubjectId, g.Key.ChapterId, g.Key.BloomLevel, g.Key.Device,
                g.Count(), g.Count(x => x.Answer.IsCorrect))).ToListAsync(ct);

    public Task<List<AdminQuestionData>> Questions(AdminReportFilter filter, bool matchSearch, CancellationToken ct)
    {
        var questions = QuestionQuery(filter, matchSearch);
        return (from q in questions
                join subject in db.Subjects on q.Chapter.SubjectId equals subject.Id
                orderby subject.Id, q.Chapter.ChapterNo, q.QNo, q.Id
                select new AdminQuestionData(new AdminQuestionItem(q.Id, subject.Id, subject.Name,
                    q.ChapterId, q.Chapter.ChapterNo, q.Chapter.Title, q.QNo, q.QuestionText, q.BloomLevel),
                    q.CorrectOption, q.HasDiagram, q.ImagePath, new[] { q.OptionA, q.OptionB, q.OptionC, q.OptionD }))
            .ToListAsync(ct);
    }

    public Task<int> QuestionCount(AdminReportFilter filter, CancellationToken ct) =>
        QuestionQuery(filter, matchSearch: true).CountAsync(ct);

    public Task<List<AdminQuestionTotals>> QuestionTotals(AdminReportFilter filter, bool matchSearch, CancellationToken ct)
    {
        var questions = QuestionQuery(filter, matchSearch);
        return Answers(filter).Where(x => questions.Any(q => q.Id == x.Answer.QuestionId))
            .GroupBy(x => x.Answer.QuestionId)
            .Select(g => new AdminQuestionTotals(g.Key, g.Count(), g.Count(x => x.Attempt.CompletedAt != null),
                g.Count(x => x.Attempt.CompletedAt != null && x.Answer.IsCorrect),
                g.Count(x => x.Attempt.CompletedAt != null && x.Answer.SelectedOption == null),
                g.Count(x => x.Attempt.CompletedAt != null && x.Answer.SelectedOption == 'A'),
                g.Count(x => x.Attempt.CompletedAt != null && x.Answer.SelectedOption == 'B'),
                g.Count(x => x.Attempt.CompletedAt != null && x.Answer.SelectedOption == 'C'),
                g.Count(x => x.Attempt.CompletedAt != null && x.Answer.SelectedOption == 'D'),
                new AdminDeviceCounts(
                    g.Count(x => x.Attempt.Device == DeviceType.Mobile),
                    g.Count(x => x.Attempt.Device == DeviceType.Desktop),
                    g.Count(x => x.Attempt.Device == DeviceType.Tablet),
                    g.Count(x => x.Attempt.Device == DeviceType.Bot),
                    g.Count(x => x.Attempt.Device == DeviceType.Unknown)))).ToListAsync(ct);
    }

    public async Task<List<AdminQuestionChoice>> QuestionChoices(AdminQuestionFilter filter, CancellationToken ct)
    {
        var questions = QuestionQuery(filter, matchSearch: true);
        var completed = Answers(filter).Where(x => x.Attempt.CompletedAt != null &&
            questions.Any(q => q.Id == x.Answer.QuestionId));
        var students = await completed.GroupBy(x => x.Answer.QuestionId)
            .Select(g => new AdminQuestionChoice(g.Key, null, g.Select(x => x.Attempt.UserId).Distinct().Count()))
            .ToListAsync(ct);
        var choices = await completed.Where(x => x.Answer.SelectedOption != null)
            .GroupBy(x => new { x.Answer.QuestionId, x.Answer.SelectedOption })
            .Select(g => new AdminQuestionChoice(g.Key.QuestionId, g.Key.SelectedOption,
                g.Select(x => x.Attempt.UserId).Distinct().Count())).ToListAsync(ct);
        students.AddRange(choices);
        return students;
    }

    public async Task<AdminVisitResults> Visits(AdminVisitFilter filter, CancellationToken ct)
    {
        var query = VisitRecords(filter);
        if (filter.EventType is { } eventType)
            query = query.Where(x => x.Visit.EventType == eventType);
        if (Search(filter) is { } search)
            query = query.Where(x => x.Visit.Path.ToLower().Contains(search) ||
                (x.Student != null && (x.Student.FullName.ToLower().Contains(search) || x.Student.Email.Contains(search))));

        var activity = filter.ExportCsv ? [] : await query.GroupBy(x => new
        {
            Day = x.Visit.VisitedAt.Date,
            Hour = x.Visit.VisitedAt.Hour,
            x.Visit.UserId,
            StudentName = x.Student == null ? null : x.Student.FullName,
            x.Visit.SessionKey,
            Device = ExamDb.VisitDevice(x.Visit.UserAgent)
        }).Select(g => new AdminVisitActivity(g.Key.Day, g.Key.Hour, g.Key.UserId, g.Key.StudentName,
            g.Key.SessionKey, g.Key.Device, g.Count(), g.Count(x => x.Visit.EventType == UserVisitEventType.Login))).ToListAsync(ct);
        var total = activity.Sum(x => x.Visits);
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        if (!filter.ExportCsv && offset >= total)
            return new([], activity, total);

        var page = query.OrderByDescending(x => x.Visit.VisitedAt).ThenByDescending(x => x.Visit.Id).AsQueryable();
        if (!filter.ExportCsv)
            page = page.Skip((int)offset).Take(filter.PageSize);
        var rows = await page.Select(x => new AdminVisitData(x.Visit.Id, x.Visit.VisitedAt, x.Visit.UserId,
            x.Student == null ? null : x.Student.FullName, x.Visit.EventType, x.Visit.Path, x.Visit.IpAddress,
            x.Visit.UserAgent)).ToListAsync(ct);
        return new(rows, activity, filter.ExportCsv ? rows.Count : total);
    }

    public Task<List<int>> ActiveStudents(AdminReportFilter filter, DateTime since, CancellationToken ct) =>
        VisitRecords(filter).Where(x => x.Visit.UserId != null && x.Visit.VisitedAt >= since)
            .Select(x => x.Visit.UserId!.Value).Distinct().ToListAsync(ct);

    private IQueryable<VisitRecord> VisitRecords(AdminReportFilter filter)
    {
        var visits = DateRange(db.UserVisitLogs.AsNoTracking(), filter, x => x.VisitedAt);
        if (filter.StudentId is { } studentId)
            visits = visits.Where(x => x.UserId == studentId);
        if (filter.Device is { } device)
            visits = visits.Where(x => ExamDb.VisitDevice(x.UserAgent) == device);
        if (filter.SubjectId.HasValue || filter.ChapterId.HasValue)
        {
            // Subject filters describe the student cohort. Login and anonymous page URLs have no curriculum fields.
            var cohort = AttemptRecords(filter, matchSearch: false, matchDates: false, matchDevice: false)
                .Select(x => x.Attempt.UserId);
            visits = visits.Where(x => x.UserId != null && cohort.Contains(x.UserId.Value));
        }

        return from visit in visits
               join student in db.Users.AsNoTracking() on visit.UserId equals student.Id into matches
               from student in matches.DefaultIfEmpty()
               where student == null || student.Role == UserRole.Student
               select new VisitRecord { Visit = visit, Student = student };
    }

    private IQueryable<AttemptRecord> AttemptRecords(AdminReportFilter filter, bool matchSearch, bool matchDates = true,
        bool matchDevice = true)
    {
        var attempts = db.ExamAttempts.AsNoTracking();
        if (matchDates)
            attempts = DateRange(attempts, filter, x => x.StartedAt);
        if (filter.SubjectId is { } subjectId)
            attempts = attempts.Where(x => x.SubjectId == subjectId);
        if (filter.ChapterId is { } chapterId)
            attempts = attempts.Where(x => x.Answers.Any(a => a.Question.ChapterId == chapterId));
        if (filter.StudentId is { } studentId)
            attempts = attempts.Where(x => x.UserId == studentId);
        if (matchDevice && filter.Device is { } device)
            attempts = attempts.Where(x => x.Device == device);

        var records = from attempt in attempts
                      join student in db.Users.AsNoTracking() on attempt.UserId equals student.Id
                      where student.Role == UserRole.Student
                      select new AttemptRecord { Attempt = attempt, Student = student };
        if (matchSearch && Search(filter) is { } search)
            records = records.Where(x => x.Student.FullName.ToLower().Contains(search) ||
                x.Student.Email.Contains(search) || x.Attempt.Subject.Name.ToLower().Contains(search));
        return records;
    }

    private static IQueryable<AdminAttemptRow> AttemptRows(IQueryable<AttemptRecord> rows) =>
        rows.OrderByDescending(x => x.Attempt.StartedAt).ThenByDescending(x => x.Attempt.Id)
            .Select(x => new AdminAttemptRow(x.Attempt.Id, x.Student.Id, x.Student.FullName, x.Student.Email,
                x.Attempt.SubjectId, x.Attempt.Subject.Name, x.Attempt.Mode, x.Attempt.TotalQuestions,
                x.Attempt.CorrectCount, x.Attempt.ScorePercent, x.Attempt.TimeSpentSeconds, x.Attempt.StartedAt,
                x.Attempt.CompletedAt, x.Attempt.Device));

    private IQueryable<AnswerRecord> Answers(AdminReportFilter filter)
    {
        var answers = db.ExamAnswers.AsNoTracking();
        if (filter.ChapterId is { } chapterId)
            answers = answers.Where(x => x.Question.ChapterId == chapterId);
        return from answer in answers
               join row in AttemptRecords(filter, matchSearch: false) on answer.ExamAttemptId equals row.Attempt.Id
               select new AnswerRecord { Answer = answer, Attempt = row.Attempt };
    }

    private IQueryable<Question> QuestionQuery(AdminReportFilter filter, bool matchSearch)
    {
        var query = db.Questions.AsNoTracking();
        if (filter.SubjectId is { } subjectId)
            query = query.Where(x => x.Chapter.SubjectId == subjectId);
        if (filter.ChapterId is { } chapterId)
            query = query.Where(x => x.ChapterId == chapterId);
        if (filter is AdminQuestionFilter { BloomLevel: { } level })
            query = query.Where(x => x.BloomLevel == level);
        if (matchSearch && Search(filter) is { } search)
        {
            query = from question in query
                    join subject in db.Subjects on question.Chapter.SubjectId equals subject.Id
                    where question.QuestionText.ToLower().Contains(search) ||
                        question.Chapter.Title.ToLower().Contains(search) || subject.Name.ToLower().Contains(search)
                    select question;
        }
        return query;
    }

    private static string? Search(AdminReportFilter filter) =>
        string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim().ToLowerInvariant();

    private static IQueryable<T> DateRange<T>(IQueryable<T> query, AdminReportFilter filter,
        Expression<Func<T, DateTime>> timestamp)
    {
        if (filter.FromDate is { } from)
            query = query.Where(Compare(timestamp, from.ToDateTime(TimeOnly.MinValue), true));
        if (filter.ToDate is { } to)
            query = query.Where(Compare(timestamp, to.ToDateTime(TimeOnly.MaxValue), false));
        return query;
    }

    private static Expression<Func<T, bool>> Compare<T>(
        Expression<Func<T, DateTime>> timestamp, DateTime boundary, bool lower)
    {
        // Capture the boundary so EF can parameterize it and reuse the query plan.
        Expression<Func<DateTime>> value = () => boundary;
        var body = lower
            ? Expression.GreaterThanOrEqual(timestamp.Body, value.Body)
            : Expression.LessThanOrEqual(timestamp.Body, value.Body);
        return Expression.Lambda<Func<T, bool>>(body, timestamp.Parameters);
    }
}

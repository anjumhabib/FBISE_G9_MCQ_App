using McqExam.Application;
using McqExam.Domain;
using Microsoft.EntityFrameworkCore;

namespace McqExam.Infrastructure;

public sealed class PublicStudyStore(ExamDb db) : IPublicStudyStore
{
    public Task<List<PublicSubject>> Subjects(CancellationToken ct) =>
        db.Subjects.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id)
            .Select(x => new PublicSubject(x.Code, x.Chapters.OrderBy(c => c.ChapterNo)
                .Select(c => new ChapterInfo(c.Id, c.ChapterNo, c.Title)).ToArray(),
                x.Chapters.Count(c => c.Questions.Any(q => q.HasDiagram)))).ToListAsync(ct);

    public async Task<IReadOnlyList<BloomCoverage>> Coverage(string code, CancellationToken ct)
    {
        var levels = await QuestionsFor(code).GroupBy(q => q.BloomLevel)
            .Select(g => new { Level = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Level, x => x.Count, ct);
        var total = levels.Values.Sum();
        return total == 0 ? [] : Enum.GetValues<BloomLevel>()
            .Select(level => new BloomCoverage(level, Math.Round(100m * levels.GetValueOrDefault(level) / total))).ToArray();
    }

    public Task<List<Question>> Sample(string code, int count, CancellationToken ct) =>
        QuestionsFor(code).Include(x => x.Chapter).OrderBy(_ => EF.Functions.Random()).Take(count).ToListAsync(ct);

    public Task<List<Question>> Questions(int[] ids, CancellationToken ct) =>
        db.Questions.AsNoTracking().Where(q => ids.Contains(q.Id)).Include(q => q.Chapter).ToListAsync(ct);

    public async Task SaveContact(ContactInquiry inquiry, CancellationToken ct)
    {
        db.ContactInquiries.Add(inquiry);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Question> QuestionsFor(string code) =>
        from question in db.Questions.AsNoTracking()
        join subject in db.Subjects on question.Chapter.SubjectId equals subject.Id
        where subject.IsActive && subject.Code == code
        select question;
}

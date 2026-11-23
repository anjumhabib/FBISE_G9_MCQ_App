using System.Text;
using static McqExam.Application.AdminReportValues;

namespace McqExam.Application;

public sealed class AdminAuditService(IAdminReportStore store, IDiagramFiles diagrams)
{
    public async Task<AdminPage<AdminQuestionRow>> Questions(AdminQuestionFilter filter, CancellationToken ct)
    {
        var bank = await store.Questions(filter, matchSearch: true, ct);
        var totals = (await store.QuestionTotals(filter, matchSearch: true, ct)).ToDictionary(x => x.QuestionId);
        var choices = (await store.QuestionChoices(filter, ct)).ToLookup(x => x.QuestionId);
        var rows = new List<AdminQuestionRow>();
        foreach (var data in bank)
        {
            var id = data.Question.Id;
            var summary = totals.GetValueOrDefault(id) ?? new AdminQuestionTotals(id, 0, 0, 0, 0, 0, 0, 0, 0, new());
            if (filter.NeverAttempted ? summary.TimesServed != 0 : HasActivityScope(filter) && summary.TimesServed == 0)
                continue;

            decimal? accuracy = summary.Completed == 0 ? null : 100m * summary.Correct / summary.Completed;
            var students = choices[id].FirstOrDefault(x => x.Option is null)?.Students ?? 0;
            var wrongKey = accuracy < 20 && choices[id].Any(x =>
                x.Option is 'A' or 'B' or 'C' or 'D' && x.Option != data.CorrectOption && 2L * x.Students > students);
            if ((filter.MaxAccuracy.HasValue && (accuracy is null || accuracy > filter.MaxAccuracy)) ||
                (filter.SuspectedWrongKey && !wrongKey))
                continue;

            var answered = summary.A + summary.B + summary.C + summary.D;
            var distribution = new AdminOptionDistribution(Percent(summary.A, answered), Percent(summary.B, answered),
                Percent(summary.C, answered), Percent(summary.D, answered));
            rows.Add(new(data.Question, data.CorrectOption, summary.TimesServed, summary.Completed, summary.Correct,
                accuracy is { } score ? Round(score) : null, summary.Skipped, distribution, wrongKey, summary.PresentationDevices));
        }
        return Page(rows, filter);
    }

    public async Task<AdminPage<AdminDuplicateGroup>> Duplicates(AdminReportFilter filter, CancellationToken ct)
    {
        var bank = await store.Questions(filter, matchSearch: false, ct);
        var totals = (await store.QuestionTotals(filter, matchSearch: false, ct)).ToDictionary(x => x.QuestionId);
        var rows = bank.GroupBy(x => Normalize(x.Question.QuestionText), StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0 && group.Count() > 1 &&
                group.Any(x => Matches(filter, x.Question.QuestionText, x.Question.Subject, x.Question.Chapter)) &&
                (!HasActivityScope(filter) || group.Any(x => totals.ContainsKey(x.Question.Id))))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new AdminDuplicateGroup(group.Key,
                group.GroupBy(x => x.Question.QuestionText, StringComparer.Ordinal).Any(x => x.Count() > 1),
                group.Select(x =>
                {
                    var summary = totals.GetValueOrDefault(x.Question.Id);
                    return new AdminDuplicateItem(x.Question, summary?.TimesServed ?? 0, summary?.PresentationDevices ?? new());
                }).ToArray()))
            .ToArray();
        return Page(rows, filter);
    }

    public async Task<AdminPage<AdminMediaIssue>> MediaAudit(AdminReportFilter filter, CancellationToken ct)
    {
        var bank = await store.Questions(filter, matchSearch: true, ct);
        var served = (await store.QuestionTotals(filter, matchSearch: true, ct)).ToDictionary(x => x.QuestionId);
        var issues = new List<AdminMediaIssue>();
        foreach (var data in bank)
        {
            var summary = served.GetValueOrDefault(data.Question.Id);
            if (HasActivityScope(filter) && summary is null)
                continue;

            var broken = data.HasDiagram && !diagrams.Exists(data.ImagePath);
            var empty = new List<string>();
            var truncated = new List<string>();
            for (var i = 0; i < data.Options.Length; i++)
            {
                var option = data.Options[i];
                var label = ((char)('A' + i)).ToString();
                if (string.IsNullOrWhiteSpace(option))
                    empty.Add(label);
                else if (LooksTruncated(option))
                    truncated.Add(label);
            }

            if (broken || empty.Count > 0 || truncated.Count > 0)
                issues.Add(new(data.Question, data.HasDiagram, data.ImagePath, broken, empty, truncated,
                    summary?.PresentationDevices ?? new()));
        }
        return Page(issues, filter);
    }

    private static bool HasActivityScope(AdminReportFilter filter) =>
        filter.FromDate.HasValue || filter.ToDate.HasValue || filter.StudentId.HasValue || filter.Device.HasValue;

    private static string Normalize(string text)
    {
        var normalized = new StringBuilder();
        var spacePending = false;
        foreach (var character in text.Normalize().EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(character))
            {
                spacePending = normalized.Length > 0;
                continue;
            }
            if (Rune.IsPunctuation(character)) continue;
            if (spacePending) normalized.Append(' ');
            normalized.Append(Rune.ToLowerInvariant(character));
            spacePending = false;
        }
        return normalized.ToString();
    }

    private static bool LooksTruncated(string value)
    {
        var text = value.TrimEnd();
        return text.EndsWith("...", StringComparison.Ordinal) || text.EndsWith('…') ||
            text.EndsWith('�') || text.EndsWith("[truncated]", StringComparison.OrdinalIgnoreCase);
    }
}

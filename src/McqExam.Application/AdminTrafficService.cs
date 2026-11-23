using static McqExam.Application.AdminReportValues;

namespace McqExam.Application;

public sealed class AdminTrafficService(IAdminReportStore store)
{
    public async Task<AdminVisitsReport> Visits(AdminVisitFilter filter, CancellationToken ct)
    {
        var data = await store.Visits(filter, ct);
        var daily = data.Activity.GroupBy(x => DateOnly.FromDateTime(x.Day)).OrderBy(x => x.Key)
            .Select(x => new AdminDailyVisitors(x.Key, UniqueVisitors(x))).ToArray();
        var hourly = data.Activity.ToLookup(x => x.Hour);
        var weekdays = data.Activity.ToLookup(x => x.Day.DayOfWeek);
        var topStudents = data.Activity.Where(x => x.StudentId.HasValue)
            .GroupBy(x => (Id: x.StudentId!.Value, x.StudentName))
            .Select(x => new AdminActiveStudent(x.Key.Id, x.Key.StudentName ?? "", x.Sum(a => a.Visits),
                DeviceCounts(x.Select(a => (a.Device, a.Visits)))))
            .OrderByDescending(x => x.Visits).ThenBy(x => x.StudentId).Take(10).ToArray();
        var rows = data.Rows.Select(x => new AdminVisitRow(x.Id, Utc(x.VisitedAt), x.StudentId, x.StudentName, x.EventType,
            x.Path, x.IpAddress, ClientDevice.Classify(x.UserAgent), ClientDevice.Browser(x.UserAgent))).ToArray();

        return new(daily, data.Activity.Sum(x => x.Logins),
            Enumerable.Range(0, 24).Select(x => new AdminHourlyTraffic(x, hourly[x].Sum(a => a.Visits))).ToArray(),
            Enum.GetValues<DayOfWeek>().Select(x => new AdminWeekdayTraffic(x, weekdays[x].Sum(a => a.Visits))).ToArray(),
            topStudents, new(rows, filter.Page, filter.PageSize, data.TotalItems),
            DeviceCounts(data.Activity.Select(x => (x.Device, x.Visits))));
    }

    private static int UniqueVisitors(IEnumerable<AdminVisitActivity> visits)
    {
        var students = new HashSet<int>();
        var studentSessions = new HashSet<string>(StringComparer.Ordinal);
        var anonymousSessions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var visit in visits)
        {
            if (visit.StudentId is { } studentId)
            {
                students.Add(studentId);
                studentSessions.Add(visit.SessionKey);
            }
            else
            {
                anonymousSessions.Add(visit.SessionKey);
            }
        }
        anonymousSessions.ExceptWith(studentSessions);
        return students.Count + anonymousSessions.Count;
    }

}

using System.Text.Json;
using System.Text.Json.Serialization;
using McqExam.Application;
using Microsoft.AspNetCore.Mvc.Filters;

namespace McqExam.WebAPI;

public sealed class ReportTimeZones(string? displayTimeZone)
{
    public TimeZoneInfo Csv { get; } = Resolve(string.IsNullOrWhiteSpace(displayTimeZone) ? "Asia/Karachi" : displayTimeZone)
        ?? throw new InvalidOperationException("Configure DisplayTimeZone with a valid timezone ID.");

    internal static TimeZoneInfo? Resolve(string id)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) return zone;
        if (id.Equals("Asia/Karachi", StringComparison.OrdinalIgnoreCase) &&
            TimeZoneInfo.TryFindSystemTimeZoneById("Pakistan Standard Time", out zone))
            return zone;
        return null;
    }
}

internal sealed class AdminReportTimeZoneFilter(ReportTimeZones zones) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var filter in context.ActionArguments.Values.OfType<AdminReportFilter>())
        {
            if (filter.ExportCsv) filter.UseTimeZone(zones.Csv);
            else if (!string.IsNullOrWhiteSpace(filter.TimeZone))
                filter.UseTimeZone(ReportTimeZones.Resolve(filter.TimeZone)
                    ?? throw new RequestFailure("Use a valid report timezone ID."));
        }

        await next();
    }
}

internal sealed class UtcInstantJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        ReportCalendar.Utc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ReportCalendar.Utc(value));
}

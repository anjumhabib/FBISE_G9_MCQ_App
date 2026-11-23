using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace McqExam.WebAPI;

public sealed class CsvDownload(IOptions<JsonOptions> json)
{
    private JsonSerializerOptions Options => json.Value.JsonSerializerOptions;

    public FileContentResult File<T>(string name, IEnumerable<T> rows)
    {
        var columns = Columns(typeof(T), "").ToArray();
        var csv = new StringBuilder("\uFEFF");
        WriteRow(csv, columns);
        foreach (var row in rows)
        {
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            Flatten(JsonSerializer.SerializeToElement(row, Options), "", cells);
            WriteRow(csv, columns.Select(column => cells.GetValueOrDefault(column) ?? ""));
        }

        return new(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8")
        {
            FileDownloadName = $"{name}.csv"
        };
    }

    private IEnumerable<string> Columns(Type type, string prefix)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var name = Options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            var path = prefix + name;
            var value = property.PropertyType;
            if (value.IsClass && value != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(value))
            {
                foreach (var column in Columns(value, path + ".")) yield return column;
            }
            else
            {
                yield return path;
            }
        }
    }

    private static void Flatten(JsonElement value, string prefix, Dictionary<string, string> cells)
    {
        foreach (var property in value.EnumerateObject())
        {
            var path = prefix + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Flatten(property.Value, path + ".", cells);
                continue;
            }

            cells.Add(path, property.Value.ValueKind switch
            {
                JsonValueKind.Null => "",
                JsonValueKind.String => EscapeFormula(property.Value.GetString()!),
                _ => property.Value.GetRawText()
            });
        }
    }

    private static string EscapeFormula(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        return (value.Length > 0 && value[0] is '\t' or '\r' or '\n') ||
            (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@' or '＝' or '＋' or '－' or '＠')
            ? "'" + value : value;
    }

    private static void WriteRow(StringBuilder csv, IEnumerable<string> values)
    {
        csv.AppendJoin(',', values.Select(value => "\"" + value.Replace("\"", "\"\"") + "\""));
        csv.Append("\r\n");
    }
}

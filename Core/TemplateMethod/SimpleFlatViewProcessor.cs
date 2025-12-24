using System.Text;
using Newtonsoft.Json.Linq;

namespace JsonTool.Core.TemplateMethod;
public class SimpleFlatViewProcessor : JsonProcessorBase
{
    public override string ProcessorName => "Simple Flat View Processor";
    public override string Description => "Converts JSON to simple flat path = value format";
    public string PathSeparator { get; set; } = ".";
    public string ArrayIndexFormat { get; set; } = "[{0}]";
    public bool IncludeTypes { get; set; } = false;
    public bool IncludeNulls { get; set; } = true;
    public bool ShowEmptyContainers { get; set; } = true;
    public string AssignmentOperator { get; set; } = " = ";
    public int MaxDepth { get; set; } = 0;

    protected override object ParseJson(string jsonContent)
    {
        return JToken.Parse(jsonContent);
    }

    protected override object TransformData(object parsedData)
    {
        if (parsedData is not JToken token)
            throw new InvalidOperationException("Expected JToken");

        var entries = new List<FlatViewEntry>();
        Flatten(token, "", entries, 0);

        return new SimpleFlatViewResult
        {
            Entries = entries,
            Statistics = new FlatViewStatistics
            {
                TotalEntries = entries.Count,
                StringCount = entries.Count(e => e.Type == "string"),
                NumberCount = entries.Count(e => e.Type == "number" || e.Type == "integer"),
                BooleanCount = entries.Count(e => e.Type == "boolean"),
                NullCount = entries.Count(e => e.Type == "null"),
                ArrayCount = entries.Count(e => e.Type == "array"),
                ObjectCount = entries.Count(e => e.Type == "object"),
                MaxDepth = entries.Any() ? entries.Max(e => e.Depth) : 0
            }
        };
    }

    private void Flatten(JToken token, string path, List<FlatViewEntry> entries, int depth)
    {
        if (MaxDepth > 0 && depth > MaxDepth)
        {
            entries.Add(new FlatViewEntry
            {
                Path = path,
                Value = "...",
                FormattedValue = "...",
                Type = "truncated",
                Depth = depth
            });
            return;
        }

        switch (token.Type)
        {
            case JTokenType.Object:
                FlattenObject((JObject)token, path, entries, depth);
                break;

            case JTokenType.Array:
                FlattenArray((JArray)token, path, entries, depth);
                break;

            case JTokenType.String:
                AddEntry(entries, path, token.Value<string>(), $"\"{token.Value<string>()}\"", "string", depth);
                break;

            case JTokenType.Integer:
                AddEntry(entries, path, token.Value<long>(), token.ToString(), "integer", depth);
                break;

            case JTokenType.Float:
                AddEntry(entries, path, token.Value<double>(), token.ToString(), "number", depth);
                break;

            case JTokenType.Boolean:
                AddEntry(entries, path, token.Value<bool>(), token.Value<bool>().ToString().ToLower(), "boolean", depth);
                break;

            case JTokenType.Null:
                if (IncludeNulls)
                {
                    AddEntry(entries, path, null, "null", "null", depth);
                }
                break;

            case JTokenType.Date:
                var date = token.Value<DateTime>();
                AddEntry(entries, path, date, $"\"{date:O}\"", "date", depth);
                break;

            default:
                AddEntry(entries, path, token.ToString(), token.ToString(), token.Type.ToString().ToLower(), depth);
                break;
        }
    }

    private void FlattenObject(JObject obj, string path, List<FlatViewEntry> entries, int depth)
    {
        if (obj.Count == 0)
        {
            if (ShowEmptyContainers)
            {
                AddEntry(entries, path, new object(), "{}", "object", depth);
            }
            return;
        }

        foreach (var prop in obj.Properties())
        {
            var newPath = string.IsNullOrEmpty(path)
                ? prop.Name
                : $"{path}{PathSeparator}{prop.Name}";

            Flatten(prop.Value, newPath, entries, depth + 1);
        }
    }

    private void FlattenArray(JArray array, string path, List<FlatViewEntry> entries, int depth)
    {
        if (array.Count == 0)
        {
            if (ShowEmptyContainers)
            {
                AddEntry(entries, path, Array.Empty<object>(), "[]", "array", depth);
            }
            return;
        }

        for (int i = 0; i < array.Count; i++)
        {
            var indexStr = string.Format(ArrayIndexFormat, i);
            var newPath = $"{path}{indexStr}";
            Flatten(array[i], newPath, entries, depth + 1);
        }
    }

    private void AddEntry(List<FlatViewEntry> entries, string path, object? value, string formattedValue, string type, int depth)
    {
        entries.Add(new FlatViewEntry
        {
            Path = path,
            Value = value,
            FormattedValue = formattedValue,
            Type = type,
            Depth = depth
        });
    }

    protected override string FormatOutput(object transformedData)
    {
        if (transformedData is not SimpleFlatViewResult result)
            return base.FormatOutput(transformedData);

        var sb = new StringBuilder();

        foreach (var entry in result.Entries)
        {
            var line = $"{entry.Path}{AssignmentOperator}{entry.FormattedValue}";
            
            if (IncludeTypes)
            {
                line += $"  // {entry.Type}";
            }

            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }

    protected override void OnAfterProcessing(ProcessingResult result)
    {
        if (result.TransformedData is SimpleFlatViewResult flatResult)
        {
            result.Metadata["TotalEntries"] = flatResult.Statistics.TotalEntries;
            result.Metadata["MaxDepth"] = flatResult.Statistics.MaxDepth;
            result.Metadata["Statistics"] = flatResult.Statistics;
        }
    }
    public static string ToFlatView(string json, string separator = ".")
    {
        var processor = new SimpleFlatViewProcessor { PathSeparator = separator };
        var result = processor.ProcessJson(json);
        return result.Success ? result.Output : $"Error: {result.ErrorMessage}";
    }
    public List<FlatViewEntry> GetEntries(string json)
    {
        var result = ProcessJson(json);
        if (result.Success && result.TransformedData is SimpleFlatViewResult flatResult)
        {
            return flatResult.Entries;
        }
        return new List<FlatViewEntry>();
    }
}

#region Data Models
public class SimpleFlatViewResult
{
    public List<FlatViewEntry> Entries { get; set; } = new();
    public FlatViewStatistics Statistics { get; set; } = new();
}
public class FlatViewEntry
{
    public string Path { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Depth { get; set; }

    public override string ToString() => $"{Path} = {FormattedValue}";
}
public class FlatViewStatistics
{
    public int TotalEntries { get; set; }
    public int StringCount { get; set; }
    public int NumberCount { get; set; }
    public int BooleanCount { get; set; }
    public int NullCount { get; set; }
    public int ArrayCount { get; set; }
    public int ObjectCount { get; set; }
    public int MaxDepth { get; set; }
}

#endregion
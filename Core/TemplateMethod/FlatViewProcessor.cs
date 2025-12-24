using Newtonsoft.Json.Linq;

namespace JsonTool.Core.TemplateMethod;
public class FlatViewProcessor : JsonProcessorBase
{
    public override string ProcessorName => "Flat View Processor";
    public override string Description => "Converts nested JSON structure to flat path-value pairs";
    public string PathSeparator { get; set; } = ".";
    public ArrayIndexFormat ArrayFormat { get; set; } = ArrayIndexFormat.Brackets;
    public bool IncludeNullValues { get; set; } = true;
    public bool IncludeEmptyContainers { get; set; } = false;
    public int MaxDepth { get; set; } = 0;

    protected override object ParseJson(string jsonContent)
    {
        return JToken.Parse(jsonContent);
    }

    protected override object TransformData(object parsedData)
    {
        if (parsedData is not JToken rootToken)
        {
            throw new InvalidOperationException("Expected JToken from ParseJson");
        }

        var flatEntries = new List<FlatEntry>();
        FlattenToken(rootToken, "$", flatEntries, 0);

        return new FlatViewResult
        {
            Entries = flatEntries,
            TotalEntries = flatEntries.Count,
            MaxDepthReached = flatEntries.Max(e => e.Depth)
        };
    }

    private void FlattenToken(JToken token, string path, List<FlatEntry> entries, int depth)
    {
        if (MaxDepth > 0 && depth > MaxDepth)
        {
            entries.Add(new FlatEntry
            {
                Path = path,
                Value = "[max depth reached]",
                ValueType = "truncated",
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

            case JTokenType.Null:
                if (IncludeNullValues)
                {
                    entries.Add(new FlatEntry
                    {
                        Path = path,
                        Value = null,
                        ValueType = "null",
                        Depth = depth
                    });
                }
                break;

            default:
                entries.Add(new FlatEntry
                {
                    Path = path,
                    Value = GetTokenValue(token),
                    ValueType = GetValueTypeName(token.Type),
                    Depth = depth,
                    OriginalToken = token
                });
                break;
        }
    }

    private void FlattenObject(JObject obj, string path, List<FlatEntry> entries, int depth)
    {
        if (obj.Count == 0)
        {
            if (IncludeEmptyContainers)
            {
                entries.Add(new FlatEntry
                {
                    Path = path,
                    Value = "{}",
                    ValueType = "empty object",
                    Depth = depth
                });
            }
            return;
        }

        foreach (var property in obj.Properties())
        {
            var propertyPath = path == "$" 
                ? $"${PathSeparator}{property.Name}" 
                : $"{path}{PathSeparator}{property.Name}";
            
            FlattenToken(property.Value, propertyPath, entries, depth + 1);
        }
    }

    private void FlattenArray(JArray array, string path, List<FlatEntry> entries, int depth)
    {
        if (array.Count == 0)
        {
            if (IncludeEmptyContainers)
            {
                entries.Add(new FlatEntry
                {
                    Path = path,
                    Value = "[]",
                    ValueType = "empty array",
                    Depth = depth
                });
            }
            return;
        }

        for (int i = 0; i < array.Count; i++)
        {
            var indexPath = ArrayFormat switch
            {
                ArrayIndexFormat.Brackets => $"{path}[{i}]",
                ArrayIndexFormat.Dot => $"{path}{PathSeparator}{i}",
                ArrayIndexFormat.Colon => $"{path}:{i}",
                _ => $"{path}[{i}]"
            };

            FlattenToken(array[i], indexPath, entries, depth + 1);
        }
    }

    private static object? GetTokenValue(JToken token)
    {
        return token.Type switch
        {
            JTokenType.String => token.Value<string>(),
            JTokenType.Integer => token.Value<long>(),
            JTokenType.Float => token.Value<double>(),
            JTokenType.Boolean => token.Value<bool>(),
            JTokenType.Date => token.Value<DateTime>(),
            JTokenType.Guid => token.Value<Guid>(),
            JTokenType.Uri => token.Value<Uri>()?.ToString(),
            JTokenType.TimeSpan => token.Value<TimeSpan>(),
            _ => token.ToString()
        };
    }

    private static string GetValueTypeName(JTokenType type)
    {
        return type switch
        {
            JTokenType.String => "string",
            JTokenType.Integer => "integer",
            JTokenType.Float => "number",
            JTokenType.Boolean => "boolean",
            JTokenType.Date => "date",
            JTokenType.Guid => "guid",
            JTokenType.Uri => "uri",
            JTokenType.TimeSpan => "timespan",
            JTokenType.Bytes => "bytes",
            _ => type.ToString().ToLower()
        };
    }

    protected override string FormatOutput(object transformedData)
    {
        if (transformedData is not FlatViewResult result)
        {
            return base.FormatOutput(transformedData);
        }

        var lines = new List<string>
        {
            $"Flat View ({result.TotalEntries} entries, max depth: {result.MaxDepthReached})",
            new string('=', 60),
            ""
        };
        var maxPathLength = result.Entries.Max(e => e.Path.Length);
        maxPathLength = Math.Min(maxPathLength, 50); // Обмежуємо

        foreach (var entry in result.Entries)
        {
            var path = entry.Path.Length > maxPathLength 
                ? "..." + entry.Path.Substring(entry.Path.Length - maxPathLength + 3)
                : entry.Path.PadRight(maxPathLength);
            
            var valueStr = FormatValue(entry.Value);
            var typeStr = $"[{entry.ValueType}]";
            
            lines.Add($"{path} = {valueStr,-30} {typeStr}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "null";
        if (value is string s) return $"\"{TruncateString(s, 25)}\"";
        if (value is bool b) return b.ToString().ToLower();
        return TruncateString(value.ToString() ?? "", 30);
    }

    private static string TruncateString(string s, int maxLength)
    {
        if (s.Length <= maxLength) return s;
        return s.Substring(0, maxLength - 3) + "...";
    }

    protected override void OnAfterProcessing(ProcessingResult result)
    {
        if (result.TransformedData is FlatViewResult flatResult)
        {
            result.Metadata["EntryCount"] = flatResult.TotalEntries;
            result.Metadata["MaxDepth"] = flatResult.MaxDepthReached;
            var typeCounts = flatResult.Entries
                .GroupBy(e => e.ValueType)
                .ToDictionary(g => g.Key, g => g.Count());
            result.Metadata["TypeCounts"] = typeCounts;
        }
    }
}

#region Data Models
public class FlatViewResult
{
    public List<FlatEntry> Entries { get; set; } = new();
    public int TotalEntries { get; set; }
    public int MaxDepthReached { get; set; }
}
public class FlatEntry
{
    public string Path { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string ValueType { get; set; } = string.Empty;
    public int Depth { get; set; }
    public JToken? OriginalToken { get; set; }
}
public enum ArrayIndexFormat
{
    Brackets,
    Dot,
    Colon
}

#endregion
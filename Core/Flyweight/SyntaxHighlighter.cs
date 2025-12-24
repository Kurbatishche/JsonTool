using System.Text.RegularExpressions;

namespace JsonTool.Core.Flyweight;
public class SyntaxHighlighter
{
    private readonly TextFormatFactory _formatFactory;

    public SyntaxHighlighter(TextFormatFactory formatFactory)
    {
        _formatFactory = formatFactory;
    }
    public List<SyntaxToken> Tokenize(string jsonContent)
    {
        var tokens = new List<SyntaxToken>();
        
        if (string.IsNullOrEmpty(jsonContent))
            return tokens;

        var patterns = new Dictionary<string, string>
        {
            { "String", "\"(?:[^\"\\\\]|\\\\.)*\"" },
            { "Number", @"-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?" },
            { "Boolean", @"\b(true|false)\b" },
            { "Null", @"\bnull\b" },
            { "Bracket", @"[\[\]{}]" },
            { "Colon", @":" },
            { "Comma", @"," }
        };

        var combinedPattern = string.Join("|", patterns.Select(p => $"(?<{p.Key}>{p.Value})"));
        var regex = new Regex(combinedPattern, RegexOptions.Compiled);

        foreach (Match match in regex.Matches(jsonContent))
        {
            string tokenType = patterns.Keys.FirstOrDefault(k => match.Groups[k].Success) ?? "Default";
            var format = _formatFactory.GetFormat(tokenType);
            if (tokenType == "String" && IsPropertyKey(jsonContent, match.Index))
            {
                format = _formatFactory.GetFormat("PropertyKey");
                tokenType = "PropertyKey";
            }

            tokens.Add(new SyntaxToken
            {
                StartIndex = match.Index,
                Length = match.Length,
                Text = match.Value,
                TokenType = tokenType,
                Format = format
            });
        }

        return tokens.OrderBy(t => t.StartIndex).ToList();
    }

    private bool IsPropertyKey(string content, int stringIndex)
    {
        var afterString = content.Substring(stringIndex);
        var stringMatch = Regex.Match(afterString, "^\"(?:[^\"\\\\]|\\\\.)*\"");
        if (!stringMatch.Success) return false;

        var afterStringEnd = stringIndex + stringMatch.Length;
        if (afterStringEnd >= content.Length) return false;

        var remaining = content.Substring(afterStringEnd).TrimStart();
        return remaining.StartsWith(":");
    }
}
public class SyntaxToken
{
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public string Text { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public ITextFormat Format { get; set; } = null!;
}
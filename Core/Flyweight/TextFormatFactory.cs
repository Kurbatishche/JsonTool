using System.Windows.Media;

namespace JsonTool.Core.Flyweight;
public class TextFormatFactory
{
    private readonly Dictionary<string, ITextFormat> _formats = new();

    public TextFormatFactory()
    {
        InitializeDefaultFormats();
    }

    private void InitializeDefaultFormats()
    {
        _formats["String"] = new TextFormat("String", Color.FromRgb(206, 145, 120), Colors.Transparent);
        _formats["Number"] = new TextFormat("Number", Color.FromRgb(181, 206, 168), Colors.Transparent);
        _formats["Boolean"] = new TextFormat("Boolean", Color.FromRgb(86, 156, 214), Colors.Transparent, isBold: true);
        _formats["Null"] = new TextFormat("Null", Color.FromRgb(86, 156, 214), Colors.Transparent, isItalic: true);
        _formats["PropertyKey"] = new TextFormat("PropertyKey", Color.FromRgb(156, 220, 254), Colors.Transparent);
        _formats["Bracket"] = new TextFormat("Bracket", Color.FromRgb(212, 212, 212), Colors.Transparent);
        _formats["Colon"] = new TextFormat("Colon", Color.FromRgb(212, 212, 212), Colors.Transparent);
        _formats["Comma"] = new TextFormat("Comma", Color.FromRgb(212, 212, 212), Colors.Transparent);
        _formats["Error"] = new TextFormat("Error", Colors.Red, Color.FromRgb(255, 230, 230));
        _formats["Comment"] = new TextFormat("Comment", Color.FromRgb(106, 153, 85), Colors.Transparent, isItalic: true);
        _formats["Keyword"] = new TextFormat("Keyword", Color.FromRgb(197, 134, 192), Colors.Transparent);
    }
    public ITextFormat GetFormat(string key)
    {
        if (_formats.TryGetValue(key, out var format))
        {
            return format;
        }
        return GetOrCreateFormat("Default", Colors.White, Colors.Transparent);
    }
    public ITextFormat GetOrCreateFormat(string name, Color foreground, Color background, bool isBold = false, bool isItalic = false)
    {
        var key = $"{name}_{foreground}_{background}_{isBold}_{isItalic}";
        
        if (!_formats.TryGetValue(key, out var format))
        {
            format = new TextFormat(name, foreground, background, isBold, isItalic);
            _formats[key] = format;
        }

        return format;
    }
    public int GetCachedFormatsCount() => _formats.Count;
    public void ClearCustomFormats()
    {
        var keysToRemove = _formats.Keys
            .Where(k => k.Contains('_'))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _formats.Remove(key);
        }
    }
}
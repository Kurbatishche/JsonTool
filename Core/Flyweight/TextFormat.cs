using System.Windows.Media;

namespace JsonTool.Core.Flyweight;
public class TextFormat : ITextFormat
{
    public Color Foreground { get; }
    public Color Background { get; }
    public bool IsBold { get; }
    public bool IsItalic { get; }
    public string FormatName { get; }

    public TextFormat(string formatName, Color foreground, Color background, bool isBold = false, bool isItalic = false)
    {
        FormatName = formatName;
        Foreground = foreground;
        Background = background;
        IsBold = isBold;
        IsItalic = isItalic;
    }

    public override bool Equals(object? obj)
    {
        if (obj is TextFormat other)
        {
            return Foreground == other.Foreground &&
                   Background == other.Background &&
                   IsBold == other.IsBold &&
                   IsItalic == other.IsItalic;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Foreground, Background, IsBold, IsItalic);
    }
}
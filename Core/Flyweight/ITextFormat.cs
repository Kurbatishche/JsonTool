using System.Windows.Media;

namespace JsonTool.Core.Flyweight;
public interface ITextFormat
{
    Color Foreground { get; }
    Color Background { get; }
    bool IsBold { get; }
    bool IsItalic { get; }
    string FormatName { get; }
}
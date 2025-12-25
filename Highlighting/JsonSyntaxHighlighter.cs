using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;

namespace JsonTool.Highlighting;
public static class JsonSyntaxHighlighter
{
    public static void ConfigureJsonEditor(TextEditor editor)
    {
        editor.SyntaxHighlighting = JsonHighlightingLoader.JsonHighlighting;
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.ShowSpaces = false;
        editor.Options.ShowTabs = false;
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 2;
        editor.Options.HighlightCurrentLine = true;
        editor.Options.ShowEndOfLine = false;
        editor.Options.ShowBoxForControlCharacters = true;
        editor.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        editor.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
        editor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));
        editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
        editor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), 1);
        editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x26, 0x4F, 0x78));
        editor.TextArea.SelectionForeground = null; // Зберігаємо оригінальні кольори тексту
        editor.FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New");
        editor.FontSize = 14;
        editor.ShowLineNumbers = true;
    }
    public static FoldingManager SetupFolding(TextEditor editor)
    {
        var foldingManager = FoldingManager.Install(editor.TextArea);
        UpdateFolding(foldingManager, editor.Document);
        
        editor.TextChanged += (s, e) => UpdateFolding(foldingManager, editor.Document);
        
        return foldingManager;
    }
    public static void UpdateFolding(FoldingManager manager, TextDocument document)
    {
        var foldings = CreateFoldings(document);
        manager.UpdateFoldings(foldings, -1);
    }
    private static IEnumerable<NewFolding> CreateFoldings(TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var startOffsets = new Stack<int>();
        var braceTypes = new Stack<char>();
        var inString = false;
        var escape = false;

        for (int i = 0; i < document.TextLength; i++)
        {
            char c = document.GetCharAt(i);

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c == '{' || c == '[')
            {
                startOffsets.Push(i);
                braceTypes.Push(c);
            }
            else if ((c == '}' || c == ']') && startOffsets.Count > 0)
            {
                var expectedClose = braceTypes.Peek() == '{' ? '}' : ']';
                if (c == expectedClose)
                {
                    int startOffset = startOffsets.Pop();
                    braceTypes.Pop();

                    var startLine = document.GetLineByOffset(startOffset);
                    var endLine = document.GetLineByOffset(i);

                    if (startLine.LineNumber < endLine.LineNumber)
                    {
                        var foldingName = c == '}' ? "{...}" : "[...]";
                        foldings.Add(new NewFolding(startOffset, i + 1) { Name = foldingName });
                    }
                }
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }
    public static void AddErrorHighlighting(TextEditor editor, int line, int column, string message)
    {
        var errorMarker = new ErrorTextMarkerService(editor.Document);
        editor.TextArea.TextView.BackgroundRenderers.Add(errorMarker);
        
        var offset = editor.Document.GetOffset(line, column);
        var lineObj = editor.Document.GetLineByNumber(line);
        var length = Math.Min(10, lineObj.EndOffset - offset);
        
        errorMarker.Create(offset, length, message);
    }
}
public class ErrorTextMarkerService : IBackgroundRenderer
{
    private readonly TextDocument _document;
    private readonly List<ErrorMarker> _markers = new();

    public ErrorTextMarkerService(TextDocument document)
    {
        _document = document;
    }

    public void Create(int offset, int length, string message)
    {
        _markers.Add(new ErrorMarker
        {
            StartOffset = offset,
            Length = length,
            Message = message
        });
    }

    public void Clear()
    {
        _markers.Clear();
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid) return;

        foreach (var marker in _markers)
        {
            var segment = new TextSegment { StartOffset = marker.StartOffset, Length = marker.Length };
            
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                var brush = new SolidColorBrush(Color.FromArgb(0x40, 0xF4, 0x47, 0x47));
                drawingContext.DrawRectangle(brush, null, rect);
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x47)), 1);
                pen.DashStyle = DashStyles.Dot;
                drawingContext.DrawLine(pen, 
                    new System.Windows.Point(rect.Left, rect.Bottom), 
                    new System.Windows.Point(rect.Right, rect.Bottom));
            }
        }
    }

    private class ErrorMarker
    {
        public int StartOffset { get; set; }
        public int Length { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private class TextSegment : ISegment
    {
        public int Offset => StartOffset;
        public int StartOffset { get; set; }
        public int Length { get; set; }
        public int EndOffset => StartOffset + Length;
    }
}
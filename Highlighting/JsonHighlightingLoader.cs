using System.IO;
using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace JsonTool.Highlighting;
public static class JsonHighlightingLoader
{
    private static IHighlightingDefinition? _jsonHighlighting;
    private static readonly object _lock = new();
    public static IHighlightingDefinition JsonHighlighting
    {
        get
        {
            if (_jsonHighlighting == null)
            {
                lock (_lock)
                {
                    _jsonHighlighting ??= LoadJsonHighlighting();
                }
            }
            return _jsonHighlighting;
        }
    }
    private static IHighlightingDefinition LoadJsonHighlighting()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "JsonTool.Resources.JsonHighlighting.xshd";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        
        if (stream != null)
        {
            using var reader = new XmlTextReader(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "JsonHighlighting.xshd");
        if (File.Exists(filePath))
        {
            using var fileStream = File.OpenRead(filePath);
            using var reader = new XmlTextReader(fileStream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        return CreateJsonHighlightingProgrammatically();
    }
    public static IHighlightingDefinition LoadFromFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new XmlTextReader(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
    public static IHighlightingDefinition LoadFromString(string xshdContent)
    {
        using var stringReader = new StringReader(xshdContent);
        using var reader = new XmlTextReader(stringReader);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
    public static void RegisterJsonHighlighting()
    {
        var highlighting = JsonHighlighting;
        HighlightingManager.Instance.RegisterHighlighting(
            "JSON",
            new[] { ".json", ".jsonc", ".json5" },
            highlighting);
    }
    private static IHighlightingDefinition CreateJsonHighlightingProgrammatically()
    {
        var xshd = GetEmbeddedXshd();
        return LoadFromString(xshd);
    }
    public static string GetEmbeddedXshd()
    {
        return """
<?xml version="1.0" encoding="utf-8"?>
<SyntaxDefinition name="JSON" extensions=".json"
                  xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  
  <Color name="Key" foreground="#9CDCFE" fontWeight="normal"/>
  <Color name="String" foreground="#CE9178"/>
  <Color name="Number" foreground="#B5CEA8"/>
  <Color name="Boolean" foreground="#569CD6" fontWeight="bold"/>
  <Color name="Null" foreground="#808080" fontStyle="italic"/>
  <Color name="Brackets" foreground="#FFD700"/>
  <Color name="Colon" foreground="#CCCCCC"/>
  <Color name="Comma" foreground="#CCCCCC"/>
  <Color name="Comment" foreground="#6A9955" fontStyle="italic"/>
  
  <RuleSet ignoreCase="false">
    
    <Span color="Comment" begin="//" />
    <Span color="Comment" multiline="true" begin="/\*" end="\*/" />
    
    <Rule color="Key">
      "(?:[^"\\]|\\.)*"\s*(?=:)
    </Rule>
    
    <Span color="String">
      <Begin>"</Begin>
      <End>"</End>
      <RuleSet>
        <Rule foreground="#D7BA7D">
          \\["\\/bfnrt]|\\u[0-9a-fA-F]{4}
        </Rule>
      </RuleSet>
    </Span>
    
    <Keywords color="Boolean">
      <Word>true</Word>
      <Word>false</Word>
    </Keywords>
    
    <Keywords color="Null">
      <Word>null</Word>
    </Keywords>
    
    <Rule color="Number">
      -?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?
    </Rule>
    
    <Rule color="Brackets">
      [\[\]{}]
    </Rule>
    
    <Rule color="Colon">
      :
    </Rule>
    
    <Rule color="Comma">
      ,
    </Rule>
    
  </RuleSet>
</SyntaxDefinition>
""";
    }
}
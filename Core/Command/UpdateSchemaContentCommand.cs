using JsonTool.Core.Models;

namespace JsonTool.Core.Command;
public class UpdateSchemaContentCommand : SchemaCommandBase
{
    private readonly JsonSchemaDocument _document;
    private readonly string _oldContent;
    private readonly string _newContent;
    private readonly Action<JsonSchemaDocument>? _onContentChanged;

    public override string Description => "Update schema content";

    public UpdateSchemaContentCommand(
        JsonSchemaDocument document,
        string newContent,
        Action<JsonSchemaDocument>? onContentChanged = null)
    {
        _document = document;
        _oldContent = document.RawContent;
        _newContent = newContent;
        _onContentChanged = onContentChanged;
    }

    public override bool CanExecute()
    {
        return _oldContent != _newContent && !IsExecuted;
    }

    protected override void ExecuteCore()
    {
        _document.RawContent = _newContent;
        _document.IsModified = true;
        _document.LastModified = DateTime.Now;
        _onContentChanged?.Invoke(_document);
    }

    protected override void UndoCore()
    {
        _document.RawContent = _oldContent;
        _document.IsModified = true;
        _document.LastModified = DateTime.Now;
        _onContentChanged?.Invoke(_document);
    }
}
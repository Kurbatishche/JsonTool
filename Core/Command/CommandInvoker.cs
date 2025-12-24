namespace JsonTool.Core.Command;
public class CommandInvoker
{
    private readonly Stack<ISchemaCommand> _undoStack = new();
    private readonly Stack<ISchemaCommand> _redoStack = new();
    private readonly int _maxHistorySize;

    public CommandInvoker(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize;
    }

    public event EventHandler? CommandExecuted;
    public event EventHandler? UndoExecuted;
    public event EventHandler? RedoExecuted;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void ExecuteCommand(ISchemaCommand command)
    {
        if (!command.CanExecute()) return;

        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        while (_undoStack.Count > _maxHistorySize)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < items.Length - 1; i++)
            {
                _undoStack.Push(items[i]);
            }
        }

        CommandExecuted?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);

        UndoExecuted?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);

        RedoExecuted?.Invoke(this, EventArgs.Empty);
    }

    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    public IEnumerable<string> GetUndoHistory()
    {
        return _undoStack.Select(c => c.Description);
    }

    public IEnumerable<string> GetRedoHistory()
    {
        return _redoStack.Select(c => c.Description);
    }
}
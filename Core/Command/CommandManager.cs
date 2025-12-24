namespace JsonTool.Core.Command;
public class SchemaCommandManager
{
    private readonly Stack<ISchemaCommand> _undoStack;
    private readonly Stack<ISchemaCommand> _redoStack;
    private readonly int _maxHistorySize;
    private readonly object _lock = new();
    public const int DefaultMaxHistorySize = 50;
    public event EventHandler<CommandEventArgs>? CommandExecuted;
    public event EventHandler<CommandEventArgs>? CommandUndone;
    public event EventHandler<CommandEventArgs>? CommandRedone;
    public event EventHandler? HistoryChanged;
    public SchemaCommandManager(int maxHistorySize = DefaultMaxHistorySize)
    {
        if (maxHistorySize <= 0)
        {
            throw new ArgumentException("Max history size must be positive", nameof(maxHistorySize));
        }

        _maxHistorySize = maxHistorySize;
        _undoStack = new Stack<ISchemaCommand>(_maxHistorySize);
        _redoStack = new Stack<ISchemaCommand>(_maxHistorySize);
    }
    public bool CanUndo
    {
        get
        {
            lock (_lock)
            {
                return _undoStack.Count > 0;
            }
        }
    }
    public bool CanRedo
    {
        get
        {
            lock (_lock)
            {
                return _redoStack.Count > 0;
            }
        }
    }
    public int UndoCount
    {
        get
        {
            lock (_lock)
            {
                return _undoStack.Count;
            }
        }
    }
    public int RedoCount
    {
        get
        {
            lock (_lock)
            {
                return _redoStack.Count;
            }
        }
    }
    public int MaxHistorySize => _maxHistorySize;
    public void Execute(ISchemaCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (!command.CanExecute())
        {
            throw new InvalidOperationException($"Cannot execute command: {command.Description}");
        }

        lock (_lock)
        {
            command.Execute();
            _redoStack.Clear();
            _undoStack.Push(command);
            TrimHistory();
        }

        OnCommandExecuted(command);
        OnHistoryChanged();
    }
    public ISchemaCommand? Undo()
    {
        ISchemaCommand? command;

        lock (_lock)
        {
            if (_undoStack.Count == 0)
            {
                return null;
            }

            command = _undoStack.Pop();
            
            if (command.CanUndo())
            {
                command.Undo();
                _redoStack.Push(command);
            }
            else
            {
                _undoStack.Push(command);
                return null;
            }
        }

        OnCommandUndone(command);
        OnHistoryChanged();
        return command;
    }
    public ISchemaCommand? Redo()
    {
        ISchemaCommand? command;

        lock (_lock)
        {
            if (_redoStack.Count == 0)
            {
                return null;
            }

            command = _redoStack.Pop();
            
            if (command.CanExecute())
            {
                command.Execute();
                _undoStack.Push(command);
            }
            else
            {
                _redoStack.Push(command);
                return null;
            }
        }

        OnCommandRedone(command);
        OnHistoryChanged();
        return command;
    }
    public IEnumerable<ISchemaCommand> Undo(int count)
    {
        var undone = new List<ISchemaCommand>();
        
        for (int i = 0; i < count && CanUndo; i++)
        {
            var command = Undo();
            if (command != null)
            {
                undone.Add(command);
            }
        }

        return undone;
    }
    public IEnumerable<ISchemaCommand> Redo(int count)
    {
        var redone = new List<ISchemaCommand>();
        
        for (int i = 0; i < count && CanRedo; i++)
        {
            var command = Redo();
            if (command != null)
            {
                redone.Add(command);
            }
        }

        return redone;
    }
    public void Clear()
    {
        lock (_lock)
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        OnHistoryChanged();
    }
    public IEnumerable<CommandInfo> GetUndoHistory()
    {
        lock (_lock)
        {
            return _undoStack.Select(c => new CommandInfo(c)).ToList();
        }
    }
    public IEnumerable<CommandInfo> GetRedoHistory()
    {
        lock (_lock)
        {
            return _redoStack.Select(c => new CommandInfo(c)).ToList();
        }
    }
    public string? GetNextUndoDescription()
    {
        lock (_lock)
        {
            return _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
        }
    }
    public string? GetNextRedoDescription()
    {
        lock (_lock)
        {
            return _redoStack.Count > 0 ? _redoStack.Peek().Description : null;
        }
    }

    private void TrimHistory()
    {
        while (_undoStack.Count > _maxHistorySize)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < items.Length - 1; i++)
            {
                _undoStack.Push(items[items.Length - 2 - i]);
            }
        }
    }

    protected virtual void OnCommandExecuted(ISchemaCommand command)
    {
        CommandExecuted?.Invoke(this, new CommandEventArgs(command, CommandAction.Execute));
    }

    protected virtual void OnCommandUndone(ISchemaCommand command)
    {
        CommandUndone?.Invoke(this, new CommandEventArgs(command, CommandAction.Undo));
    }

    protected virtual void OnCommandRedone(ISchemaCommand command)
    {
        CommandRedone?.Invoke(this, new CommandEventArgs(command, CommandAction.Redo));
    }

    protected virtual void OnHistoryChanged()
    {
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
public class CommandInfo
{
    public Guid Id { get; }
    public string Description { get; }
    public DateTime CreatedAt { get; }
    public bool IsExecuted { get; }

    public CommandInfo(ISchemaCommand command)
    {
        Id = command.Id;
        Description = command.Description;
        CreatedAt = command.CreatedAt;
        IsExecuted = command.IsExecuted;
    }
}
public class CommandEventArgs : EventArgs
{
    public ISchemaCommand Command { get; }
    public CommandAction Action { get; }

    public CommandEventArgs(ISchemaCommand command, CommandAction action)
    {
        Command = command;
        Action = action;
    }
}
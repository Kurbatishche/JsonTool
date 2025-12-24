namespace JsonTool.Core.Command;
public abstract class SchemaCommandBase : ISchemaCommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public abstract string Description { get; }
    public DateTime CreatedAt { get; } = DateTime.Now;
    public bool IsExecuted { get; protected set; }
    public event EventHandler<CommandExecutedEventArgs>? Executed;
    public event EventHandler<CommandExecutedEventArgs>? Undone;

    public void Execute()
    {
        if (!CanExecute())
        {
            throw new InvalidOperationException($"Cannot execute command: {Description}");
        }

        ExecuteCore();
        IsExecuted = true;
        OnExecuted();
    }

    public void Undo()
    {
        if (!CanUndo())
        {
            throw new InvalidOperationException($"Cannot undo command: {Description}");
        }

        UndoCore();
        IsExecuted = false;
        OnUndone();
    }

    public virtual bool CanExecute() => !IsExecuted;
    public virtual bool CanUndo() => IsExecuted;
    protected abstract void ExecuteCore();
    protected abstract void UndoCore();

    protected virtual void OnExecuted()
    {
        Executed?.Invoke(this, new CommandExecutedEventArgs(this, CommandAction.Execute));
    }

    protected virtual void OnUndone()
    {
        Undone?.Invoke(this, new CommandExecutedEventArgs(this, CommandAction.Undo));
    }
}
public enum CommandAction
{
    Execute,
    Undo,
    Redo
}
public class CommandExecutedEventArgs : EventArgs
{
    public ISchemaCommand Command { get; }
    public CommandAction Action { get; }
    public DateTime Timestamp { get; } = DateTime.Now;

    public CommandExecutedEventArgs(ISchemaCommand command, CommandAction action)
    {
        Command = command;
        Action = action;
    }
}
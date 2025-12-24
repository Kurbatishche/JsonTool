namespace JsonTool.Core.Command;
public interface ISchemaCommand
{
    Guid Id { get; }
    string Description { get; }
    DateTime CreatedAt { get; }
    bool IsExecuted { get; }
    void Execute();
    void Undo();
    bool CanExecute();
    bool CanUndo();
}
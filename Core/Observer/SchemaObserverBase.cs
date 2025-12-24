namespace JsonTool.Core.Observer;
public abstract class SchemaObserverBase : ISchemaObserver
{
    public abstract string ObserverId { get; }
    public virtual int Priority => 100;
    public bool IsEnabled { get; set; } = true;

    public virtual void OnSchemaChanged(SchemaChangeNotification notification)
    {
    }

    public virtual void OnSchemaSaved(SchemaSaveNotification notification)
    {
    }

    public virtual void OnSchemaLoaded(SchemaLoadNotification notification)
    {
    }

    public virtual void OnCommandExecuted(CommandNotification notification)
    {
    }
}
namespace JsonTool.Core.Observer;
public interface ISchemaSubject
{
    void Attach(ISchemaObserver observer);
    void Detach(ISchemaObserver observer);
    void Detach(string observerId);
    void NotifySchemaChanged(SchemaChangeNotification notification);
    void NotifySchemaSaved(SchemaSaveNotification notification);
    void NotifySchemaLoaded(SchemaLoadNotification notification);
    void NotifyCommandExecuted(CommandNotification notification);
    IReadOnlyList<ISchemaObserver> GetObservers();
    bool IsAttached(string observerId);
    void SuspendNotifications();
    void ResumeNotifications();
}
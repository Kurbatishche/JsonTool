namespace JsonTool.Core.Observer;
public interface IDocumentSubject
{
    void Attach(IDocumentObserver observer);
    void Detach(IDocumentObserver observer);
    void NotifyDocumentChanged();
    void NotifyDocumentSaved();
    void NotifyValidationCompleted();
    void NotifyPropertyChanged();
}
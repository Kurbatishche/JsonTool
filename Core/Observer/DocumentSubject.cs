using JsonTool.Core.Models;

namespace JsonTool.Core.Observer;
public class DocumentSubject : IDocumentSubject
{
    private readonly List<IDocumentObserver> _observers = new();
    private JsonSchemaDocument? _currentDocument;
    private ValidationResult? _lastValidationResult;
    private JsonPropertyMetadata? _lastChangedProperty;

    public JsonSchemaDocument? CurrentDocument
    {
        get => _currentDocument;
        set
        {
            _currentDocument = value;
            NotifyDocumentChanged();
        }
    }

    public ValidationResult? LastValidationResult
    {
        get => _lastValidationResult;
        set
        {
            _lastValidationResult = value;
            NotifyValidationCompleted();
        }
    }

    public JsonPropertyMetadata? LastChangedProperty
    {
        get => _lastChangedProperty;
        set
        {
            _lastChangedProperty = value;
            NotifyPropertyChanged();
        }
    }

    public void Attach(IDocumentObserver observer)
    {
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
    }

    public void Detach(IDocumentObserver observer)
    {
        _observers.Remove(observer);
    }

    public void NotifyDocumentChanged()
    {
        if (_currentDocument == null) return;
        foreach (var observer in _observers.ToList())
        {
            observer.OnDocumentChanged(_currentDocument);
        }
    }

    public void NotifyDocumentSaved()
    {
        if (_currentDocument == null) return;
        foreach (var observer in _observers.ToList())
        {
            observer.OnDocumentSaved(_currentDocument);
        }
    }

    public void NotifyValidationCompleted()
    {
        if (_lastValidationResult == null) return;
        foreach (var observer in _observers.ToList())
        {
            observer.OnValidationCompleted(_lastValidationResult);
        }
    }

    public void NotifyPropertyChanged()
    {
        if (_lastChangedProperty == null) return;
        foreach (var observer in _observers.ToList())
        {
            observer.OnPropertyChanged(_lastChangedProperty);
        }
    }
}
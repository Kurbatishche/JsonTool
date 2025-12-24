using JsonTool.Core.Models;

namespace JsonTool.Core.Observer;
public interface IDocumentObserver
{
    void OnDocumentChanged(JsonSchemaDocument document);
    void OnDocumentSaved(JsonSchemaDocument document);
    void OnValidationCompleted(ValidationResult result);
    void OnPropertyChanged(JsonPropertyMetadata property);
}
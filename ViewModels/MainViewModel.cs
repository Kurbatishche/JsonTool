using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using JsonTool.Core.Command;
using JsonTool.Core.Flyweight;
using JsonTool.Core.Models;
using JsonTool.Core.Observer;
using JsonTool.Core.Strategy;
using JsonTool.Services;
using Microsoft.Win32;

namespace JsonTool.ViewModels;
public class MainViewModel : ViewModelBase, IDocumentObserver
{
    private readonly IJsonSchemaService _schemaService;
    private readonly DocumentSubject _documentSubject;
    private readonly CommandInvoker _commandInvoker;
    private readonly TextFormatFactory _formatFactory;
    private readonly SyntaxHighlighter _syntaxHighlighter;
    private readonly AutoSaveObserver _autoSaveObserver;

    private JsonSchemaDocument? _currentDocument;
    private JsonPropertyMetadata? _selectedProperty;
    private string _jsonContent = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isModified;
    private bool _isFlatView;
    private IJsonViewStrategy _currentViewStrategy;
    private IExportStrategy _currentExportStrategy;

    public MainViewModel(
        IJsonSchemaService schemaService,
        DocumentSubject documentSubject,
        CommandInvoker commandInvoker,
        TextFormatFactory formatFactory)
    {
        _schemaService = schemaService;
        _documentSubject = documentSubject;
        _commandInvoker = commandInvoker;
        _formatFactory = formatFactory;
        _syntaxHighlighter = new SyntaxHighlighter(formatFactory);
        _currentViewStrategy = new TreeViewStrategy();
        _currentExportStrategy = new MarkdownExportStrategy();
        ViewStrategies = new ObservableCollection<IJsonViewStrategy>
        {
            new TreeViewStrategy(),
            new FlatViewStrategy()
        };
        ExportStrategies = new ObservableCollection<IExportStrategy>
        {
            new MarkdownExportStrategy(),
            new JsonExportStrategy()
        };
        _documentSubject.Attach(this);
        _autoSaveObserver = new AutoSaveObserver(async () => await SaveDocumentAsync());
        InitializeCommands();

        Properties = new ObservableCollection<JsonPropertyMetadata>();
        ValidationErrors = new ObservableCollection<string>();
    }

    #region Properties

    public JsonSchemaDocument? CurrentDocument
    {
        get => _currentDocument;
        set => SetProperty(ref _currentDocument, value);
    }

    public ObservableCollection<JsonPropertyMetadata> Properties { get; }
    public ObservableCollection<string> ValidationErrors { get; }
    public ObservableCollection<IJsonViewStrategy> ViewStrategies { get; }
    public ObservableCollection<IExportStrategy> ExportStrategies { get; }

    public List<string> AvailableTypes { get; } = new()
    {
        "string", "integer", "number", "boolean", "array", "object", "null"
    };

    public List<string> AvailableFormats { get; } = new()
    {
        "", "date-time", "date", "time", "email", "uri", "uuid", "hostname", 
        "ipv4", "ipv6", "regex", "json-pointer", "uri-reference"
    };

    public JsonPropertyMetadata? SelectedProperty
    {
        get => _selectedProperty;
        set
        {
            if (SetProperty(ref _selectedProperty, value))
            {
                OnPropertyChanged(nameof(HasSelectedProperty));
            }
        }
    }

    public bool HasSelectedProperty => SelectedProperty != null;

    public string JsonContent
    {
        get => _jsonContent;
        set
        {
            if (SetProperty(ref _jsonContent, value))
            {
                OnJsonContentChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsModified
    {
        get => _isModified;
        set => SetProperty(ref _isModified, value);
    }

    private double _editorFontSize = 14;
    public double EditorFontSize
    {
        get => _editorFontSize;
        set => SetProperty(ref _editorFontSize, value);
    }

    public bool IsFlatView
    {
        get => _isFlatView;
        set
        {
            if (SetProperty(ref _isFlatView, value))
            {
                _currentViewStrategy = value ? new FlatViewStrategy() : new TreeViewStrategy();
                RefreshPropertiesView();
            }
        }
    }

    public IJsonViewStrategy CurrentViewStrategy
    {
        get => _currentViewStrategy;
        set
        {
            if (SetProperty(ref _currentViewStrategy, value))
            {
                RefreshPropertiesView();
            }
        }
    }

    public IExportStrategy CurrentExportStrategy
    {
        get => _currentExportStrategy;
        set => SetProperty(ref _currentExportStrategy, value);
    }

    public bool CanUndo => _commandInvoker.CanUndo;
    public bool CanRedo => _commandInvoker.CanRedo;

    #endregion

    #region Commands

    public ICommand OpenFileCommand { get; private set; } = null!;
    public ICommand SaveFileCommand { get; private set; } = null!;
    public ICommand ValidateSchemaCommand { get; private set; } = null!;
    public ICommand ValidateDataCommand { get; private set; } = null!;
    public ICommand ExportCommand { get; private set; } = null!;
    public ICommand ExportMarkdownCommand { get; private set; } = null!;
    public ICommand UndoCommand { get; private set; } = null!;
    public ICommand RedoCommand { get; private set; } = null!;
    public ICommand ToggleViewCommand { get; private set; } = null!;
    public ICommand UpdatePropertyCommand { get; private set; } = null!;
    public ICommand ApplyPropertyChangesCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
        SaveFileCommand = new AsyncRelayCommand(SaveDocumentAsync, () => CurrentDocument != null);
        ValidateSchemaCommand = new AsyncRelayCommand(ValidateSchemaAsync, () => !string.IsNullOrEmpty(JsonContent));
        ValidateDataCommand = new AsyncRelayCommand(ValidateDataAsync, () => CurrentDocument != null);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => CurrentDocument != null);
        ExportMarkdownCommand = new AsyncRelayCommand(ExportMarkdownAsync, () => !string.IsNullOrEmpty(JsonContent));
        UndoCommand = new RelayCommand(() => Undo(), () => CanUndo);
        RedoCommand = new RelayCommand(() => Redo(), () => CanRedo);
        ToggleViewCommand = new RelayCommand(() => IsFlatView = !IsFlatView);
        UpdatePropertyCommand = new RelayCommand(UpdateSelectedProperty, _ => HasSelectedProperty);
        ApplyPropertyChangesCommand = new AsyncRelayCommand(ApplyPropertyChangesAsync, () => HasSelectedProperty);

        _commandInvoker.CommandExecuted += (_, _) => RefreshCommandStates();
        _commandInvoker.UndoExecuted += (_, _) => RefreshCommandStates();
        _commandInvoker.RedoExecuted += (_, _) => RefreshCommandStates();
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    #endregion

    #region Methods

    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Schema Files|*.json|All Files|*.*",
            Title = "Open JSON Schema"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                StatusMessage = "Loading...";
                CurrentDocument = await _schemaService.LoadSchemaAsync(dialog.FileName);
                JsonContent = CurrentDocument.RawContent;
                _documentSubject.CurrentDocument = CurrentDocument;
                RefreshPropertiesView();
                UpdateValidationErrors();
                StatusMessage = $"Loaded: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }

    private async Task SaveDocumentAsync()
    {
        if (CurrentDocument == null) return;

        try
        {
            CurrentDocument.RawContent = JsonContent;
            await _schemaService.SaveSchemaAsync(CurrentDocument);
            IsModified = false;
            _documentSubject.NotifyDocumentSaved();
            StatusMessage = $"Saved: {Path.GetFileName(CurrentDocument.FilePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    private async Task ValidateSchemaAsync()
    {
        try
        {
            StatusMessage = "Validating schema...";
            var result = await _schemaService.ValidateSchemaAsync(JsonContent);
            _documentSubject.LastValidationResult = result;

            if (result.IsValid)
            {
                StatusMessage = "Schema is valid";
            }
            else
            {
                StatusMessage = $"Validation failed: {result.Errors.Count} error(s)";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Validation error: {ex.Message}";
        }
    }

    private async Task ValidateDataAsync()
    {
        if (CurrentDocument == null) return;

        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files|*.json|All Files|*.*",
            Title = "Select JSON Data to Validate"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var jsonData = await File.ReadAllTextAsync(dialog.FileName);
                var result = await _schemaService.ValidateDataAgainstSchemaAsync(jsonData, JsonContent);
                _documentSubject.LastValidationResult = result;

                StatusMessage = result.IsValid
                    ? "Data is valid against schema"
                    : $"Data validation failed: {result.Errors.Count} error(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }

    private async Task ExportAsync()
    {
        if (CurrentDocument == null) return;

        var dialog = new SaveFileDialog
        {
            Filter = $"{CurrentExportStrategy.ExportName} Files|*{CurrentExportStrategy.FileExtension}",
            Title = "Export Schema",
            FileName = Path.GetFileNameWithoutExtension(CurrentDocument.FilePath)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var context = new ExportContext(CurrentExportStrategy);
                await context.ExportToFileAsync(CurrentDocument, dialog.FileName);
                StatusMessage = $"Exported to: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export error: {ex.Message}";
            }
        }
    }

    private async Task ExportMarkdownAsync()
    {
        if (string.IsNullOrEmpty(JsonContent)) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown Files|*.md|All Files|*.*",
            Title = "Export to Markdown",
            FileName = CurrentDocument != null 
                ? Path.GetFileNameWithoutExtension(CurrentDocument.FilePath) + ".md"
                : "schema.md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                StatusMessage = "Exporting to Markdown...";
                var exporter = new Core.TemplateMethod.MarkdownTableExporter();
                var result = exporter.ProcessJson(JsonContent);

                if (result.Success)
                {
                    await File.WriteAllTextAsync(dialog.FileName, result.Output);
                    StatusMessage = $"Exported to: {Path.GetFileName(dialog.FileName)}";
                }
                else
                {
                    StatusMessage = $"Export failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export error: {ex.Message}";
            }
        }
    }

    private void Undo()
    {
        _commandInvoker.Undo();
        if (CurrentDocument != null)
        {
            JsonContent = CurrentDocument.RawContent;
        }
    }

    private void Redo()
    {
        _commandInvoker.Redo();
        if (CurrentDocument != null)
        {
            JsonContent = CurrentDocument.RawContent;
        }
    }

    private void UpdateSelectedProperty(object? parameter)
    {
        if (SelectedProperty == null || CurrentDocument == null) return;

        var command = new EditPropertyCommand(
            SelectedProperty,
            "Description",
            SelectedProperty.Description,
            SelectedProperty.Description,
            prop => _documentSubject.LastChangedProperty = prop);

        _commandInvoker.ExecuteCommand(command);
        var updatedContent = _schemaService.UpdateSchemaWithMetadata(JsonContent, SelectedProperty);
        if (updatedContent != JsonContent)
        {
            var contentCommand = new UpdateSchemaContentCommand(
                CurrentDocument,
                updatedContent,
                doc => _documentSubject.NotifyDocumentChanged());
            _commandInvoker.ExecuteCommand(contentCommand);
            JsonContent = updatedContent;
        }
    }

    private async Task ApplyPropertyChangesAsync()
    {
        if (SelectedProperty == null || string.IsNullOrEmpty(JsonContent)) return;

        try
        {
            StatusMessage = "Applying changes...";
            var updatedContent = await Task.Run(() => 
                _schemaService.UpdateSchemaWithMetadata(JsonContent, SelectedProperty));
            
            if (updatedContent != JsonContent)
            {
                JsonContent = updatedContent;
                IsModified = true;
                StatusMessage = $"Property '{SelectedProperty.Name}' updated";
            }
            else
            {
                StatusMessage = "No changes to apply";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying changes: {ex.Message}";
        }
    }

    private void OnJsonContentChanged()
    {
        IsModified = true;
        if (CurrentDocument != null)
        {
            CurrentDocument.RawContent = JsonContent;
            CurrentDocument.IsModified = true;
            _documentSubject.NotifyDocumentChanged();
        }
    }

    private void RefreshPropertiesView()
    {
        Properties.Clear();
        if (CurrentDocument == null) return;

        var transformedProperties = _currentViewStrategy.TransformForView(CurrentDocument);
        foreach (var prop in transformedProperties)
        {
            Properties.Add(prop);
        }
    }

    private void UpdateValidationErrors()
    {
        ValidationErrors.Clear();
        if (CurrentDocument?.ValidationErrors != null)
        {
            foreach (var error in CurrentDocument.ValidationErrors)
            {
                ValidationErrors.Add(error);
            }
        }
    }

    #endregion

    #region IDocumentObserver Implementation

    public void OnDocumentChanged(JsonSchemaDocument document)
    {
        IsModified = document.IsModified;
    }

    public void OnDocumentSaved(JsonSchemaDocument document)
    {
        IsModified = false;
        StatusMessage = "Document saved";
    }

    public void OnValidationCompleted(ValidationResult result)
    {
        ValidationErrors.Clear();
        foreach (var error in result.Errors)
        {
            ValidationErrors.Add($"[{error.Path}] {error.Message}");
        }
        foreach (var warning in result.Warnings)
        {
            ValidationErrors.Add($"[Warning] [{warning.Path}] {warning.Message}");
        }
    }

    public void OnPropertyChanged(JsonPropertyMetadata property)
    {
        StatusMessage = $"Property '{property.Name}' updated";
    }

    #endregion
}
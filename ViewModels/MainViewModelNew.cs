using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonTool.Core.Command;
using JsonTool.Core.Models;
using JsonTool.Core.Observer;
using JsonTool.Core.Strategy.Validation;
using JsonTool.Core.TemplateMethod;
using Microsoft.Win32;

namespace JsonTool.ViewModels;
public class MainViewModelNew : ViewModelBase, IDisposable
{
    #region Fields

    private readonly SchemaCommandManager _commandManager;
    private readonly SchemaChangeNotifier _notifier;
    private readonly AutoSaveObserver _autoSaveObserver;
    private readonly HistoryObserver _historyObserver;
    private readonly ValidationContext _validationContext;

    private string _jsonContent = string.Empty;
    private string _currentFilePath = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isModified;
    private bool _isAutoSaveEnabled = true;
    private bool _isFlatView;
    private bool _isPropertiesPanelVisible = true;
    private bool _isValidationPanelVisible = true;
    private double _editorFontSize = 14;
    private JsonPropertyMetadata? _selectedProperty;
    private string? _selectedValidationError;
    private JObject? _currentSchema;

    #endregion

    #region Constructor

    public MainViewModelNew()
    {
        _commandManager = new SchemaCommandManager(maxHistorySize: 50);
        _commandManager.HistoryChanged += (s, e) => RefreshCommandStates();
        _notifier = new SchemaChangeNotifier();
        _historyObserver = new HistoryObserver();
        _autoSaveObserver = new AutoSaveObserver(
            saveAction: async () => await SaveFileInternalAsync(),
            saveDelay: TimeSpan.FromSeconds(3)
        );

        _notifier.Attach(_historyObserver);
        _notifier.Attach(_autoSaveObserver);
        _notifier.IntegrateWithCommandManager(_commandManager);
        _autoSaveObserver.AfterSave += (s, e) => StatusMessage = "Auto-saved";
        _autoSaveObserver.SaveError += (s, e) => StatusMessage = $"Auto-save failed: {e.Exception?.Message}";
        _validationContext = new ValidationContext(new SyntaxValidationStrategy());
        Properties = new ObservableCollection<JsonPropertyMetadata>();
        ValidationErrors = new ObservableCollection<string>();
        RecentFiles = new ObservableCollection<string>();
        UndoHistory = new ObservableCollection<CommandInfo>();
        AvailableTypes = new ObservableCollection<string>
        {
            "string", "integer", "number", "boolean", "object", "array", "null"
        };
        AvailableFormats = new ObservableCollection<string>
        {
            "", "email", "uri", "date", "date-time", "time", "uuid", "hostname", "ipv4", "ipv6"
        };
        InitializeCommands();
    }

    #endregion

    #region Properties

    public ObservableCollection<JsonPropertyMetadata> Properties { get; }
    public ObservableCollection<string> ValidationErrors { get; }
    public ObservableCollection<string> RecentFiles { get; }
    public ObservableCollection<CommandInfo> UndoHistory { get; }
    public ObservableCollection<string> AvailableTypes { get; }
    public ObservableCollection<string> AvailableFormats { get; }

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

    public string CurrentFilePath
    {
        get => _currentFilePath;
        set
        {
            if (SetProperty(ref _currentFilePath, value))
            {
                OnPropertyChanged(nameof(CurrentFileName));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string CurrentFileName => string.IsNullOrEmpty(CurrentFilePath) 
        ? "Untitled" 
        : Path.GetFileName(CurrentFilePath);

    public string WindowTitle
    {
        get
        {
            var modified = IsModified ? " *" : "";
            var fileName = CurrentFileName;
            return $"JSON Tool - {fileName}{modified}";
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
        set
        {
            if (SetProperty(ref _isModified, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public bool IsAutoSaveEnabled
    {
        get => _isAutoSaveEnabled;
        set
        {
            if (SetProperty(ref _isAutoSaveEnabled, value))
            {
                _autoSaveObserver.IsEnabled = value;
                StatusMessage = value ? "Auto-save enabled" : "Auto-save disabled";
            }
        }
    }

    public bool IsFlatView
    {
        get => _isFlatView;
        set
        {
            if (SetProperty(ref _isFlatView, value))
            {
                RefreshPropertiesView();
            }
        }
    }

    public bool IsPropertiesPanelVisible
    {
        get => _isPropertiesPanelVisible;
        set => SetProperty(ref _isPropertiesPanelVisible, value);
    }

    public bool IsValidationPanelVisible
    {
        get => _isValidationPanelVisible;
        set => SetProperty(ref _isValidationPanelVisible, value);
    }

    public double EditorFontSize
    {
        get => _editorFontSize;
        set => SetProperty(ref _editorFontSize, Math.Clamp(value, 8, 48));
    }

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

    public string? SelectedValidationError
    {
        get => _selectedValidationError;
        set => SetProperty(ref _selectedValidationError, value);
    }

    public int UndoCount => _commandManager.UndoCount;
    public int RedoCount => _commandManager.RedoCount;
    public bool CanUndo => _commandManager.CanUndo;
    public bool CanRedo => _commandManager.CanRedo;

    #endregion

    #region Commands

    public ICommand OpenFileCommand { get; private set; } = null!;
    public ICommand SaveFileCommand { get; private set; } = null!;
    public ICommand SaveAsFileCommand { get; private set; } = null!;
    public ICommand UndoCommand { get; private set; } = null!;
    public ICommand RedoCommand { get; private set; } = null!;
    public ICommand ValidateSchemaCommand { get; private set; } = null!;
    public ICommand ValidateDataCommand { get; private set; } = null!;
    public ICommand ExportMarkdownCommand { get; private set; } = null!;
    public ICommand FormatJsonCommand { get; private set; } = null!;
    public ICommand MinifyJsonCommand { get; private set; } = null!;
    public ICommand IncreaseFontSizeCommand { get; private set; } = null!;
    public ICommand DecreaseFontSizeCommand { get; private set; } = null!;
    public ICommand ResetFontSizeCommand { get; private set; } = null!;
    public ICommand ApplyPropertyChangesCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        OpenFileCommand = new RelayCommand(async _ => await OpenFileAsync());
        SaveFileCommand = new RelayCommand(async _ => await SaveFileAsync(), _ => !string.IsNullOrEmpty(JsonContent));
        SaveAsFileCommand = new RelayCommand(async _ => await SaveFileAsAsync(), _ => !string.IsNullOrEmpty(JsonContent));
        
        UndoCommand = new RelayCommand(_ => ExecuteUndo(), _ => CanUndo);
        RedoCommand = new RelayCommand(_ => ExecuteRedo(), _ => CanRedo);
        
        ValidateSchemaCommand = new RelayCommand(async _ => await ValidateSchemaAsync(), _ => !string.IsNullOrEmpty(JsonContent));
        ValidateDataCommand = new RelayCommand(async _ => await ValidateDataAsync(), _ => _currentSchema != null);
        
        ExportMarkdownCommand = new RelayCommand(async _ => await ExportToMarkdownAsync(), _ => _currentSchema != null);
        
        FormatJsonCommand = new RelayCommand(_ => FormatJson(), _ => !string.IsNullOrEmpty(JsonContent));
        MinifyJsonCommand = new RelayCommand(_ => MinifyJson(), _ => !string.IsNullOrEmpty(JsonContent));
        
        IncreaseFontSizeCommand = new RelayCommand(_ => EditorFontSize += 2);
        DecreaseFontSizeCommand = new RelayCommand(_ => EditorFontSize -= 2);
        ResetFontSizeCommand = new RelayCommand(_ => EditorFontSize = 14);
        
        ApplyPropertyChangesCommand = new RelayCommand(_ => ApplyPropertyChanges(), _ => HasSelectedProperty);
    }

    #endregion

    #region Command Implementations

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
                var content = await File.ReadAllTextAsync(dialog.FileName);
                
                JsonContent = content;
                CurrentFilePath = dialog.FileName;
                IsModified = false;
                
                ParseSchema();
                AddToRecentFiles(dialog.FileName);
                
                _notifier.NotifySchemaLoaded(new SchemaLoadNotification
                {
                    FilePath = dialog.FileName,
                    Success = true,
                    PropertiesCount = Properties.Count
                });

                StatusMessage = $"Loaded: {CurrentFileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ValidationErrors.Clear();
                ValidationErrors.Add($"Failed to load file: {ex.Message}");
            }
        }
    }

    private async Task SaveFileAsync()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            await SaveFileAsAsync();
            return;
        }

        await SaveFileInternalAsync();
    }

    private async Task SaveFileAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Schema Files|*.json|All Files|*.*",
            Title = "Save JSON Schema",
            FileName = CurrentFileName
        };

        if (dialog.ShowDialog() == true)
        {
            CurrentFilePath = dialog.FileName;
            await SaveFileInternalAsync();
        }
    }

    private async Task SaveFileInternalAsync()
    {
        if (string.IsNullOrEmpty(CurrentFilePath)) return;

        try
        {
            await File.WriteAllTextAsync(CurrentFilePath, JsonContent);
            IsModified = false;
            
            _notifier.NotifySchemaSaved(new SchemaSaveNotification
            {
                FilePath = CurrentFilePath,
                Success = true
            });

            StatusMessage = $"Saved: {CurrentFileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
            
            _notifier.NotifySchemaSaved(new SchemaSaveNotification
            {
                FilePath = CurrentFilePath,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private void ExecuteUndo()
    {
        var command = _commandManager.Undo();
        if (command != null)
        {
            if (_currentSchema != null)
            {
                JsonContent = _currentSchema.ToString(Formatting.Indented);
            }
            StatusMessage = $"Undone: {command.Description}";
        }
        RefreshUndoHistory();
    }

    private void ExecuteRedo()
    {
        var command = _commandManager.Redo();
        if (command != null)
        {
            if (_currentSchema != null)
            {
                JsonContent = _currentSchema.ToString(Formatting.Indented);
            }
            StatusMessage = $"Redone: {command.Description}";
        }
        RefreshUndoHistory();
    }

    private async Task ValidateSchemaAsync()
    {
        ValidationErrors.Clear();
        
        try
        {
            StatusMessage = "Validating schema...";
            _validationContext.SetStrategy(new SchemaValidationStrategy());
            var result = await Task.Run(() => _validationContext.Validate(JsonContent));

            if (result.IsValid)
            {
                StatusMessage = "Schema is valid";
                ValidationErrors.Add("✓ Schema is valid");
            }
            else
            {
                StatusMessage = $"Validation failed: {result.Errors.Count} error(s)";
                foreach (var error in result.Errors)
                {
                    ValidationErrors.Add($"[{error.Path}] {error.Message}");
                }
            }

            foreach (var warning in result.Warnings)
            {
                ValidationErrors.Add($"[Warning] {warning.Message}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Validation error: {ex.Message}";
            ValidationErrors.Add($"Validation error: {ex.Message}");
        }
    }

    private async Task ValidateDataAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files|*.json|All Files|*.*",
            Title = "Select JSON Data to Validate"
        };

        if (dialog.ShowDialog() == true)
        {
            ValidationErrors.Clear();
            
            try
            {
                var jsonData = await File.ReadAllTextAsync(dialog.FileName);
                
                _validationContext.SetStrategy(new JsonBySchemaValidationStrategy(JsonContent));
                var result = await Task.Run(() => _validationContext.Validate(jsonData));

                if (result.IsValid)
                {
                    StatusMessage = "Data is valid against schema";
                    ValidationErrors.Add("✓ Data is valid against schema");
                }
                else
                {
                    StatusMessage = $"Data validation failed: {result.Errors.Count} error(s)";
                    foreach (var error in result.Errors)
                    {
                        ValidationErrors.Add($"[{error.Path}] {error.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ValidationErrors.Add($"Error: {ex.Message}");
            }
        }
    }

    private async Task ExportToMarkdownAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown Files|*.md|All Files|*.*",
            Title = "Export to Markdown",
            FileName = Path.GetFileNameWithoutExtension(CurrentFilePath) + ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                StatusMessage = "Exporting to Markdown...";
                var exporter = new MarkdownExporter
                {
                    IncludeTableOfContents = true,
                    IncludeExamples = true,
                    IncludeJsonExample = true
                };

                var result = await Task.Run(() => exporter.ProcessJson(JsonContent));

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

    private void FormatJson()
    {
        try
        {
            var obj = JToken.Parse(JsonContent);
            JsonContent = obj.ToString(Formatting.Indented);
            StatusMessage = "JSON formatted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Format error: {ex.Message}";
        }
    }

    private void MinifyJson()
    {
        try
        {
            var obj = JToken.Parse(JsonContent);
            JsonContent = obj.ToString(Formatting.None);
            StatusMessage = "JSON minified";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Minify error: {ex.Message}";
        }
    }

    private void ApplyPropertyChanges()
    {
        if (SelectedProperty == null || _currentSchema == null) return;

        try
        {
            var command = new UpdateMetadataCommand(
                _currentSchema,
                $"properties.{SelectedProperty.Name}",
                new PropertyMetadata
                {
                    Description = SelectedProperty.Description,
                    Type = SelectedProperty.DataType,
                    Format = SelectedProperty.Format,
                    Example = SelectedProperty.Example
                });

            _commandManager.Execute(command);
            JsonContent = _currentSchema.ToString(Formatting.Indented);
            
            StatusMessage = $"Property '{SelectedProperty.Name}' updated";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating property: {ex.Message}";
        }
    }

    #endregion

    #region Private Methods

    private void OnJsonContentChanged()
    {
        IsModified = true;
        ParseSchema();
        
        _notifier.NotifySchemaChanged(new SchemaChangeNotification
        {
            ChangeType = ChangeType.ContentChanged,
            IsModified = true
        });
    }

    private void ParseSchema()
    {
        Properties.Clear();

        if (string.IsNullOrWhiteSpace(JsonContent)) return;

        try
        {
            _currentSchema = JObject.Parse(JsonContent);
            var properties = _currentSchema["properties"] as JObject;
            
            if (properties == null) return;

            var required = (_currentSchema["required"] as JArray)?
                .Select(r => r.ToString())
                .ToHashSet() ?? new HashSet<string>();

            foreach (var prop in properties.Properties())
            {
                var propDef = prop.Value as JObject;
                if (propDef == null) continue;

                var metadata = new JsonPropertyMetadata
                {
                    Name = prop.Name,
                    Path = $"$.properties.{prop.Name}",
                    DataType = propDef["type"]?.ToString() ?? "unknown",
                    Description = propDef["description"]?.ToString(),
                    Format = propDef["format"]?.ToString(),
                    Example = propDef["example"]?.ToString() ?? propDef["default"]?.ToString(),
                    IsRequired = required.Contains(prop.Name)
                };

                Properties.Add(metadata);
            }
        }
        catch
        {
        }
    }

    private void RefreshPropertiesView()
    {
        if (IsFlatView)
        {
            StatusMessage = "Flat view enabled";
        }
        else
        {
            StatusMessage = "Tree view enabled";
        }
        ParseSchema();
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
        RefreshUndoHistory();
    }

    private void RefreshUndoHistory()
    {
        UndoHistory.Clear();
        foreach (var info in _commandManager.GetUndoHistory().Take(10))
        {
            UndoHistory.Add(info);
        }
    }

    private void AddToRecentFiles(string filePath)
    {
        if (RecentFiles.Contains(filePath))
        {
            RecentFiles.Remove(filePath);
        }
        RecentFiles.Insert(0, filePath);
        
        while (RecentFiles.Count > 10)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _autoSaveObserver.Dispose();
        _notifier.Dispose();
    }

    #endregion
}
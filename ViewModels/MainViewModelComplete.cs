using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonTool.Core.Command;
using JsonTool.Core.Flyweight;
using JsonTool.Core.Models;
using JsonTool.Core.Observer;
using JsonTool.Core.Strategy.Validation;
using JsonTool.Core.TemplateMethod;
using Microsoft.Win32;

namespace JsonTool.ViewModels;
public class MainViewModelComplete : ViewModelBase, IDisposable
{
    #region Fields - Pattern Instances
    private readonly SchemaCommandManager _commandManager;
    private readonly SchemaChangeNotifier _notifier;
    private readonly AutoSaveObserver _autoSaveObserver;
    private readonly HistoryObserver _historyObserver;
    private readonly UIUpdateObserver _uiUpdateObserver;
    private readonly ValidationContext _validationContext;
    private readonly SchemaPropertyFlyweightFactory _flyweightFactory;
    private readonly SchemaFlyweightParser _schemaParser;

    #endregion

    #region Fields - State

    private string _schemaText = string.Empty;
    private string _jsonText = string.Empty;
    private string _currentFilePath = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isModified;
    private bool _isAutoSaveEnabled = true;
    private bool _isFlatView;
    private bool _isPropertiesPanelVisible = true;
    private bool _isValidationPanelVisible = true;
    private bool _isBusy;
    private double _editorFontSize = 14;
    private SchemaProperty? _selectedProperty;
    private ValidationError? _selectedError;
    private JObject? _currentSchema;

    #endregion

    #region Constructor

    public MainViewModelComplete()
    {
        _commandManager = new SchemaCommandManager(maxHistorySize: 50);
        _commandManager.HistoryChanged += OnCommandHistoryChanged;
        _notifier = new SchemaChangeNotifier();
        _autoSaveObserver = new AutoSaveObserver(
            saveAction: async () => await SaveSchemaInternalAsync(),
            saveDelay: TimeSpan.FromSeconds(3)
        );
        _autoSaveObserver.AfterSave += (s, e) => StatusMessage = "Auto-saved";
        _autoSaveObserver.SaveError += (s, e) => StatusMessage = $"Auto-save failed: {e.Exception?.Message}";
        _historyObserver = new HistoryObserver(maxHistorySize: 100);
        _uiUpdateObserver = new UIUpdateObserver();
        _uiUpdateObserver.StatusUpdated += (s, e) => StatusMessage = e.Message ?? StatusMessage;
        _notifier.Attach(_autoSaveObserver);
        _notifier.Attach(_historyObserver);
        _notifier.Attach(_uiUpdateObserver);
        _notifier.IntegrateWithCommandManager(_commandManager);
        _validationContext = new ValidationContext(new SyntaxValidationStrategy());
        _flyweightFactory = new SchemaPropertyFlyweightFactory();
        _schemaParser = new SchemaFlyweightParser(_flyweightFactory);
        Properties = new ObservableCollection<SchemaProperty>();
        ValidationErrors = new ObservableCollection<ValidationError>();
        FlatProperties = new ObservableCollection<SchemaProperty>();
        RecentFiles = new ObservableCollection<string>();
        UndoHistory = new ObservableCollection<CommandInfo>();
        
        AvailableTypes = new ObservableCollection<string>
        {
            "string", "integer", "number", "boolean", "object", "array", "null"
        };
        
        AvailableFormats = new ObservableCollection<string>
        {
            "", "email", "uri", "date", "date-time", "time", "uuid", 
            "hostname", "ipv4", "ipv6", "regex", "json-pointer"
        };
        InitializeCommands();
    }

    #endregion

    #region Observable Collections
    public ObservableCollection<SchemaProperty> Properties { get; }
    public ObservableCollection<SchemaProperty> FlatProperties { get; }
    public ObservableCollection<ValidationError> ValidationErrors { get; }
    public ObservableCollection<string> RecentFiles { get; }
    public ObservableCollection<CommandInfo> UndoHistory { get; }
    public ObservableCollection<string> AvailableTypes { get; }
    public ObservableCollection<string> AvailableFormats { get; }

    #endregion

    #region Properties - Schema & JSON
    public string SchemaText
    {
        get => _schemaText;
        set
        {
            if (SetProperty(ref _schemaText, value))
            {
                OnSchemaTextChanged();
            }
        }
    }
    public string JsonText
    {
        get => _jsonText;
        set => SetProperty(ref _jsonText, value);
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
            return $"JSON Tool - {CurrentFileName}{modified}";
        }
    }

    #endregion

    #region Properties - State
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
                OnPropertyChanged(nameof(CurrentProperties));
                StatusMessage = value ? "Flat view" : "Tree view";
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
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
    public double EditorFontSize
    {
        get => _editorFontSize;
        set => SetProperty(ref _editorFontSize, Math.Clamp(value, 8, 48));
    }
    public SchemaProperty? SelectedProperty
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
    public ValidationError? SelectedError
    {
        get => _selectedError;
        set => SetProperty(ref _selectedError, value);
    }
    public ObservableCollection<SchemaProperty> CurrentProperties => 
        IsFlatView ? FlatProperties : Properties;

    #endregion

    #region Properties - Command State
    public int UndoCount => _commandManager.UndoCount;
    public int RedoCount => _commandManager.RedoCount;
    public bool CanUndo => _commandManager.CanUndo;
    public bool CanRedo => _commandManager.CanRedo;
    public string FlyweightStats => _flyweightFactory.GetCacheReport();

    #endregion

    #region Commands
    public ICommand OpenSchemaCommand { get; private set; } = null!;
    public ICommand SaveSchemaCommand { get; private set; } = null!;
    public ICommand SaveSchemaAsCommand { get; private set; } = null!;
    public ICommand NewSchemaCommand { get; private set; } = null!;
    public ICommand UndoCommand { get; private set; } = null!;
    public ICommand RedoCommand { get; private set; } = null!;
    public ICommand FormatJsonCommand { get; private set; } = null!;
    public ICommand MinifyJsonCommand { get; private set; } = null!;
    public ICommand ValidateSchemaCommand { get; private set; } = null!;
    public ICommand CheckJsonCommand { get; private set; } = null!;
    public ICommand ClearErrorsCommand { get; private set; } = null!;
    public ICommand ExportMarkdownCommand { get; private set; } = null!;
    public ICommand ExportFlatViewCommand { get; private set; } = null!;
    public ICommand ShowFlatViewCommand { get; private set; } = null!;
    public ICommand ShowTreeViewCommand { get; private set; } = null!;
    public ICommand ToggleViewCommand { get; private set; } = null!;
    public ICommand UpdatePropertyCommand { get; private set; } = null!;
    public ICommand AddPropertyCommand { get; private set; } = null!;
    public ICommand DeletePropertyCommand { get; private set; } = null!;
    public ICommand IncreaseFontSizeCommand { get; private set; } = null!;
    public ICommand DecreaseFontSizeCommand { get; private set; } = null!;
    public ICommand ResetFontSizeCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        OpenSchemaCommand = new RelayCommand(async _ => await OpenSchemaAsync());
        SaveSchemaCommand = new RelayCommand(async _ => await SaveSchemaAsync(), _ => !string.IsNullOrEmpty(SchemaText));
        SaveSchemaAsCommand = new RelayCommand(async _ => await SaveSchemaAsAsync(), _ => !string.IsNullOrEmpty(SchemaText));
        NewSchemaCommand = new RelayCommand(_ => NewSchema());
        UndoCommand = new RelayCommand(_ => ExecuteUndo(), _ => CanUndo);
        RedoCommand = new RelayCommand(_ => ExecuteRedo(), _ => CanRedo);
        FormatJsonCommand = new RelayCommand(_ => FormatSchema(), _ => !string.IsNullOrEmpty(SchemaText));
        MinifyJsonCommand = new RelayCommand(_ => MinifySchema(), _ => !string.IsNullOrEmpty(SchemaText));
        ValidateSchemaCommand = new RelayCommand(async _ => await ValidateSchemaAsync(), _ => !string.IsNullOrEmpty(SchemaText));
        CheckJsonCommand = new RelayCommand(async _ => await CheckJsonAgainstSchemaAsync(), _ => !string.IsNullOrEmpty(SchemaText));
        ClearErrorsCommand = new RelayCommand(_ => ValidationErrors.Clear());
        ExportMarkdownCommand = new RelayCommand(async _ => await ExportToMarkdownAsync(), _ => _currentSchema != null);
        ExportFlatViewCommand = new RelayCommand(async _ => await ExportFlatViewAsync(), _ => _currentSchema != null);
        ShowFlatViewCommand = new RelayCommand(_ => IsFlatView = true);
        ShowTreeViewCommand = new RelayCommand(_ => IsFlatView = false);
        ToggleViewCommand = new RelayCommand(_ => IsFlatView = !IsFlatView);
        UpdatePropertyCommand = new RelayCommand(_ => UpdateSelectedProperty(), _ => HasSelectedProperty);
        AddPropertyCommand = new RelayCommand(_ => AddNewProperty(), _ => _currentSchema != null);
        DeletePropertyCommand = new RelayCommand(_ => DeleteSelectedProperty(), _ => HasSelectedProperty);
        IncreaseFontSizeCommand = new RelayCommand(_ => EditorFontSize += 2);
        DecreaseFontSizeCommand = new RelayCommand(_ => EditorFontSize -= 2);
        ResetFontSizeCommand = new RelayCommand(_ => EditorFontSize = 14);
    }

    #endregion

    #region Command Implementations - File

    private async Task OpenSchemaAsync()
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
                IsBusy = true;
                StatusMessage = "Loading...";

                var content = await File.ReadAllTextAsync(dialog.FileName);
                SchemaText = content;
                CurrentFilePath = dialog.FileName;
                IsModified = false;

                AddToRecentFiles(dialog.FileName);

                _notifier.NotifySchemaLoaded(new SchemaLoadNotification
                {
                    FilePath = dialog.FileName,
                    Success = true,
                    PropertiesCount = Properties.Count
                });

                StatusMessage = $"Loaded: {CurrentFileName} ({Properties.Count} properties)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ValidationErrors.Add(ValidationError.SyntaxError($"Failed to load: {ex.Message}"));
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private async Task SaveSchemaAsync()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            await SaveSchemaAsAsync();
            return;
        }

        await SaveSchemaInternalAsync();
    }

    private async Task SaveSchemaAsAsync()
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
            await SaveSchemaInternalAsync();
        }
    }

    private async Task SaveSchemaInternalAsync()
    {
        if (string.IsNullOrEmpty(CurrentFilePath)) return;

        try
        {
            IsBusy = true;
            await File.WriteAllTextAsync(CurrentFilePath, SchemaText);
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
        finally
        {
            IsBusy = false;
        }
    }

    private void NewSchema()
    {
        var template = @"{
  ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
  ""$id"": ""https://example.com/schema.json"",
  ""title"": ""New Schema"",
  ""description"": ""Description of your schema"",
  ""type"": ""object"",
  ""properties"": {
    ""id"": {
      ""type"": ""integer"",
      ""description"": ""Unique identifier""
    },
    ""name"": {
      ""type"": ""string"",
      ""description"": ""Name""
    }
  },
  ""required"": [""id"", ""name""]
}";
        SchemaText = template;
        CurrentFilePath = string.Empty;
        IsModified = false;
        _commandManager.Clear();
        StatusMessage = "New schema created";
    }

    #endregion

    #region Command Implementations - Edit

    private void ExecuteUndo()
    {
        var command = _commandManager.Undo();
        if (command != null)
        {
            RefreshSchemaFromJObject();
            StatusMessage = $"Undone: {command.Description}";
        }
        RefreshUndoHistory();
    }

    private void ExecuteRedo()
    {
        var command = _commandManager.Redo();
        if (command != null)
        {
            RefreshSchemaFromJObject();
            StatusMessage = $"Redone: {command.Description}";
        }
        RefreshUndoHistory();
    }

    private void FormatSchema()
    {
        try
        {
            var obj = JToken.Parse(SchemaText);
            SchemaText = obj.ToString(Formatting.Indented);
            StatusMessage = "Schema formatted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Format error: {ex.Message}";
        }
    }

    private void MinifySchema()
    {
        try
        {
            var obj = JToken.Parse(SchemaText);
            SchemaText = obj.ToString(Formatting.None);
            StatusMessage = "Schema minified";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Minify error: {ex.Message}";
        }
    }

    #endregion

    #region Command Implementations - Validation (STRATEGY PATTERN)

    private async Task ValidateSchemaAsync()
    {
        ValidationErrors.Clear();

        try
        {
            IsBusy = true;
            StatusMessage = "Validating schema...";
            _validationContext.SetStrategy(new SchemaValidationStrategy());
            var result = await Task.Run(() => _validationContext.Validate(SchemaText));

            if (result.IsValid)
            {
                ValidationErrors.Add(ValidationError.Success("Schema is valid"));
                StatusMessage = "Schema is valid";
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ValidationErrors.Add(new ValidationError
                    {
                        ErrorType = ValidationErrorType.Error,
                        Message = error.Message,
                        Path = error.Path,
                        LineNumber = error.LineNumber,
                        LinePosition = error.LinePosition
                    });
                }
                StatusMessage = $"Validation failed: {result.Errors.Count} error(s)";
            }

            foreach (var warning in result.Warnings)
            {
                ValidationErrors.Add(ValidationError.Warning(warning.Message, warning.Path));
            }
        }
        catch (Exception ex)
        {
            ValidationErrors.Add(ValidationError.SyntaxError(ex.Message));
            StatusMessage = $"Validation error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckJsonAgainstSchemaAsync()
    {
        if (string.IsNullOrWhiteSpace(JsonText))
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files|*.json|All Files|*.*",
                Title = "Select JSON Data to Validate"
            };

            if (dialog.ShowDialog() == true)
            {
                JsonText = await File.ReadAllTextAsync(dialog.FileName);
            }
            else
            {
                return;
            }
        }

        ValidationErrors.Clear();

        try
        {
            IsBusy = true;
            StatusMessage = "Checking JSON against schema...";
            _validationContext.SetStrategy(new JsonBySchemaValidationStrategy(SchemaText));
            var result = await Task.Run(() => _validationContext.Validate(JsonText));

            if (result.IsValid)
            {
                ValidationErrors.Add(ValidationError.Success("JSON is valid against schema"));
                StatusMessage = "JSON is valid against schema";
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ValidationErrors.Add(new ValidationError
                    {
                        ErrorType = ValidationErrorType.Error,
                        Message = error.Message,
                        Path = error.Path
                    });
                }
                StatusMessage = $"JSON validation failed: {result.Errors.Count} error(s)";
            }
        }
        catch (Exception ex)
        {
            ValidationErrors.Add(ValidationError.SyntaxError(ex.Message));
            StatusMessage = $"Check error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Command Implementations - Export (TEMPLATE METHOD PATTERN)

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
                IsBusy = true;
                StatusMessage = "Exporting to Markdown...";
                var exporter = new MarkdownExporter
                {
                    IncludeTableOfContents = true,
                    IncludeExamples = true,
                    IncludeJsonExample = true
                };

                var result = await Task.Run(() => exporter.ProcessJson(SchemaText));

                if (result.Success)
                {
                    await File.WriteAllTextAsync(dialog.FileName, result.Output);
                    StatusMessage = $"Exported to: {Path.GetFileName(dialog.FileName)}";
                }
                else
                {
                    StatusMessage = $"Export failed: {result.ErrorMessage}";
                    ValidationErrors.Add(ValidationError.SyntaxError(result.ErrorMessage ?? "Export failed"));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private async Task ExportFlatViewAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files|*.txt|JSON Files|*.json|All Files|*.*",
            Title = "Export Flat View",
            FileName = Path.GetFileNameWithoutExtension(CurrentFilePath) + "_flat.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Exporting flat view...";
                var processor = new FlatViewProcessor
                {
                    PathSeparator = "."
                };

                var result = await Task.Run(() => processor.ProcessJson(SchemaText));

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
            finally
            {
                IsBusy = false;
            }
        }
    }

    #endregion

    #region Command Implementations - Property (COMMAND PATTERN)

    private void UpdateSelectedProperty()
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
                    Type = SelectedProperty.Type,
                    Format = SelectedProperty.Format,
                    Example = SelectedProperty.Example
                });

            _commandManager.Execute(command);
            RefreshSchemaFromJObject();

            StatusMessage = $"Property '{SelectedProperty.Name}' updated";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update error: {ex.Message}";
        }
    }

    private void AddNewProperty()
    {
        if (_currentSchema == null) return;

        try
        {
            var newPropertyName = $"newProperty{Properties.Count + 1}";
            var newPropertyDef = new JObject
            {
                ["type"] = "string",
                ["description"] = "New property"
            };
            var command = new AddPropertyCommand(
                _currentSchema,
                "properties",
                newPropertyName,
                newPropertyDef);

            _commandManager.Execute(command);
            RefreshSchemaFromJObject();

            StatusMessage = $"Property '{newPropertyName}' added";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Add error: {ex.Message}";
        }
    }

    private void DeleteSelectedProperty()
    {
        if (SelectedProperty == null || _currentSchema == null) return;

        try
        {
            var command = new DeletePropertyCommand(
                _currentSchema,
                $"properties.{SelectedProperty.Name}");

            _commandManager.Execute(command);
            SelectedProperty = null;
            RefreshSchemaFromJObject();

            StatusMessage = "Property deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete error: {ex.Message}";
        }
    }

    #endregion

    #region Private Methods - Schema Parsing (FLYWEIGHT PATTERN)

    private void OnSchemaTextChanged()
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
        FlatProperties.Clear();

        if (string.IsNullOrWhiteSpace(SchemaText)) return;

        try
        {
            _currentSchema = JObject.Parse(SchemaText);
            var flyweightProperties = _schemaParser.Parse(_currentSchema);
            var required = (_currentSchema["required"] as JArray)?
                .Select(r => r.ToString())
                .ToHashSet() ?? new HashSet<string>();

            foreach (var fwProp in flyweightProperties)
            {
                var prop = ConvertToSchemaProperty(fwProp, required);
                Properties.Add(prop);
                AddToFlatList(prop);
            }

            OnPropertyChanged(nameof(CurrentProperties));
        }
        catch
        {
        }
    }

    private SchemaProperty ConvertToSchemaProperty(SchemaPropertyContext fwProp, HashSet<string> required, int depth = 0)
    {
        var prop = new SchemaProperty
        {
            Name = fwProp.Name,
            Path = fwProp.Path,
            Type = fwProp.Type,
            Format = fwProp.Format,
            Pattern = fwProp.Pattern,
            Description = fwProp.Description,
            Example = fwProp.Example,
            IsRequired = required.Contains(fwProp.Name),
            Minimum = fwProp.Minimum,
            Maximum = fwProp.Maximum,
            MinLength = fwProp.MinLength,
            MaxLength = fwProp.MaxLength,
            Depth = depth
        };

        if (fwProp.EnumValues != null)
        {
            foreach (var enumValue in fwProp.EnumValues)
            {
                prop.EnumValues.Add(enumValue);
            }
        }

        foreach (var child in fwProp.Children)
        {
            var childProp = ConvertToSchemaProperty(child, required, depth + 1);
            childProp.Parent = prop;
            prop.Children.Add(childProp);
        }

        return prop;
    }

    private void AddToFlatList(SchemaProperty prop)
    {
        FlatProperties.Add(prop);
        foreach (var child in prop.Children)
        {
            AddToFlatList(child);
        }
    }

    private void RefreshSchemaFromJObject()
    {
        if (_currentSchema != null)
        {
            SchemaText = _currentSchema.ToString(Formatting.Indented);
        }
    }

    #endregion

    #region Private Methods - Helpers

    private void OnCommandHistoryChanged(object? sender, EventArgs e)
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
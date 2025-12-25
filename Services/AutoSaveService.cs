using System.IO;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace JsonTool.Services;
public class AutoSaveService : IDisposable
{
    #region Fields

    private readonly Timer _debounceTimer;
    private readonly object _lock = new();
    private readonly AutoSaveSettings _settings;

    private string? _currentFilePath;
    private string _pendingContent = string.Empty;
    private bool _hasPendingChanges;
    private bool _isDisposed;

    #endregion

    #region Events
    public event EventHandler<AutoSaveEventArgs>? BeforeAutoSave;
    public event EventHandler<AutoSaveEventArgs>? AfterAutoSave;
    public event EventHandler<AutoSaveErrorEventArgs>? AutoSaveError;
    public event EventHandler<AutoSaveRecoveryEventArgs>? RecoveryAvailable;

    #endregion

    #region Properties
    public bool IsEnabled
    {
        get => _settings.IsEnabled;
        set
        {
            _settings.IsEnabled = value;
            if (!value)
            {
                _debounceTimer.Stop();
            }
        }
    }
    public bool HasPendingChanges => _hasPendingChanges;
    public string? CurrentFilePath => _currentFilePath;
    public string AutoSaveFilePath => _settings.GetAutoSaveFilePath(_currentFilePath);
    public AutoSaveSettings Settings => _settings;

    #endregion

    #region Constructor

    public AutoSaveService() : this(new AutoSaveSettings())
    {
    }

    public AutoSaveService(AutoSaveSettings settings)
    {
        _settings = settings;

        _debounceTimer = new Timer(_settings.DebounceDelayMs);
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += OnDebounceTimerElapsed;

        EnsureAutoSaveDirectoryExists();
    }
    public static async Task<AutoSaveService> CreateAsync()
    {
        var settings = await AutoSaveSettings.LoadAsync();
        return new AutoSaveService(settings);
    }

    #endregion

    #region Public Methods
    public void NotifyContentChanged(string content, string? filePath = null)
    {
        if (!IsEnabled || _isDisposed) return;

        lock (_lock)
        {
            _pendingContent = content;
            _currentFilePath = filePath;
            _hasPendingChanges = true;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }
    public async Task SaveNowAsync()
    {
        if (!_hasPendingChanges || _isDisposed) return;

        _debounceTimer.Stop();
        await PerformAutoSaveAsync();
    }
    public async Task<AutoSaveRecoveryInfo?> CheckForRecoveryAsync(string? originalFilePath = null)
    {
        var autoSavePath = _settings.GetAutoSaveFilePath(originalFilePath);

        if (!File.Exists(autoSavePath))
        {
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(autoSavePath);
            if (_settings.AutoSaveLifetimeHours > 0)
            {
                var age = DateTime.Now - fileInfo.LastWriteTime;
                if (age.TotalHours > _settings.AutoSaveLifetimeHours)
                {
                    await DeleteAutoSaveFileAsync(autoSavePath);
                    return null;
                }
            }

            var content = await File.ReadAllTextAsync(autoSavePath);
            var metadata = await LoadMetadataAsync(autoSavePath);

            var recoveryInfo = new AutoSaveRecoveryInfo
            {
                AutoSaveFilePath = autoSavePath,
                OriginalFilePath = metadata?.OriginalFilePath ?? originalFilePath,
                Content = content,
                LastModified = fileInfo.LastWriteTime,
                FileSize = fileInfo.Length
            };
            RecoveryAvailable?.Invoke(this, new AutoSaveRecoveryEventArgs(recoveryInfo));

            return recoveryInfo;
        }
        catch (Exception ex)
        {
            AutoSaveError?.Invoke(this, new AutoSaveErrorEventArgs(
                "Failed to check recovery file", ex, autoSavePath));
            return null;
        }
    }
    public async Task<string?> RecoverAsync(string? originalFilePath = null)
    {
        var recoveryInfo = await CheckForRecoveryAsync(originalFilePath);
        return recoveryInfo?.Content;
    }
    public async Task ClearAutoSaveAsync()
    {
        var autoSavePath = AutoSaveFilePath;
        await DeleteAutoSaveFileAsync(autoSavePath);

        lock (_lock)
        {
            _hasPendingChanges = false;
        }
    }
    public async Task ClearAllAutoSavesAsync()
    {
        var directory = _settings.GetAutoSaveDirectory();

        if (!Directory.Exists(directory)) return;

        var files = Directory.GetFiles(directory, $"*{_settings.AutoSaveSuffix}");

        foreach (var file in files)
        {
            await DeleteAutoSaveFileAsync(file);
        }
    }
    public async Task<List<AutoSaveRecoveryInfo>> GetAllAutoSavesAsync()
    {
        var result = new List<AutoSaveRecoveryInfo>();
        var directory = _settings.GetAutoSaveDirectory();

        if (!Directory.Exists(directory)) return result;

        var files = Directory.GetFiles(directory, $"*{_settings.AutoSaveSuffix}");

        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                var content = await File.ReadAllTextAsync(file);
                var metadata = await LoadMetadataAsync(file);

                result.Add(new AutoSaveRecoveryInfo
                {
                    AutoSaveFilePath = file,
                    OriginalFilePath = metadata?.OriginalFilePath,
                    Content = content,
                    LastModified = fileInfo.LastWriteTime,
                    FileSize = fileInfo.Length
                });
            }
            catch
            {
            }
        }

        return result.OrderByDescending(r => r.LastModified).ToList();
    }
    public void SetCurrentFile(string? filePath)
    {
        _currentFilePath = filePath;
    }
    public async Task SaveSettingsAsync()
    {
        await _settings.SaveAsync();
    }

    #endregion

    #region Private Methods

    private void EnsureAutoSaveDirectoryExists()
    {
        var directory = _settings.GetAutoSaveDirectory();
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async void OnDebounceTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await PerformAutoSaveAsync();
    }

    private async Task PerformAutoSaveAsync()
    {
        string content;
        string autoSavePath;

        lock (_lock)
        {
            if (!_hasPendingChanges || string.IsNullOrEmpty(_pendingContent))
            {
                return;
            }

            content = _pendingContent;
            autoSavePath = AutoSaveFilePath;
        }

        var eventArgs = new AutoSaveEventArgs(autoSavePath, _currentFilePath);

        try
        {
            BeforeAutoSave?.Invoke(this, eventArgs);

            if (eventArgs.Cancel) return;

            EnsureAutoSaveDirectoryExists();
            if (_settings.CreateBackups && File.Exists(autoSavePath))
            {
                await CreateBackupAsync(autoSavePath);
            }
            await File.WriteAllTextAsync(autoSavePath, content);
            await SaveMetadataAsync(autoSavePath, new AutoSaveMetadata
            {
                OriginalFilePath = _currentFilePath,
                SavedAt = DateTime.Now,
                ContentLength = content.Length
            });

            lock (_lock)
            {
                _hasPendingChanges = false;
            }

            eventArgs.Success = true;
            AfterAutoSave?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            AutoSaveError?.Invoke(this, new AutoSaveErrorEventArgs(
                "Auto-save failed", ex, autoSavePath));
        }
    }

    private async Task CreateBackupAsync(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        if (string.IsNullOrEmpty(directory)) return;
        var backups = Directory.GetFiles(directory, $"{fileName}.backup.*{extension}")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .ToList();

        while (backups.Count >= _settings.MaxBackupCount)
        {
            var oldest = backups.Last();
            await DeleteAutoSaveFileAsync(oldest);
            backups.Remove(oldest);
        }
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(directory, $"{fileName}.backup.{timestamp}{extension}");
        File.Copy(filePath, backupPath);
    }

    private async Task SaveMetadataAsync(string autoSavePath, AutoSaveMetadata metadata)
    {
        var metadataPath = autoSavePath + ".meta";
        var json = JsonSerializer.Serialize(metadata);
        await File.WriteAllTextAsync(metadataPath, json);
    }

    private async Task<AutoSaveMetadata?> LoadMetadataAsync(string autoSavePath)
    {
        var metadataPath = autoSavePath + ".meta";

        if (!File.Exists(metadataPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(metadataPath);
            return JsonSerializer.Deserialize<AutoSaveMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task DeleteAutoSaveFileAsync(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var metadataPath = filePath + ".meta";
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }

            await Task.CompletedTask;
        }
        catch
        {
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _debounceTimer.Stop();
        _debounceTimer.Dispose();
        if (_hasPendingChanges)
        {
            PerformAutoSaveAsync().GetAwaiter().GetResult();
        }
    }

    #endregion
}

#region Event Args
public class AutoSaveEventArgs : EventArgs
{
    public string AutoSaveFilePath { get; }
    public string? OriginalFilePath { get; }
    public bool Cancel { get; set; }
    public bool Success { get; set; }

    public AutoSaveEventArgs(string autoSaveFilePath, string? originalFilePath)
    {
        AutoSaveFilePath = autoSaveFilePath;
        OriginalFilePath = originalFilePath;
    }
}
public class AutoSaveErrorEventArgs : EventArgs
{
    public string Message { get; }
    public Exception Exception { get; }
    public string? FilePath { get; }

    public AutoSaveErrorEventArgs(string message, Exception exception, string? filePath = null)
    {
        Message = message;
        Exception = exception;
        FilePath = filePath;
    }
}
public class AutoSaveRecoveryEventArgs : EventArgs
{
    public AutoSaveRecoveryInfo RecoveryInfo { get; }

    public AutoSaveRecoveryEventArgs(AutoSaveRecoveryInfo recoveryInfo)
    {
        RecoveryInfo = recoveryInfo;
    }
}

#endregion

#region Data Models
public class AutoSaveRecoveryInfo
{
    public string AutoSaveFilePath { get; set; } = string.Empty;
    public string? OriginalFilePath { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public long FileSize { get; set; }

    public string DisplayName => string.IsNullOrEmpty(OriginalFilePath)
        ? "Untitled"
        : Path.GetFileName(OriginalFilePath);

    public string TimeSinceModified
    {
        get
        {
            var span = DateTime.Now - LastModified;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
            return $"{(int)span.TotalDays} days ago";
        }
    }
}
public class AutoSaveMetadata
{
    public string? OriginalFilePath { get; set; }
    public DateTime SavedAt { get; set; }
    public int ContentLength { get; set; }
}

#endregion
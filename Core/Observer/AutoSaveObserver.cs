using System.Timers;
using Timer = System.Timers.Timer;

namespace JsonTool.Core.Observer;
public class AutoSaveObserver : SchemaObserverBase, IDisposable
{
    private readonly Timer _debounceTimer;
    private readonly Func<Task> _saveAction;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _hasPendingChanges;
    private DateTime _lastChangeTime;
    private int _pendingChangesCount;

    public override string ObserverId => "AutoSaveObserver";
    public override int Priority => 50; // Середній пріоритет
    public TimeSpan SaveDelay { get; }
    public bool HasPendingChanges => _hasPendingChanges;
    public DateTime LastChangeTime => _lastChangeTime;
    public int PendingChangesCount => _pendingChangesCount;
    public event EventHandler<AutoSaveEventArgs>? BeforeSave;
    public event EventHandler<AutoSaveEventArgs>? AfterSave;
    public event EventHandler<AutoSaveErrorEventArgs>? SaveError;
    public AutoSaveObserver(Func<Task> saveAction, TimeSpan? saveDelay = null)
    {
        _saveAction = saveAction ?? throw new ArgumentNullException(nameof(saveAction));
        SaveDelay = saveDelay ?? TimeSpan.FromSeconds(3);

        _debounceTimer = new Timer(SaveDelay.TotalMilliseconds);
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += OnTimerElapsed;
    }
    public AutoSaveObserver(Action saveAction, TimeSpan? saveDelay = null)
        : this(() => { saveAction(); return Task.CompletedTask; }, saveDelay)
    {
    }

    public override void OnSchemaChanged(SchemaChangeNotification notification)
    {
        if (!IsEnabled || _disposed) return;

        lock (_lock)
        {
            _hasPendingChanges = true;
            _lastChangeTime = DateTime.Now;
            _pendingChangesCount++;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    public override void OnSchemaSaved(SchemaSaveNotification notification)
    {
        if (!IsEnabled || _disposed) return;

        lock (_lock)
        {
            if (notification.Success)
            {
                _hasPendingChanges = false;
                _pendingChangesCount = 0;
                _debounceTimer.Stop();
            }
        }
    }

    public override void OnCommandExecuted(CommandNotification notification)
    {
        if (!IsEnabled || _disposed) return;
        OnSchemaChanged(new SchemaChangeNotification
        {
            ChangeType = ChangeType.ContentChanged,
            IsModified = true,
            Source = notification.Command.Description
        });
    }
    public async Task SaveNowAsync()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _debounceTimer.Stop();
        }

        await PerformSaveAsync();
    }
    public void CancelPendingSave()
    {
        lock (_lock)
        {
            _debounceTimer.Stop();
            _hasPendingChanges = false;
            _pendingChangesCount = 0;
        }
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await PerformSaveAsync();
    }

    private async Task PerformSaveAsync()
    {
        if (_disposed || !_hasPendingChanges) return;

        var eventArgs = new AutoSaveEventArgs
        {
            ChangeCount = _pendingChangesCount,
            LastChangeTime = _lastChangeTime
        };

        try
        {
            BeforeSave?.Invoke(this, eventArgs);

            if (eventArgs.Cancel)
            {
                return;
            }

            await _saveAction();

            lock (_lock)
            {
                _hasPendingChanges = false;
                _pendingChangesCount = 0;
            }

            eventArgs.Success = true;
            AfterSave?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            eventArgs.Success = false;
            SaveError?.Invoke(this, new AutoSaveErrorEventArgs
            {
                ChangeCount = _pendingChangesCount,
                LastChangeTime = _lastChangeTime,
                Exception = ex
            });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _debounceTimer.Stop();
        _debounceTimer.Elapsed -= OnTimerElapsed;
        _debounceTimer.Dispose();
    }
}
public class AutoSaveEventArgs : EventArgs
{
    public int ChangeCount { get; set; }
    public DateTime LastChangeTime { get; set; }
    public bool Cancel { get; set; }
    public bool Success { get; set; }
}
public class AutoSaveErrorEventArgs : AutoSaveEventArgs
{
    public Exception? Exception { get; set; }
}
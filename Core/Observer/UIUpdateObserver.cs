using System.Windows;
using System.Windows.Threading;

namespace JsonTool.Core.Observer;
public class UIUpdateObserver : SchemaObserverBase
{
    private readonly Dispatcher? _dispatcher;
    private readonly Action<UIUpdateEventArgs>? _updateAction;

    public override string ObserverId => "UIUpdateObserver";
    public override int Priority => 100; // Низький пріоритет - оновлюємо UI останніми
    public event EventHandler<StatusUpdateEventArgs>? StatusUpdated;
    public event EventHandler<ProgressUpdateEventArgs>? ProgressUpdated;
    public event EventHandler<PropertiesUpdateEventArgs>? PropertiesUpdated;
    public event EventHandler<ModificationStateEventArgs>? ModificationStateChanged;
    public event EventHandler<ValidationUpdateEventArgs>? ValidationUpdated;
    public UIUpdateObserver(Dispatcher? dispatcher = null, Action<UIUpdateEventArgs>? updateAction = null)
    {
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher;
        _updateAction = updateAction;
    }

    public override void OnSchemaChanged(SchemaChangeNotification notification)
    {
        InvokeOnUIThread(() =>
        {
            StatusUpdated?.Invoke(this, new StatusUpdateEventArgs
            {
                Message = GetStatusMessage(notification),
                IsModified = notification.IsModified,
                Timestamp = notification.Timestamp
            });
            ModificationStateChanged?.Invoke(this, new ModificationStateEventArgs
            {
                IsModified = notification.IsModified,
                ChangeType = notification.ChangeType,
                PropertyPath = notification.PropertyPath
            });
            if (notification.ChangeType == ChangeType.PropertyAdded ||
                notification.ChangeType == ChangeType.PropertyDeleted ||
                notification.ChangeType == ChangeType.PropertyUpdated)
            {
                PropertiesUpdated?.Invoke(this, new PropertiesUpdateEventArgs
                {
                    ChangeType = notification.ChangeType,
                    PropertyPath = notification.PropertyPath,
                    RequiresRefresh = true
                });
            }
            _updateAction?.Invoke(new UIUpdateEventArgs
            {
                UpdateType = UIUpdateType.SchemaChanged,
                Notification = notification
            });
        });
    }

    public override void OnSchemaSaved(SchemaSaveNotification notification)
    {
        InvokeOnUIThread(() =>
        {
            var message = notification.Success
                ? $"Saved: {System.IO.Path.GetFileName(notification.FilePath)}"
                : $"Save failed: {notification.ErrorMessage}";

            StatusUpdated?.Invoke(this, new StatusUpdateEventArgs
            {
                Message = message,
                IsModified = false,
                Timestamp = notification.Timestamp,
                IsError = !notification.Success
            });

            if (notification.Success)
            {
                ModificationStateChanged?.Invoke(this, new ModificationStateEventArgs
                {
                    IsModified = false
                });
            }

            _updateAction?.Invoke(new UIUpdateEventArgs
            {
                UpdateType = UIUpdateType.SchemaSaved,
                Notification = notification
            });
        });
    }

    public override void OnSchemaLoaded(SchemaLoadNotification notification)
    {
        InvokeOnUIThread(() =>
        {
            var message = notification.Success
                ? $"Loaded: {System.IO.Path.GetFileName(notification.FilePath)} ({notification.PropertiesCount} properties)"
                : $"Load failed: {notification.ErrorMessage}";

            StatusUpdated?.Invoke(this, new StatusUpdateEventArgs
            {
                Message = message,
                IsModified = false,
                Timestamp = notification.Timestamp,
                IsError = !notification.Success
            });

            if (notification.Success)
            {
                PropertiesUpdated?.Invoke(this, new PropertiesUpdateEventArgs
                {
                    RequiresRefresh = true,
                    PropertiesCount = notification.PropertiesCount
                });

                ModificationStateChanged?.Invoke(this, new ModificationStateEventArgs
                {
                    IsModified = false
                });
            }

            _updateAction?.Invoke(new UIUpdateEventArgs
            {
                UpdateType = UIUpdateType.SchemaLoaded,
                Notification = notification
            });
        });
    }

    public override void OnCommandExecuted(CommandNotification notification)
    {
        InvokeOnUIThread(() =>
        {
            var actionText = notification.ActionType switch
            {
                CommandActionType.Execute => "Executed",
                CommandActionType.Undo => "Undone",
                CommandActionType.Redo => "Redone",
                _ => "Command"
            };

            StatusUpdated?.Invoke(this, new StatusUpdateEventArgs
            {
                Message = $"{actionText}: {notification.Command.Description}",
                IsModified = true,
                Timestamp = notification.Timestamp
            });

            _updateAction?.Invoke(new UIUpdateEventArgs
            {
                UpdateType = UIUpdateType.CommandExecuted,
                Notification = notification
            });
        });
    }
    public void UpdateStatus(string message, bool isError = false)
    {
        InvokeOnUIThread(() =>
        {
            StatusUpdated?.Invoke(this, new StatusUpdateEventArgs
            {
                Message = message,
                Timestamp = DateTime.Now,
                IsError = isError
            });
        });
    }
    public void UpdateProgress(int current, int total, string? message = null)
    {
        InvokeOnUIThread(() =>
        {
            ProgressUpdated?.Invoke(this, new ProgressUpdateEventArgs
            {
                Current = current,
                Total = total,
                Percentage = total > 0 ? (double)current / total * 100 : 0,
                Message = message,
                IsIndeterminate = total <= 0
            });
        });
    }
    public void UpdateValidation(bool isValid, int errorCount, int warningCount)
    {
        InvokeOnUIThread(() =>
        {
            ValidationUpdated?.Invoke(this, new ValidationUpdateEventArgs
            {
                IsValid = isValid,
                ErrorCount = errorCount,
                WarningCount = warningCount
            });
        });
    }

    private void InvokeOnUIThread(Action action)
    {
        if (_dispatcher == null || _dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
        }
    }

    private static string GetStatusMessage(SchemaChangeNotification notification)
    {
        return notification.ChangeType switch
        {
            ChangeType.PropertyAdded => $"Property added: {notification.PropertyPath}",
            ChangeType.PropertyUpdated => $"Property updated: {notification.PropertyPath}",
            ChangeType.PropertyDeleted => $"Property deleted: {notification.PropertyPath}",
            ChangeType.MetadataUpdated => $"Metadata updated: {notification.PropertyPath}",
            ChangeType.SchemaReloaded => "Schema reloaded",
            ChangeType.ContentChanged => "Schema modified",
            _ => "Schema changed"
        };
    }
}

#region Event Args
public class UIUpdateEventArgs : EventArgs
{
    public UIUpdateType UpdateType { get; set; }
    public object? Notification { get; set; }
}
public enum UIUpdateType
{
    SchemaChanged,
    SchemaSaved,
    SchemaLoaded,
    CommandExecuted,
    ValidationCompleted
}
public class StatusUpdateEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public bool IsModified { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsError { get; set; }
}
public class ProgressUpdateEventArgs : EventArgs
{
    public int Current { get; set; }
    public int Total { get; set; }
    public double Percentage { get; set; }
    public string? Message { get; set; }
    public bool IsIndeterminate { get; set; }
}
public class PropertiesUpdateEventArgs : EventArgs
{
    public ChangeType? ChangeType { get; set; }
    public string? PropertyPath { get; set; }
    public bool RequiresRefresh { get; set; }
    public int PropertiesCount { get; set; }
}
public class ModificationStateEventArgs : EventArgs
{
    public bool IsModified { get; set; }
    public ChangeType? ChangeType { get; set; }
    public string? PropertyPath { get; set; }
}
public class ValidationUpdateEventArgs : EventArgs
{
    public bool IsValid { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

#endregion
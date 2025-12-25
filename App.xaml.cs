using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using JsonTool.Core.Command;
using JsonTool.Core.Flyweight;
using JsonTool.Core.Observer;
using JsonTool.Core.Strategy.Validation;
using JsonTool.Services;
using JsonTool.ViewModels;
using JsonTool.Views;

namespace JsonTool;
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private Serilog.ILogger? _logger;
    public static IServiceProvider Services => ((App)Current)._serviceProvider!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            ConfigureLogging();
            _logger?.Information("Application starting...");
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            _logger?.Information("Services configured successfully");
            await InitializeServicesAsync();
            await CheckAutoSaveRecoveryAsync();
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            _logger?.Information("Application started successfully");
        }
        catch (Exception ex)
        {
            _logger?.Fatal(ex, "Application failed to start");
            MessageBox.Show(
                $"Failed to start application: {ex.Message}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("Application shutting down...");

        try
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Log.CloseAndFlush();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error during shutdown");
        }

        base.OnExit(e);
    }
    private void ConfigureLogging()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JsonTool", "Logs");

        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "jsontool-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _logger = Log.Logger;
    }
    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });
        services.AddSingleton<AutoSaveSettings>(sp =>
        {
            var settings = AutoSaveSettings.LoadAsync().GetAwaiter().GetResult();
            return settings;
        });
        services.AddSingleton<SchemaCommandManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SchemaCommandManager>>();
            var manager = new SchemaCommandManager(maxHistorySize: 50);
            logger.LogInformation("SchemaCommandManager initialized with history size: 50");
            return manager;
        });
        services.AddSingleton<SchemaChangeNotifier>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SchemaChangeNotifier>>();
            var notifier = new SchemaChangeNotifier();
            logger.LogInformation("SchemaChangeNotifier initialized");
            return notifier;
        });
        services.AddSingleton<SchemaPropertyFlyweightFactory>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SchemaPropertyFlyweightFactory>>();
            var factory = new SchemaPropertyFlyweightFactory();
            logger.LogInformation("SchemaPropertyFlyweightFactory initialized");
            return factory;
        });
        services.AddSingleton<IJsonSchemaService, JsonSchemaService>();
        services.AddSingleton<DocumentSubject>();
        services.AddSingleton<CommandInvoker>();
        services.AddSingleton<TextFormatFactory>();
        services.AddSingleton<ValidationContext>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ValidationContext>>();
            var context = new ValidationContext(new SyntaxValidationStrategy());
            logger.LogInformation("ValidationContext initialized with SyntaxValidationStrategy");
            return context;
        });
        services.AddSingleton<AutoSaveService>(sp =>
        {
            var settings = sp.GetRequiredService<AutoSaveSettings>();
            var logger = sp.GetRequiredService<ILogger<AutoSaveService>>();
            var service = new AutoSaveService(settings);
            
            service.AfterAutoSave += (s, e) =>
                logger.LogInformation("Auto-saved to: {Path}", e.AutoSaveFilePath);
            
            service.AutoSaveError += (s, e) =>
                logger.LogError(e.Exception, "Auto-save failed: {Message}", e.Message);
            
            logger.LogInformation("AutoSaveService initialized with debounce: {Delay}ms", 
                settings.DebounceDelayMs);
            return service;
        });
        services.AddSingleton<AutoSaveObserver>(sp =>
        {
            var autoSaveService = sp.GetRequiredService<AutoSaveService>();
            return new AutoSaveObserver(
                saveAction: () => autoSaveService.SaveNowAsync(),
                saveDelay: TimeSpan.FromMilliseconds(
                    sp.GetRequiredService<AutoSaveSettings>().DebounceDelayMs)
            );
        });

        services.AddSingleton<HistoryObserver>(sp =>
        {
            return new HistoryObserver(maxHistorySize: 100);
        });

        services.AddSingleton<UIUpdateObserver>();
        services.AddTransient<SchemaFlyweightParser>(sp =>
        {
            var factory = sp.GetRequiredService<SchemaPropertyFlyweightFactory>();
            return new SchemaFlyweightParser(factory);
        });
        services.AddTransient<MainViewModel>(sp =>
        {
            var schemaService = sp.GetRequiredService<IJsonSchemaService>();
            var documentSubject = sp.GetRequiredService<DocumentSubject>();
            var commandInvoker = sp.GetRequiredService<CommandInvoker>();
            var formatFactory = sp.GetRequiredService<TextFormatFactory>();

            return new MainViewModel(
                schemaService,
                documentSubject,
                commandInvoker,
                formatFactory);
        });
        services.AddTransient<MainWindow>(sp =>
        {
            var viewModel = sp.GetRequiredService<MainViewModel>();
            var window = new MainWindow
            {
                DataContext = viewModel
            };
            return window;
        });

        services.AddTransient<FlatViewWindow>();
    }
    private async Task InitializeServicesAsync()
    {
        _logger?.Information("Initializing services...");
        var notifier = _serviceProvider!.GetRequiredService<SchemaChangeNotifier>();
        var commandManager = _serviceProvider.GetRequiredService<SchemaCommandManager>();
        var autoSaveObserver = _serviceProvider.GetRequiredService<AutoSaveObserver>();
        var historyObserver = _serviceProvider.GetRequiredService<HistoryObserver>();
        var uiUpdateObserver = _serviceProvider.GetRequiredService<UIUpdateObserver>();
        notifier.Attach(autoSaveObserver);
        notifier.Attach(historyObserver);
        notifier.Attach(uiUpdateObserver);
        notifier.IntegrateWithCommandManager(commandManager);

        _logger?.Information("Observers attached to SchemaChangeNotifier");
        Highlighting.JsonHighlightingLoader.RegisterJsonHighlighting();
        _logger?.Information("JSON syntax highlighting registered");

        await Task.CompletedTask;
    }
    private async Task CheckAutoSaveRecoveryAsync()
    {
        var autoSaveService = _serviceProvider!.GetRequiredService<AutoSaveService>();
        var allAutoSaves = await autoSaveService.GetAllAutoSavesAsync();

        if (allAutoSaves.Count > 0)
        {
            _logger?.Information("Found {Count} auto-save files for potential recovery", allAutoSaves.Count);

            var settings = _serviceProvider.GetRequiredService<AutoSaveSettings>();
            
            if (!settings.AutoRecoverWithoutPrompt)
            {
                var result = MessageBox.Show(
                    $"Found {allAutoSaves.Count} unsaved file(s) from previous session.\n\n" +
                    $"Most recent: {allAutoSaves[0].DisplayName} ({allAutoSaves[0].TimeSinceModified})\n\n" +
                    "Would you like to recover them?",
                    "Recovery Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    _logger?.Information("User declined recovery, clearing auto-saves");
                    await autoSaveService.ClearAllAutoSavesAsync();
                }
                else
                {
                    _logger?.Information("User accepted recovery");
                }
            }
        }
    }
    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            _logger?.Fatal(ex, "Unhandled domain exception");
        };

        DispatcherUnhandledException += (s, e) =>
        {
            _logger?.Error(e.Exception, "Unhandled dispatcher exception");
            
            MessageBox.Show(
                $"An error occurred: {e.Exception.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            _logger?.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }
}
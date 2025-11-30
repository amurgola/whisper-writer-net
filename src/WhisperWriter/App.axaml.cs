using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WhisperWriter.Application.Services;
using WhisperWriter.Core.Interfaces;
using WhisperWriter.UI.ViewModels;
using WhisperWriter.UI.Views;

namespace WhisperWriter;

public class App : Avalonia.Application
{
    private WhisperWriterService? _whisperService;
    private StatusWindow? _statusWindow;
    private MainWindow? _mainWindow;
    private IConfigurationService? _configService;
    private IAudioRecorderService? _audioRecorder;
    private IWhisperModelManager? _modelManager;
    private ICudaDetectionService? _cudaDetectionService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Program.ServiceProvider != null)
        {
            // Get services
            _whisperService = Program.ServiceProvider.GetRequiredService<WhisperWriterService>();
            _configService = Program.ServiceProvider.GetRequiredService<IConfigurationService>();
            _audioRecorder = Program.ServiceProvider.GetRequiredService<IAudioRecorderService>();
            _modelManager = Program.ServiceProvider.GetRequiredService<IWhisperModelManager>();
            _cudaDetectionService = Program.ServiceProvider.GetRequiredService<ICudaDetectionService>();

            // Initialize the service
            await _whisperService.InitializeAsync();

            // Create view models
            var mainViewModel = new MainViewModel(_whisperService);
            var statusViewModel = new StatusViewModel(_whisperService, _modelManager);

            // Create main window
            _mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // Set up settings handler
            mainViewModel.SettingsRequested += (_, _) => OpenSettingsWindow();

            // Set up tray icon
            var trayViewModel = new TrayIconViewModel(
                showWindowAction: ShowMainWindow,
                openSettingsAction: OpenSettingsWindow,
                exitAction: () => desktop.Shutdown()
            );
            DataContext = trayViewModel;

            // Create status window if not hidden
            if (!_configService.Configuration.Misc.HideStatusWindow)
            {
                _statusWindow = new StatusWindow(statusViewModel, _configService);
                _statusWindow.Show();
            }

            desktop.MainWindow = _mainWindow;

            // Handle shutdown
            desktop.ShutdownRequested += OnShutdownRequested;

            // Minimize to tray instead of closing
            _mainWindow.Closing += (_, e) =>
            {
                e.Cancel = true;
                _mainWindow.Hide();
            };

            // Check if API key is missing and auto-open settings
            bool needsConfiguration = NeedsConfiguration();

            if (needsConfiguration)
            {
                // Show the window and open settings immediately
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;

                // Use Dispatcher to open settings after window is shown
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    OpenSettingsWindow();
                });
            }
            else if (_configService.Configuration.Misc.StartMinimized)
            {
                // Start minimized to tray
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
            }

            // Start the service
            _whisperService.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private bool NeedsConfiguration()
    {
        if (_configService == null) return false;

        var config = _configService.Configuration;

        // If using API mode and no API key is set
        if (config.Model.UseApi && string.IsNullOrWhiteSpace(config.Model.Api.ApiKey))
        {
            return true;
        }

        // If using local model, check if the model is downloaded
        if (!config.Model.UseApi && _modelManager != null)
        {
            if (!_modelManager.IsModelDownloaded(config.Model.Local.Model))
            {
                return true;
            }
        }

        return false;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OpenSettingsWindow()
    {
        if (_mainWindow == null || _configService == null || _audioRecorder == null) return;

        var settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel(_configService, _audioRecorder, _modelManager, _cudaDetectionService)
        };
        settingsWindow.ShowDialog(_mainWindow);
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _whisperService?.Stop();
        _statusWindow?.Close();
    }
}

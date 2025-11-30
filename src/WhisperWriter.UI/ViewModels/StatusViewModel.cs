using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WhisperWriter.Application.Services;
using WhisperWriter.Core.Enums;
using WhisperWriter.Core.Interfaces;

namespace WhisperWriter.UI.ViewModels;

/// <summary>
/// View model for the status window overlay.
/// </summary>
public partial class StatusViewModel : ViewModelBase
{
    private readonly WhisperWriterService _whisperService;
    private readonly IWhisperModelManager? _modelManager;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private RecordingState _currentState = RecordingState.Idle;

    [ObservableProperty]
    private IBrush _statusColor = Brushes.Gray;

    public StatusViewModel(WhisperWriterService whisperService, IWhisperModelManager? modelManager = null)
    {
        _whisperService = whisperService;
        _modelManager = modelManager;

        _whisperService.StateChanged += OnStateChanged;

        if (_modelManager != null)
        {
            _modelManager.ModelLoadingStateChanged += OnModelLoadingStateChanged;
        }
    }

    private void OnModelLoadingStateChanged(object? sender, ModelLoadingStateEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (e.IsLoading)
            {
                StatusText = e.Status;
                StatusColor = Brushes.Purple;
                IsVisible = true;
            }
            // When loading completes, let the normal state handler take over
        });
    }

    private void OnStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        CurrentState = e.NewState;

        StatusText = e.NewState switch
        {
            RecordingState.Idle => "Ready",
            RecordingState.Recording => "Recording...",
            RecordingState.LoadingModel => "Loading model...",
            RecordingState.Transcribing => "Transcribing...",
            RecordingState.Typing => "Typing...",
            _ => "Ready"
        };

        StatusColor = e.NewState switch
        {
            RecordingState.Idle => Brushes.Gray,
            RecordingState.Recording => Brushes.Red,
            RecordingState.LoadingModel => Brushes.Purple,
            RecordingState.Transcribing => Brushes.Orange,
            RecordingState.Typing => Brushes.Green,
            _ => Brushes.Gray
        };

        // Auto-hide/show based on state
        IsVisible = e.NewState != RecordingState.Idle;
    }
}

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WhisperWriter.Application.Services;
using WhisperWriter.Core.Enums;

namespace WhisperWriter.UI.ViewModels;

/// <summary>
/// View model for the status window overlay.
/// </summary>
public partial class StatusViewModel : ViewModelBase
{
    private readonly WhisperWriterService _whisperService;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private RecordingState _currentState = RecordingState.Idle;

    [ObservableProperty]
    private IBrush _statusColor = Brushes.Gray;

    public StatusViewModel(WhisperWriterService whisperService)
    {
        _whisperService = whisperService;
        _whisperService.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        CurrentState = e.NewState;

        StatusText = e.NewState switch
        {
            RecordingState.Idle => "Ready",
            RecordingState.Recording => "Recording...",
            RecordingState.Transcribing => "Transcribing...",
            RecordingState.Typing => "Typing...",
            _ => "Ready"
        };

        StatusColor = e.NewState switch
        {
            RecordingState.Idle => Brushes.Gray,
            RecordingState.Recording => Brushes.Red,
            RecordingState.Transcribing => Brushes.Orange,
            RecordingState.Typing => Brushes.Green,
            _ => Brushes.Gray
        };

        // Auto-hide/show based on state
        IsVisible = e.NewState != RecordingState.Idle;
    }
}

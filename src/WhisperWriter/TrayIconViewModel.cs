using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace WhisperWriter;

/// <summary>
/// View model for the system tray icon commands.
/// </summary>
public class TrayIconViewModel
{
    public ICommand ShowWindowCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }

    public TrayIconViewModel(
        Action showWindowAction,
        Action openSettingsAction,
        Action exitAction)
    {
        ShowWindowCommand = new RelayCommand(showWindowAction);
        OpenSettingsCommand = new RelayCommand(openSettingsAction);
        ExitCommand = new RelayCommand(exitAction);
    }
}

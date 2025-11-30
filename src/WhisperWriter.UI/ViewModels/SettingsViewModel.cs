using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WhisperWriter.Core.Enums;
using WhisperWriter.Core.Interfaces;
using WhisperWriter.Core.Models;

namespace WhisperWriter.UI.ViewModels;

/// <summary>
/// View model for the settings window.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private readonly IAudioRecorderService _audioRecorder;
    private readonly IWhisperModelManager? _modelManager;
    private readonly ICudaDetectionService? _cudaDetectionService;

    // Model Options
    [ObservableProperty]
    private bool _useApi = true;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _apiBaseUrl = "https://api.openai.com/v1";

    [ObservableProperty]
    private string _apiModel = "whisper-1";

    [ObservableProperty]
    private string _language = string.Empty;

    [ObservableProperty]
    private double _temperature = 0.0;

    [ObservableProperty]
    private string _initialPrompt = string.Empty;

    [ObservableProperty]
    private string _localModel = "base";

    [ObservableProperty]
    private string _localDevice = "auto";

    [ObservableProperty]
    private string _computeType = "default";

    [ObservableProperty]
    private bool _keepModelLoaded = true;

    // Model download state
    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    [ObservableProperty]
    private bool _isSelectedModelDownloaded;

    [ObservableProperty]
    private string _selectedModelMemoryInfo = string.Empty;

    // CUDA status
    [ObservableProperty]
    private bool _isCudaAvailable;

    [ObservableProperty]
    private string _cudaStatusMessage = string.Empty;

    [ObservableProperty]
    private string _cudaDownloadUrl = "https://developer.nvidia.com/cuda-downloads";

    [ObservableProperty]
    private bool _showCudaSection;

    [ObservableProperty]
    private int _selectedGpuIndex = -1;

    [ObservableProperty]
    private GpuDeviceInfo? _selectedGpuDevice;

    // Recording Options
    [ObservableProperty]
    private string _activationKey = "ctrl+shift+space";

    [ObservableProperty]
    private RecordingMode _recordingMode = RecordingMode.VoiceActivityDetection;

    [ObservableProperty]
    private int _selectedDeviceIndex = -1;

    [ObservableProperty]
    private int _sampleRate = 16000;

    [ObservableProperty]
    private int _silenceDuration = 900;

    [ObservableProperty]
    private int _minDuration = 100;

    // Post-Processing Options
    [ObservableProperty]
    private double _writingKeyPressDelay = 0.005;

    [ObservableProperty]
    private bool _removeTrailingPeriod;

    [ObservableProperty]
    private bool _addTrailingSpace = true;

    [ObservableProperty]
    private bool _removeCapitalization;

    [ObservableProperty]
    private InputMethod _inputMethod = InputMethod.SharpHook;

    // Misc Options
    [ObservableProperty]
    private bool _printToTerminal = true;

    [ObservableProperty]
    private bool _hideStatusWindow;

    [ObservableProperty]
    private bool _noiseOnCompletion;

    [ObservableProperty]
    private bool _startMinimized;

    // Collections
    public ObservableCollection<AudioDevice> AudioDevices { get; } = new();
    public ObservableCollection<string> LocalModels { get; } = new() { "tiny", "base", "small", "medium", "large" };
    public ObservableCollection<string> Devices { get; } = new() { "auto", "cuda", "cpu" };
    public ObservableCollection<string> ComputeTypes { get; } = new() { "default", "float32", "float16", "int8" };
    public ObservableCollection<GpuDeviceInfo> GpuDevices { get; } = new();
    public ObservableCollection<RecordingMode> RecordingModes { get; } = new()
    {
        RecordingMode.Continuous,
        RecordingMode.VoiceActivityDetection,
        RecordingMode.PressToToggle,
        RecordingMode.HoldToRecord
    };
    public ObservableCollection<InputMethod> InputMethods { get; } = new()
    {
        InputMethod.SharpHook,
        InputMethod.Native,
        InputMethod.Ydotool,
        InputMethod.Dotool
    };

    public SettingsViewModel(
        IConfigurationService configService,
        IAudioRecorderService audioRecorder,
        IWhisperModelManager? modelManager = null,
        ICudaDetectionService? cudaDetectionService = null)
    {
        _configService = configService;
        _audioRecorder = audioRecorder;
        _modelManager = modelManager;
        _cudaDetectionService = cudaDetectionService;

        if (_modelManager != null)
        {
            _modelManager.DownloadProgressChanged += OnDownloadProgressChanged;
        }

        LoadAudioDevices();
        LoadFromConfiguration();
        UpdateModelInfo();
        UpdateCudaStatus();
    }

    private void OnDownloadProgressChanged(object? sender, ModelDownloadProgressEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            DownloadProgress = e.ProgressPercentage;
            DownloadStatus = e.Status;

            if (e.ProgressPercentage >= 100)
            {
                IsDownloading = false;
                UpdateModelInfo();
            }
        });
    }

    partial void OnLocalModelChanged(string value)
    {
        UpdateModelInfo();
    }

    partial void OnLocalDeviceChanged(string value)
    {
        // Show CUDA section when cuda is selected
        ShowCudaSection = value.Equals("cuda", StringComparison.OrdinalIgnoreCase);
        UpdateCudaStatus();
    }

    private void UpdateCudaStatus()
    {
        if (_cudaDetectionService == null)
        {
            IsCudaAvailable = false;
            CudaStatusMessage = "CUDA detection service not available";
            return;
        }

        var status = _cudaDetectionService.GetCudaStatus();
        IsCudaAvailable = status.IsAvailable;
        CudaStatusMessage = status.StatusMessage;

        // Update GPU devices list
        GpuDevices.Clear();
        if (status.IsAvailable && status.Devices.Count > 0)
        {
            // Add "Auto" option
            GpuDevices.Add(new GpuDeviceInfo { DeviceIndex = -1, Name = "Auto (Default)" });

            foreach (var device in status.Devices)
            {
                GpuDevices.Add(device);
            }

            // Select the correct device based on saved index
            SelectedGpuDevice = GpuDevices.FirstOrDefault(d => d.DeviceIndex == SelectedGpuIndex)
                                ?? GpuDevices.First();
        }
    }

    partial void OnSelectedGpuDeviceChanged(GpuDeviceInfo? value)
    {
        if (value != null)
        {
            SelectedGpuIndex = value.DeviceIndex;
        }
    }

    [RelayCommand]
    private void RefreshCudaStatus()
    {
        _cudaDetectionService?.Refresh();
        UpdateCudaStatus();
    }

    [RelayCommand]
    private void OpenCudaDownloadLink()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = CudaDownloadUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Ignore errors opening the URL
        }
    }

    private void UpdateModelInfo()
    {
        var modelInfo = WhisperModelInfo.GetById(LocalModel);
        if (modelInfo != null)
        {
            SelectedModelMemoryInfo = $"~{modelInfo.RequiredRamGb:F1} GB RAM required, {modelInfo.DownloadSizeMb} MB download, {modelInfo.RelativeSpeed} speed";
        }
        else
        {
            SelectedModelMemoryInfo = string.Empty;
        }

        IsSelectedModelDownloaded = _modelManager?.IsModelDownloaded(LocalModel) ?? false;
    }

    [RelayCommand]
    private async Task DownloadModelAsync()
    {
        if (_modelManager == null || IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatus = "Starting download...";

        try
        {
            await _modelManager.DownloadModelAsync(LocalModel);
            UpdateModelInfo();
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteModelAsync()
    {
        if (_modelManager == null) return;

        await _modelManager.DeleteModelAsync(LocalModel);
        UpdateModelInfo();
    }

    private void LoadAudioDevices()
    {
        AudioDevices.Clear();
        AudioDevices.Add(new AudioDevice { DeviceIndex = -1, Name = "Default Device", IsDefault = true });

        foreach (var device in _audioRecorder.GetAvailableDevices())
        {
            AudioDevices.Add(device);
        }
    }

    private void LoadFromConfiguration()
    {
        var config = _configService.Configuration;

        // Model
        UseApi = config.Model.UseApi;
        ApiKey = config.Model.Api.ApiKey ?? string.Empty;
        ApiBaseUrl = config.Model.Api.BaseUrl;
        ApiModel = config.Model.Api.Model;
        Language = config.Model.Common.Language ?? string.Empty;
        Temperature = config.Model.Common.Temperature;
        InitialPrompt = config.Model.Common.InitialPrompt ?? string.Empty;
        LocalModel = config.Model.Local.Model;
        LocalDevice = config.Model.Local.Device;
        ComputeType = config.Model.Local.ComputeType;
        KeepModelLoaded = config.Model.Local.KeepModelLoaded;
        SelectedGpuIndex = config.Model.Local.GpuDeviceIndex;

        // Recording
        ActivationKey = config.Recording.ActivationKey;
        RecordingMode = config.Recording.RecordingMode;
        SelectedDeviceIndex = config.Recording.SoundDevice;
        SampleRate = config.Recording.SampleRate;
        SilenceDuration = config.Recording.SilenceDuration;
        MinDuration = config.Recording.MinDuration;

        // Post-Processing
        WritingKeyPressDelay = config.PostProcessing.WritingKeyPressDelay;
        RemoveTrailingPeriod = config.PostProcessing.RemoveTrailingPeriod;
        AddTrailingSpace = config.PostProcessing.AddTrailingSpace;
        RemoveCapitalization = config.PostProcessing.RemoveCapitalization;
        InputMethod = config.PostProcessing.InputMethod;

        // Misc
        PrintToTerminal = config.Misc.PrintToTerminal;
        HideStatusWindow = config.Misc.HideStatusWindow;
        NoiseOnCompletion = config.Misc.NoiseOnCompletion;
        StartMinimized = config.Misc.StartMinimized;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var config = _configService.Configuration;

        // Model
        config.Model.UseApi = UseApi;
        config.Model.Api.ApiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey;
        config.Model.Api.BaseUrl = ApiBaseUrl;
        config.Model.Api.Model = ApiModel;
        config.Model.Common.Language = string.IsNullOrWhiteSpace(Language) ? null : Language;
        config.Model.Common.Temperature = Temperature;
        config.Model.Common.InitialPrompt = string.IsNullOrWhiteSpace(InitialPrompt) ? null : InitialPrompt;
        config.Model.Local.Model = LocalModel;
        config.Model.Local.Device = LocalDevice;
        config.Model.Local.ComputeType = ComputeType;
        config.Model.Local.KeepModelLoaded = KeepModelLoaded;
        config.Model.Local.GpuDeviceIndex = SelectedGpuIndex;

        // Recording
        config.Recording.ActivationKey = ActivationKey;
        config.Recording.RecordingMode = RecordingMode;
        config.Recording.SoundDevice = SelectedDeviceIndex;
        config.Recording.SampleRate = SampleRate;
        config.Recording.SilenceDuration = SilenceDuration;
        config.Recording.MinDuration = MinDuration;

        // Post-Processing
        config.PostProcessing.WritingKeyPressDelay = WritingKeyPressDelay;
        config.PostProcessing.RemoveTrailingPeriod = RemoveTrailingPeriod;
        config.PostProcessing.AddTrailingSpace = AddTrailingSpace;
        config.PostProcessing.RemoveCapitalization = RemoveCapitalization;
        config.PostProcessing.InputMethod = InputMethod;

        // Misc
        config.Misc.PrintToTerminal = PrintToTerminal;
        config.Misc.HideStatusWindow = HideStatusWindow;
        config.Misc.NoiseOnCompletion = NoiseOnCompletion;
        config.Misc.StartMinimized = StartMinimized;

        await _configService.SaveAsync();
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        _configService.ResetToDefaults();
        LoadFromConfiguration();
    }
}

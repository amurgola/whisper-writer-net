using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;
using WhisperWriter.Core.Interfaces;
using WhisperWriter.Core.Models;

namespace WhisperWriter.Infrastructure.Transcription;

/// <summary>
/// Manages Whisper model downloading, loading, and transcription.
/// </summary>
public sealed class WhisperModelManager : IWhisperModelManager
{
    private readonly ILogger<WhisperModelManager> _logger;
    private readonly IConfigurationService _configService;
    private readonly string _modelsDirectory;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private string? _loadedModelId;
    private bool _isDownloading;
    private bool _disposed;

    public event EventHandler<ModelDownloadProgressEventArgs>? DownloadProgressChanged;
    public event EventHandler<ModelLoadingStateEventArgs>? ModelLoadingStateChanged;

    public bool IsModelLoaded => _whisperProcessor != null;
    public string? LoadedModelId => _loadedModelId;
    public bool IsDownloading => _isDownloading;

    public WhisperModelManager(
        ILogger<WhisperModelManager> logger,
        IConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;

        // Store models in AppData/WhisperWriter/models
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _modelsDirectory = Path.Combine(appDataPath, "WhisperWriter", "models");
        Directory.CreateDirectory(_modelsDirectory);

        _logger.LogInformation("Whisper models directory: {Path}", _modelsDirectory);
    }

    public bool IsModelDownloaded(string modelId)
    {
        var modelPath = GetModelPath(modelId);
        return File.Exists(modelPath);
    }

    public string GetModelPath(string modelId)
    {
        return Path.Combine(_modelsDirectory, $"ggml-{modelId}.bin");
    }

    public async Task DownloadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (_isDownloading)
        {
            _logger.LogWarning("Download already in progress");
            return;
        }

        var modelInfo = WhisperModelInfo.GetById(modelId);
        if (modelInfo == null)
        {
            throw new ArgumentException($"Unknown model: {modelId}", nameof(modelId));
        }

        var modelPath = GetModelPath(modelId);
        if (File.Exists(modelPath))
        {
            _logger.LogInformation("Model {ModelId} already downloaded at {Path}", modelId, modelPath);
            return;
        }

        _isDownloading = true;
        try
        {
            _logger.LogInformation("Starting download of model {ModelId} ({Size} MB)", modelId, modelInfo.DownloadSizeMb);

            RaiseProgress(modelId, 0, 0, modelInfo.DownloadSizeMb * 1024L * 1024L, "Starting download...");

            var ggmlType = GetGgmlType(modelId);

            // Download using Whisper.net's built-in downloader
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);

            // Write to temp file first, then rename
            var tempPath = modelPath + ".tmp";
            try
            {
                await using var fileStream = File.Create(tempPath);

                var buffer = new byte[81920];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await modelStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytesRead += bytesRead;

                    var progress = modelInfo.DownloadSizeMb > 0
                        ? (double)totalBytesRead / (modelInfo.DownloadSizeMb * 1024L * 1024L) * 100
                        : 0;

                    RaiseProgress(modelId, Math.Min(progress, 99), totalBytesRead,
                        modelInfo.DownloadSizeMb * 1024L * 1024L, "Downloading...");
                }

                await fileStream.FlushAsync(cancellationToken);
            }
            catch
            {
                // Clean up temp file on error
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }

            // Rename temp file to final path
            File.Move(tempPath, modelPath, overwrite: true);

            RaiseProgress(modelId, 100, modelInfo.DownloadSizeMb * 1024L * 1024L,
                modelInfo.DownloadSizeMb * 1024L * 1024L, "Download complete");

            _logger.LogInformation("Model {ModelId} downloaded successfully to {Path}", modelId, modelPath);
        }
        finally
        {
            _isDownloading = false;
        }
    }

    public async Task LoadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            // If same model is already loaded, skip
            if (_loadedModelId == modelId && _whisperProcessor != null)
            {
                _logger.LogDebug("Model {ModelId} is already loaded", modelId);
                return;
            }

            // Unload any existing model
            UnloadModelInternal();

            var modelPath = GetModelPath(modelId);
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found. Please download the model first.", modelPath);
            }

            var config = _configService.Configuration.Model;
            var device = config.Local.Device.ToLowerInvariant();
            var gpuDeviceIndex = config.Local.GpuDeviceIndex;

            // Configure runtime library order based on device preference
            ConfigureRuntimeOrder(device);

            // Notify that loading is starting
            var loadingMessage = device == "cuda"
                ? gpuDeviceIndex >= 0
                    ? $"Loading {modelId} model (CUDA GPU {gpuDeviceIndex})..."
                    : $"Loading {modelId} model (CUDA)..."
                : device == "cpu"
                    ? $"Loading {modelId} model (CPU)..."
                    : $"Loading {modelId} model...";
            RaiseLoadingState(true, modelId, loadingMessage);

            _logger.LogInformation("Loading model {ModelId} from {Path} with device preference: {Device}, GPU index: {GpuIndex}",
                modelId, modelPath, device, gpuDeviceIndex);
            _logger.LogInformation("Runtime library order: {Order}",
                string.Join(", ", RuntimeOptions.RuntimeLibraryOrder));

            // Create factory options with GPU device selection if CUDA is enabled
            var factoryOptions = new WhisperFactoryOptions();
            if (device == "cuda" && gpuDeviceIndex >= 0)
            {
                factoryOptions.UseGpu = true;
                factoryOptions.GpuDevice = gpuDeviceIndex;
                _logger.LogInformation("Using GPU device index: {GpuIndex}", gpuDeviceIndex);
            }
            else if (device == "cuda")
            {
                factoryOptions.UseGpu = true;
            }

            // Create factory from model file
            _whisperFactory = WhisperFactory.FromPath(modelPath, factoryOptions);

            // Build processor with configuration
            var builder = _whisperFactory.CreateBuilder();

            // Set language if specified
            if (!string.IsNullOrEmpty(config.Common.Language))
            {
                builder.WithLanguage(config.Common.Language);
            }
            else
            {
                builder.WithLanguageDetection();
            }

            _whisperProcessor = builder.Build();
            _loadedModelId = modelId;

            _logger.LogInformation("Model {ModelId} loaded successfully", modelId);

            // Notify that loading is complete
            RaiseLoadingState(false, modelId, "Model loaded");
        }
        catch (Exception ex)
        {
            RaiseLoadingState(false, modelId, $"Failed to load model: {ex.Message}");
            throw;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void UnloadModel()
    {
        _loadLock.Wait();
        try
        {
            UnloadModelInternal();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private void UnloadModelInternal()
    {
        if (_whisperProcessor != null)
        {
            _logger.LogInformation("Unloading model {ModelId}", _loadedModelId);
            _whisperProcessor.Dispose();
            _whisperProcessor = null;
        }

        if (_whisperFactory != null)
        {
            _whisperFactory.Dispose();
            _whisperFactory = null;
        }

        _loadedModelId = null;
    }

    public async Task<TranscriptionResult> TranscribeAsync(AudioData audioData, CancellationToken cancellationToken = default)
    {
        if (_whisperProcessor == null)
        {
            return TranscriptionResult.Failed("No model loaded. Please load a model first.");
        }

        try
        {
            _logger.LogDebug("Starting local transcription of {Duration:F2}s audio", audioData.DurationSeconds);

            using var audioStream = audioData.GetStream();
            var segments = new List<string>();
            string? detectedLanguage = null;

            await foreach (var segment in _whisperProcessor.ProcessAsync(audioStream, cancellationToken))
            {
                segments.Add(segment.Text);
                detectedLanguage ??= segment.Language;
            }

            var fullText = string.Join(" ", segments).Trim();

            if (string.IsNullOrEmpty(fullText))
            {
                _logger.LogWarning("Empty transcription result");
                return TranscriptionResult.Failed("Empty transcription result");
            }

            _logger.LogInformation("Local transcription successful: {Length} characters", fullText.Length);

            return TranscriptionResult.Successful(fullText, detectedLanguage, audioData.DurationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during local transcription");
            return TranscriptionResult.Failed($"Transcription error: {ex.Message}");
        }
    }

    public Task DeleteModelAsync(string modelId)
    {
        var modelPath = GetModelPath(modelId);

        // Unload if this model is currently loaded
        if (_loadedModelId == modelId)
        {
            UnloadModel();
        }

        if (File.Exists(modelPath))
        {
            File.Delete(modelPath);
            _logger.LogInformation("Deleted model {ModelId} from {Path}", modelId, modelPath);
        }

        return Task.CompletedTask;
    }

    public IEnumerable<(WhisperModelInfo Info, bool IsDownloaded)> GetAvailableModels()
    {
        return WhisperModelInfo.AllModels.Select(m => (m, IsModelDownloaded(m.Id)));
    }

    private static GgmlType GetGgmlType(string modelId)
    {
        return modelId.ToLowerInvariant() switch
        {
            "tiny" => GgmlType.Tiny,
            "base" => GgmlType.Base,
            "small" => GgmlType.Small,
            "medium" => GgmlType.Medium,
            "large" => GgmlType.LargeV3,
            _ => throw new ArgumentException($"Unknown model: {modelId}", nameof(modelId))
        };
    }

    private void ConfigureRuntimeOrder(string device)
    {
        // Set the runtime library order based on device preference
        // This must be done BEFORE loading the model
        RuntimeOptions.RuntimeLibraryOrder.Clear();

        switch (device)
        {
            case "cuda":
                RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.Cuda);
                RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.Cpu);
                break;
            case "cpu":
                RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.Cpu);
                break;
            default: // "auto" - try GPU first, fall back to CPU
                RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.Cuda);
                RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.Vulkan);
                RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.CoreML);
                RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.OpenVino);
                RuntimeOptions.RuntimeLibraryOrder.Add(RuntimeLibrary.Cpu);
                break;
        }

        _logger.LogDebug("Configured runtime order for device '{Device}': {Runtimes}",
            device, string.Join(", ", RuntimeOptions.RuntimeLibraryOrder));
    }

    private void RaiseProgress(string modelId, double percentage, long bytesDownloaded, long totalBytes, string status)
    {
        DownloadProgressChanged?.Invoke(this, new ModelDownloadProgressEventArgs
        {
            ModelId = modelId,
            ProgressPercentage = percentage,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            Status = status
        });
    }

    private void RaiseLoadingState(bool isLoading, string modelId, string status)
    {
        ModelLoadingStateChanged?.Invoke(this, new ModelLoadingStateEventArgs
        {
            IsLoading = isLoading,
            ModelId = modelId,
            Status = status
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnloadModelInternal();
        _loadLock.Dispose();
    }
}

using WhisperWriter.Core.Models;

namespace WhisperWriter.Core.Interfaces;

/// <summary>
/// Event args for model download progress.
/// </summary>
public class ModelDownloadProgressEventArgs : EventArgs
{
    public string ModelId { get; init; } = string.Empty;
    public double ProgressPercentage { get; init; }
    public long BytesDownloaded { get; init; }
    public long TotalBytes { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Event args for model loading state changes.
/// </summary>
public class ModelLoadingStateEventArgs : EventArgs
{
    public bool IsLoading { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Manages Whisper model downloading, loading, and lifecycle.
/// </summary>
public interface IWhisperModelManager : IDisposable
{
    /// <summary>
    /// Event raised when download progress changes.
    /// </summary>
    event EventHandler<ModelDownloadProgressEventArgs>? DownloadProgressChanged;

    /// <summary>
    /// Event raised when model loading state changes.
    /// </summary>
    event EventHandler<ModelLoadingStateEventArgs>? ModelLoadingStateChanged;

    /// <summary>
    /// Gets whether a model is currently loaded in memory.
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Gets the ID of the currently loaded model, if any.
    /// </summary>
    string? LoadedModelId { get; }

    /// <summary>
    /// Gets whether a download is currently in progress.
    /// </summary>
    bool IsDownloading { get; }

    /// <summary>
    /// Checks if a model is downloaded and available locally.
    /// </summary>
    bool IsModelDownloaded(string modelId);

    /// <summary>
    /// Gets the local file path for a model.
    /// </summary>
    string GetModelPath(string modelId);

    /// <summary>
    /// Downloads a model if not already present.
    /// </summary>
    Task DownloadModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a model into memory for transcription.
    /// </summary>
    Task LoadModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads the current model from memory.
    /// </summary>
    void UnloadModel();

    /// <summary>
    /// Transcribes audio data using the loaded model.
    /// </summary>
    Task<TranscriptionResult> TranscribeAsync(AudioData audioData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a downloaded model to free disk space.
    /// </summary>
    Task DeleteModelAsync(string modelId);

    /// <summary>
    /// Gets information about all available models and their download status.
    /// </summary>
    IEnumerable<(WhisperModelInfo Info, bool IsDownloaded)> GetAvailableModels();
}

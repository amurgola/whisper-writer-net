using WhisperWriter.Core.Models;

namespace WhisperWriter.Core.Interfaces;

/// <summary>
/// Service for detecting CUDA availability and enumerating GPU devices.
/// </summary>
public interface ICudaDetectionService
{
    /// <summary>
    /// Gets the current CUDA status.
    /// </summary>
    CudaStatus GetCudaStatus();

    /// <summary>
    /// Gets the URL to download the CUDA toolkit.
    /// </summary>
    string CudaDownloadUrl { get; }

    /// <summary>
    /// Refreshes the CUDA status (re-detects devices).
    /// </summary>
    void Refresh();
}

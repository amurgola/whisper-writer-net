namespace WhisperWriter.Core.Models;

/// <summary>
/// Information about a GPU device.
/// </summary>
public sealed class GpuDeviceInfo
{
    /// <summary>
    /// Device index (0-based).
    /// </summary>
    public int DeviceIndex { get; init; }

    /// <summary>
    /// Device name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Total memory in bytes.
    /// </summary>
    public long TotalMemoryBytes { get; init; }

    /// <summary>
    /// CUDA compute capability (e.g., "8.6").
    /// </summary>
    public string? ComputeCapability { get; init; }

    /// <summary>
    /// Display string for UI.
    /// </summary>
    public string DisplayName => TotalMemoryBytes > 0
        ? $"{Name} ({TotalMemoryBytes / (1024 * 1024 * 1024.0):F1} GB)"
        : Name;
}

/// <summary>
/// CUDA availability status.
/// </summary>
public sealed class CudaStatus
{
    /// <summary>
    /// Whether CUDA runtime is available.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Whether the Whisper.net CUDA runtime library is installed.
    /// </summary>
    public bool IsRuntimeInstalled { get; init; }

    /// <summary>
    /// CUDA toolkit version if detected.
    /// </summary>
    public string? CudaVersion { get; init; }

    /// <summary>
    /// Error message if CUDA is not available.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of available GPU devices.
    /// </summary>
    public IReadOnlyList<GpuDeviceInfo> Devices { get; init; } = Array.Empty<GpuDeviceInfo>();

    /// <summary>
    /// Status message for display.
    /// </summary>
    public string StatusMessage
    {
        get
        {
            if (!IsRuntimeInstalled)
                return "CUDA runtime not installed";
            if (!IsAvailable)
                return ErrorMessage ?? "CUDA not available";
            if (Devices.Count == 0)
                return "No CUDA-capable GPU found";
            return $"CUDA available ({Devices.Count} GPU{(Devices.Count > 1 ? "s" : "")})";
        }
    }
}

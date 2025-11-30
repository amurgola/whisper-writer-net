namespace WhisperWriter.Core.Models;

/// <summary>
/// Information about a Whisper model including size and memory requirements.
/// </summary>
public sealed class WhisperModelInfo
{
    /// <summary>
    /// Model identifier (tiny, base, small, medium, large).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for the model.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Number of parameters in millions.
    /// </summary>
    public required int ParametersMillion { get; init; }

    /// <summary>
    /// Approximate RAM required in GB.
    /// </summary>
    public required double RequiredRamGb { get; init; }

    /// <summary>
    /// Approximate VRAM required for GPU acceleration in GB.
    /// </summary>
    public required double RequiredVramGb { get; init; }

    /// <summary>
    /// Relative speed compared to large model (higher is faster).
    /// </summary>
    public required string RelativeSpeed { get; init; }

    /// <summary>
    /// Approximate download size in MB.
    /// </summary>
    public required int DownloadSizeMb { get; init; }

    /// <summary>
    /// Gets the formatted display string with memory requirements.
    /// </summary>
    public string DisplayWithMemory => $"{DisplayName} (~{RequiredRamGb:F1} GB RAM, {DownloadSizeMb} MB download)";

    /// <summary>
    /// All available Whisper models with their specifications.
    /// </summary>
    public static IReadOnlyList<WhisperModelInfo> AllModels { get; } = new List<WhisperModelInfo>
    {
        new()
        {
            Id = "tiny",
            DisplayName = "Tiny",
            ParametersMillion = 39,
            RequiredRamGb = 1.0,
            RequiredVramGb = 1.0,
            RelativeSpeed = "~32x",
            DownloadSizeMb = 75
        },
        new()
        {
            Id = "base",
            DisplayName = "Base",
            ParametersMillion = 74,
            RequiredRamGb = 1.0,
            RequiredVramGb = 1.0,
            RelativeSpeed = "~16x",
            DownloadSizeMb = 142
        },
        new()
        {
            Id = "small",
            DisplayName = "Small",
            ParametersMillion = 244,
            RequiredRamGb = 2.0,
            RequiredVramGb = 2.0,
            RelativeSpeed = "~6x",
            DownloadSizeMb = 466
        },
        new()
        {
            Id = "medium",
            DisplayName = "Medium",
            ParametersMillion = 769,
            RequiredRamGb = 5.0,
            RequiredVramGb = 5.0,
            RelativeSpeed = "~2x",
            DownloadSizeMb = 1500
        },
        new()
        {
            Id = "large",
            DisplayName = "Large",
            ParametersMillion = 1550,
            RequiredRamGb = 10.0,
            RequiredVramGb = 10.0,
            RelativeSpeed = "1x",
            DownloadSizeMb = 2900
        }
    };

    /// <summary>
    /// Gets model info by ID.
    /// </summary>
    public static WhisperModelInfo? GetById(string id) =>
        AllModels.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

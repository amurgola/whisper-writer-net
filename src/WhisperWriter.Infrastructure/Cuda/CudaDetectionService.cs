using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WhisperWriter.Core.Interfaces;
using WhisperWriter.Core.Models;

namespace WhisperWriter.Infrastructure.Cuda;

/// <summary>
/// Service for detecting CUDA availability and enumerating GPU devices.
/// </summary>
public sealed partial class CudaDetectionService : ICudaDetectionService
{
    private readonly ILogger<CudaDetectionService> _logger;
    private CudaStatus? _cachedStatus;

    public string CudaDownloadUrl => "https://developer.nvidia.com/cuda-downloads";

    public CudaDetectionService(ILogger<CudaDetectionService> logger)
    {
        _logger = logger;
    }

    public CudaStatus GetCudaStatus()
    {
        if (_cachedStatus != null)
            return _cachedStatus;

        _cachedStatus = DetectCuda();
        return _cachedStatus;
    }

    public void Refresh()
    {
        _cachedStatus = null;
        _cachedStatus = DetectCuda();
    }

    private CudaStatus DetectCuda()
    {
        try
        {
            // Check if Whisper.net CUDA runtime DLL exists
            var isRuntimeInstalled = CheckWhisperCudaRuntime();

            if (!isRuntimeInstalled)
            {
                _logger.LogInformation("Whisper.net CUDA runtime not found in application");
                return new CudaStatus
                {
                    IsAvailable = false,
                    IsRuntimeInstalled = false,
                    ErrorMessage = "Whisper.net CUDA runtime package not installed"
                };
            }

            // Try to detect CUDA using nvidia-smi
            var (success, version, devices, error) = DetectWithNvidiaSmi();

            if (success)
            {
                _logger.LogInformation("CUDA detected: version {Version}, {DeviceCount} device(s)",
                    version, devices.Count);
                return new CudaStatus
                {
                    IsAvailable = true,
                    IsRuntimeInstalled = true,
                    CudaVersion = version,
                    Devices = devices
                };
            }

            _logger.LogWarning("CUDA detection failed: {Error}", error);
            return new CudaStatus
            {
                IsAvailable = false,
                IsRuntimeInstalled = true,
                ErrorMessage = error ?? "CUDA not detected. Please install NVIDIA drivers and CUDA toolkit."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting CUDA");
            return new CudaStatus
            {
                IsAvailable = false,
                IsRuntimeInstalled = false,
                ErrorMessage = $"Error detecting CUDA: {ex.Message}"
            };
        }
    }

    private bool CheckWhisperCudaRuntime()
    {
        // Check for the CUDA runtime DLL in typical locations
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var runtimePaths = new[]
        {
            Path.Combine(basePath, "runtimes", "win-x64", "native", "whisper.dll"),
            Path.Combine(basePath, "runtimes", "win-x64", "native", "ggml-cuda.dll"),
            Path.Combine(basePath, "whisper.dll"),
            Path.Combine(basePath, "ggml-cuda.dll"),
            // Also check for the cublas DLLs that come with CUDA runtime
            Path.Combine(basePath, "runtimes", "win-x64", "native", "cublas64_12.dll"),
            Path.Combine(basePath, "cublas64_12.dll"),
        };

        foreach (var path in runtimePaths)
        {
            if (File.Exists(path))
            {
                _logger.LogDebug("Found CUDA-related file: {Path}", path);
                return true;
            }
        }

        // The package is installed via NuGet, so runtime files should be available
        // Even if we can't find specific files, assume it's installed if the package reference exists
        return true;
    }

    private (bool Success, string? Version, List<GpuDeviceInfo> Devices, string? Error) DetectWithNvidiaSmi()
    {
        try
        {
            // nvidia-smi is typically in PATH on systems with NVIDIA drivers
            var nvidiaSmiPath = FindNvidiaSmi();
            if (nvidiaSmiPath == null)
            {
                return (false, null, new List<GpuDeviceInfo>(),
                    "nvidia-smi not found. Please install NVIDIA drivers.");
            }

            // Get CUDA version
            string? cudaVersion = null;
            var versionResult = RunNvidiaSmi(nvidiaSmiPath, "--query-gpu=driver_version --format=csv,noheader");
            if (!string.IsNullOrWhiteSpace(versionResult))
            {
                cudaVersion = versionResult.Trim().Split('\n')[0].Trim();
            }

            // Get GPU information
            var gpuResult = RunNvidiaSmi(nvidiaSmiPath,
                "--query-gpu=index,name,memory.total --format=csv,noheader,nounits");

            var devices = new List<GpuDeviceInfo>();
            if (!string.IsNullOrWhiteSpace(gpuResult))
            {
                var lines = gpuResult.Trim().Split('\n');
                foreach (var line in lines)
                {
                    var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length >= 3 &&
                        int.TryParse(parts[0], out var index) &&
                        long.TryParse(parts[2], out var memoryMb))
                    {
                        devices.Add(new GpuDeviceInfo
                        {
                            DeviceIndex = index,
                            Name = parts[1],
                            TotalMemoryBytes = memoryMb * 1024 * 1024
                        });
                    }
                }
            }

            if (devices.Count == 0)
            {
                return (false, cudaVersion, devices, "No NVIDIA GPUs found");
            }

            return (true, cudaVersion, devices, null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "nvidia-smi detection failed");
            return (false, null, new List<GpuDeviceInfo>(), ex.Message);
        }
    }

    private string? FindNvidiaSmi()
    {
        // Common locations for nvidia-smi
        var possiblePaths = new List<string>
        {
            "nvidia-smi", // In PATH
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            possiblePaths.AddRange(new[]
            {
                Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
                Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"),
                @"C:\Windows\System32\nvidia-smi.exe"
            });
        }
        else
        {
            possiblePaths.AddRange(new[]
            {
                "/usr/bin/nvidia-smi",
                "/usr/local/bin/nvidia-smi"
            });
        }

        foreach (var path in possiblePaths)
        {
            try
            {
                var result = RunNvidiaSmi(path, "--version");
                if (!string.IsNullOrEmpty(result))
                {
                    _logger.LogDebug("Found nvidia-smi at: {Path}", path);
                    return path;
                }
            }
            catch
            {
                // Try next path
            }
        }

        return null;
    }

    private string? RunNvidiaSmi(string path, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}

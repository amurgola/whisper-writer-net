using Microsoft.Extensions.Logging;
using WhisperWriter.Core.Interfaces;
using WhisperWriter.Core.Models;

namespace WhisperWriter.Infrastructure.Transcription;

/// <summary>
/// Local Whisper transcription service using Whisper.net.
/// </summary>
public sealed class LocalWhisperTranscriptionService : ITranscriptionService
{
    private readonly ILogger<LocalWhisperTranscriptionService> _logger;
    private readonly IWhisperModelManager _modelManager;
    private readonly IConfigurationService _configService;

    public LocalWhisperTranscriptionService(
        ILogger<LocalWhisperTranscriptionService> logger,
        IWhisperModelManager modelManager,
        IConfigurationService configService)
    {
        _logger = logger;
        _modelManager = modelManager;
        _configService = configService;
    }

    public async Task<TranscriptionResult> TranscribeAsync(AudioData audioData, CancellationToken cancellationToken = default)
    {
        var config = _configService.Configuration.Model.Local;
        var modelId = config.Model;

        try
        {
            // Ensure model is downloaded
            if (!_modelManager.IsModelDownloaded(modelId))
            {
                _logger.LogWarning("Model {ModelId} is not downloaded", modelId);
                return TranscriptionResult.Failed($"Model '{modelId}' is not downloaded. Please download it in Settings.");
            }

            // Load model if not already loaded or if different model requested
            if (!_modelManager.IsModelLoaded || _modelManager.LoadedModelId != modelId)
            {
                _logger.LogInformation("Loading model {ModelId}", modelId);
                await _modelManager.LoadModelAsync(modelId, cancellationToken);
            }

            // Transcribe
            return await _modelManager.TranscribeAsync(audioData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during local transcription");
            return TranscriptionResult.Failed($"Local transcription error: {ex.Message}");
        }
        finally
        {
            // Unload model if not keeping warm
            if (!config.KeepModelLoaded && _modelManager.IsModelLoaded)
            {
                _logger.LogDebug("Unloading model (KeepModelLoaded is disabled)");
                _modelManager.UnloadModel();
            }
        }
    }
}

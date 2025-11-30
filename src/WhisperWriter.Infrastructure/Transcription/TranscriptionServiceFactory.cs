using Microsoft.Extensions.Logging;
using WhisperWriter.Core.Interfaces;
using WhisperWriter.Core.Models;

namespace WhisperWriter.Infrastructure.Transcription;

/// <summary>
/// Factory that selects the appropriate transcription service based on current configuration.
/// This allows runtime switching between API and local transcription.
/// </summary>
public sealed class TranscriptionServiceFactory : ITranscriptionService
{
    private readonly ILogger<TranscriptionServiceFactory> _logger;
    private readonly IConfigurationService _configService;
    private readonly OpenAiTranscriptionService _apiService;
    private readonly LocalWhisperTranscriptionService _localService;

    public TranscriptionServiceFactory(
        ILogger<TranscriptionServiceFactory> logger,
        IConfigurationService configService,
        OpenAiTranscriptionService apiService,
        LocalWhisperTranscriptionService localService)
    {
        _logger = logger;
        _configService = configService;
        _apiService = apiService;
        _localService = localService;
    }

    public async Task<TranscriptionResult> TranscribeAsync(AudioData audioData, CancellationToken cancellationToken = default)
    {
        var useApi = _configService.Configuration.Model.UseApi;

        if (useApi)
        {
            _logger.LogDebug("Using OpenAI API for transcription");
            return await _apiService.TranscribeAsync(audioData, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Using local Whisper model for transcription");
            return await _localService.TranscribeAsync(audioData, cancellationToken);
        }
    }
}

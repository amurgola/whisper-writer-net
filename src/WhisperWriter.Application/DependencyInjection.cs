using Microsoft.Extensions.DependencyInjection;
using WhisperWriter.Application.Services;
using WhisperWriter.Core.Interfaces;
using WhisperWriter.Infrastructure;
using WhisperWriter.Infrastructure.Audio;
using WhisperWriter.Infrastructure.Configuration;
using WhisperWriter.Infrastructure.Input;
using WhisperWriter.Infrastructure.Keyboard;
using WhisperWriter.Infrastructure.Transcription;

namespace WhisperWriter.Application;

/// <summary>
/// Extension methods for registering application services with dependency injection.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all WhisperWriter services with the DI container.
    /// </summary>
    public static IServiceCollection AddWhisperWriter(this IServiceCollection services, string? configPath = null)
    {
        // Core services (Singleton for shared state)
        services.AddSingleton<IConfigurationService>(sp =>
            new YamlConfigurationService(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<YamlConfigurationService>>(),
                configPath));

        // Model manager for local Whisper models
        services.AddSingleton<IWhisperModelManager, WhisperModelManager>();

        // Transcription services - both are registered as concrete types
        services.AddSingleton<OpenAiTranscriptionService>();
        services.AddSingleton<LocalWhisperTranscriptionService>();

        // Transcription service factory - dynamically selects service based on current config
        // This allows runtime switching between API and local transcription
        services.AddSingleton<ITranscriptionService, TranscriptionServiceFactory>();

        // Infrastructure services
        services.AddSingleton<IAudioRecorderService, NAudioRecorderService>();
        services.AddSingleton<IKeyboardListenerService, SharpHookKeyboardListenerService>();
        services.AddSingleton<IInputSimulatorService, SharpHookInputSimulatorService>();
        services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
        services.AddSingleton<ITextPostProcessor, TextPostProcessor>();

        // Application services
        services.AddSingleton<WhisperWriterService>();

        return services;
    }
}

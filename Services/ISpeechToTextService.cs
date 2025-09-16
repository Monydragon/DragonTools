namespace DragonTools.Services;

public interface ISpeechToTextService
{
    // Request any needed permissions (microphone, speech recognition)
    Task<bool> EnsurePermissionsAsync(CancellationToken cancellationToken = default);

    // Perform a one-shot recognition and return the transcribed text (or null/empty if canceled/failed)
    Task<string?> ListenOnceAsync(string? prompt = null, CancellationToken cancellationToken = default);

    // Start recognition and report partial results via callback; returns final/best text when complete
    Task<string?> ListenWithProgressAsync(Action<string> onPartial, string? prompt = null, CancellationToken cancellationToken = default);
}

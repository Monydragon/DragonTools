namespace DragonTools.Services;

public partial class SpeechToTextService : ISpeechToTextService
{
    public Task<bool> EnsurePermissionsAsync(CancellationToken cancellationToken = default)
        => PlatformEnsurePermissionsAsync(cancellationToken);

    public Task<string?> ListenOnceAsync(string? prompt = null, CancellationToken cancellationToken = default)
        => PlatformListenOnceAsync(prompt, cancellationToken);

    public Task<string?> ListenWithProgressAsync(Action<string> onPartial, string? prompt = null, CancellationToken cancellationToken = default)
        => PlatformListenWithProgressAsync(onPartial, prompt, cancellationToken);

    // Platform-specific implementations
    public partial Task<bool> PlatformEnsurePermissionsAsync(CancellationToken cancellationToken);
    public partial Task<string?> PlatformListenOnceAsync(string? prompt, CancellationToken cancellationToken);
    public partial Task<string?> PlatformListenWithProgressAsync(Action<string> onPartial, string? prompt, CancellationToken cancellationToken);
}

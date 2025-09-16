#if WINDOWS
using System.Threading;
using System.Threading.Tasks;
using DragonTools.Services;

namespace DragonTools.Services;

public partial class SpeechToTextService
{
    public partial Task<bool> PlatformEnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        // Microphone capability is declared in manifest; assume allowed.
        return Task.FromResult(true);
    }

    public partial Task<string?> PlatformListenOnceAsync(string? prompt, CancellationToken cancellationToken)
    {
        // TODO: Implement with Windows Speech APIs; return null for now.
        return Task.FromResult<string?>(null);
    }

    public partial Task<string?> PlatformListenWithProgressAsync(System.Action<string> onPartial, string? prompt, CancellationToken cancellationToken)
    {
        // TODO: Implement streaming with Windows Speech APIs
        return Task.FromResult<string?>(null);
    }
}
#endif

#if IOS
using System.Threading;
using System.Threading.Tasks;
using DragonTools.Services;

namespace DragonTools.Services;

public partial class SpeechToTextService
{
    public partial Task<bool> PlatformEnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        return Permissions.CheckStatusAsync<Permissions.Microphone>().ContinueWith(async t => {
            var status = t.Result;
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Microphone>();
            }
            return status == PermissionStatus.Granted;
        }).Unwrap();
    }

    public partial Task<string?> PlatformListenOnceAsync(string? prompt, CancellationToken cancellationToken)
    {
        // TODO: Implement using SFSpeechRecognizer + AVAudioEngine
        return Task.FromResult<string?>(null);
    }

    public partial Task<string?> PlatformListenWithProgressAsync(Action<string> onPartial, string? prompt, CancellationToken cancellationToken)
    {
        // TODO: Implement streaming with SFSpeechRecognizer + AVAudioEngine
        return Task.FromResult<string?>(null);
    }
}
#endif

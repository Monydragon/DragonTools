#if ANDROID
using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Speech;
using Microsoft.Maui.ApplicationModel;

namespace DragonTools.Services;

public partial class SpeechToTextService
{
    // Strong references and state
    private SpeechRecognizer? _activeRecognizer;
    private IRecognitionListener? _activeListener;
    private Timer? _timeoutTimer;
    private string? _bestPartial;
    private bool _inProgress;
    private readonly object _sync = new();
    private const int UiRequestCode = 0x53452; // 'SPE'
    private static TaskCompletionSource<string?>? _uiTcs;

    public partial async Task<bool> PlatformEnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Microphone>();
            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Permission error: {ex.Message}");
            return false;
        }
    }

    // UI fallback entry
    private static Task<string?> LaunchUiRecognizerAsync(string? prompt, CancellationToken token)
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            return Task.FromResult<string?>(null);
        }

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiTcs = tcs;

        if (token.CanBeCanceled)
        {
            token.Register(() =>
            {
                try { tcs.TrySetCanceled(token); } catch { }
                _uiTcs = null;
            });
        }

        try
        {
            var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);
            intent.PutExtra(RecognizerIntent.ExtraMaxResults, 5);
            intent.PutExtra(RecognizerIntent.ExtraPartialResults, false);
            if (!string.IsNullOrWhiteSpace(prompt))
                intent.PutExtra(RecognizerIntent.ExtraPrompt, prompt);

            activity.StartActivityForResult(intent, UiRequestCode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UI recognizer start error: {ex.Message}");
            tcs.TrySetResult(null);
            _uiTcs = null;
        }

        return tcs.Task;
    }

    // Hook from MainActivity
    public static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != UiRequestCode) return;
        var tcs = _uiTcs;
        _uiTcs = null;
        if (tcs == null) return;

        try
        {
            if (resultCode == Result.Ok && data != null)
            {
                var results = data.GetStringArrayListExtra(RecognizerIntent.ExtraResults);
                string? best = null;
                if (results != null && results.Count > 0)
                {
                    best = results[0];
                    for (int i = 1; i < results.Count; i++)
                    {
                        var c = results[i];
                        if (!string.IsNullOrWhiteSpace(c) && best != null && c.Length > best.Length)
                            best = c;
                    }
                }
                tcs.TrySetResult(best);
                return;
            }
            tcs.TrySetResult(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UI recognizer result error: {ex.Message}");
            tcs.TrySetResult(null);
        }
    }

    public partial Task<string?> PlatformListenOnceAsync(string? prompt, CancellationToken cancellationToken)
    {
        var activity = Platform.CurrentActivity;
        var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;
        if (context == null)
        {
            return Task.FromResult<string?>(null);
        }
        // Do not early-return on IsRecognitionAvailable; some devices report false erroneously
        // If StartListening fails, we’ll fall back to UI (when possible)

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            lock (_sync)
            {
                if (_inProgress)
                    CleanupRecognizer();
                _inProgress = true;
                _bestPartial = null;
            }

            try
            {
                var intent = new Android.Content.Intent(RecognizerIntent.ActionRecognizeSpeech);
                intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);
                intent.PutExtra(RecognizerIntent.ExtraLanguagePreference, Java.Util.Locale.Default.ToString());
                try { intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default.ToLanguageTag()); } catch { }
                intent.PutExtra(RecognizerIntent.ExtraCallingPackage, context.PackageName);
                intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
                intent.PutExtra(RecognizerIntent.ExtraMaxResults, 5);
                intent.PutExtra(RecognizerIntent.ExtraPreferOffline, false);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, 1200);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputPossiblyCompleteSilenceLengthMillis, 1200);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, 1200);
                if (!string.IsNullOrWhiteSpace(prompt))
                    intent.PutExtra(RecognizerIntent.ExtraPrompt, prompt);

                // Dynamic timeout management based on lifecycle
                void SetTimeout(int ms)
                {
                    try { _timeoutTimer?.Dispose(); } catch { }
                    _timeoutTimer = new Timer(_ =>
                    {
                        System.Diagnostics.Debug.WriteLine("⏰ Timeout");
                        Complete(GetBest());
                    }, null, ms, Timeout.Infinite);
                }

                // Initial overall timeout
                SetTimeout(25000);

                _activeListener = new BridgeListener(
                    onReady: () => { System.Diagnostics.Debug.WriteLine("🎤 Ready for speech"); SetTimeout(20000); },
                    onBegin: () => { System.Diagnostics.Debug.WriteLine("🗣️ Begin speech"); SetTimeout(8000); },
                    onPartial: s => { UpdateBest(s); SetTimeout(5000); },
                    onEnd: () => { System.Diagnostics.Debug.WriteLine("🛑 End of speech"); SetTimeout(3000); },
                    onFinal: s => { if (string.IsNullOrWhiteSpace(s)) s = GetBest(); Complete(s); },
                    onError: err => { System.Diagnostics.Debug.WriteLine($"❌ Error: {err}"); Complete(GetBest()); }
                );

                // Create recognizer BEFORE setting listener
                _activeRecognizer = SpeechRecognizer.CreateSpeechRecognizer(context);
                _activeRecognizer.SetRecognitionListener(_activeListener);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("🚫 Cancel requested");
                        Complete(GetBest(), canceled: true);
                    });
                }

                try
                {
                    _activeRecognizer.StartListening(intent);
                    System.Diagnostics.Debug.WriteLine("🚀 StartListening issued");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ StartListening exception: {ex.Message}");
                    Complete(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Setup exception: {ex.Message}");
                Complete(null);
            }
        });

        return tcs.Task;

        // Helpers
        void UpdateBest(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return;
            if (string.IsNullOrWhiteSpace(_bestPartial) || candidate.Length > _bestPartial!.Length)
                _bestPartial = candidate;
        }
        string? GetBest() => _bestPartial;

        void Complete(string? result, bool canceled = false)
        {
            lock (_sync)
            {
                if (!_inProgress) return;
                _inProgress = false;
            }

            try { _timeoutTimer?.Dispose(); } catch { }
            _timeoutTimer = null;

            MainThread.BeginInvokeOnMainThread(CleanupRecognizer);

            // Cancellation wins
            if (canceled)
            {
                tcs.TrySetCanceled(cancellationToken);
                return;
            }

            // If we have a non-empty result, finish
            if (!string.IsNullOrWhiteSpace(result))
            {
                tcs.TrySetResult(result);
                return;
            }

            // Fallback: launch UI recognizer if available
            var act = Platform.CurrentActivity;
            if (act != null && !cancellationToken.IsCancellationRequested)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var ui = await LaunchUiRecognizerAsync(prompt, cancellationToken);
                        if (cancellationToken.IsCancellationRequested)
                            tcs.TrySetCanceled(cancellationToken);
                        else
                            tcs.TrySetResult(ui);
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                    }
                    catch (Exception)
                    {
                        tcs.TrySetResult(null);
                    }
                });
            }
            else
            {
                // No UI fallback available; normalize empty => null
                tcs.TrySetResult(null);
            }
        }
    }

    public partial Task<string?> PlatformListenWithProgressAsync(Action<string> onPartial, string? prompt, CancellationToken cancellationToken)
    {
        var activity = Platform.CurrentActivity;
        var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;
        if (context == null)
            return Task.FromResult<string?>(null);
        // Do not early-return on IsRecognitionAvailable; try direct recognizer first

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            lock (_sync)
            {
                if (_inProgress)
                    CleanupRecognizer();
                _inProgress = true;
                _bestPartial = null;
            }

            try
            {
                var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
                intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
                intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default);
                intent.PutExtra(RecognizerIntent.ExtraLanguagePreference, Java.Util.Locale.Default.ToString());
                try { intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default.ToLanguageTag()); } catch { }
                intent.PutExtra(RecognizerIntent.ExtraCallingPackage, context.PackageName);
                intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
                intent.PutExtra(RecognizerIntent.ExtraMaxResults, 5);
                intent.PutExtra(RecognizerIntent.ExtraPreferOffline, false);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputCompleteSilenceLengthMillis, 1200);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputPossiblyCompleteSilenceLengthMillis, 1200);
                intent.PutExtra(RecognizerIntent.ExtraSpeechInputMinimumLengthMillis, 1200);
                if (!string.IsNullOrWhiteSpace(prompt)) intent.PutExtra(RecognizerIntent.ExtraPrompt, prompt);

                void SetTimeout(int ms)
                {
                    try { _timeoutTimer?.Dispose(); } catch { }
                    _timeoutTimer = new Timer(_ => Complete(GetBest()), null, ms, Timeout.Infinite);
                }
                SetTimeout(25000);

                _activeListener = new BridgeListener(
                    onReady: () => SetTimeout(20000),
                    onBegin: () => SetTimeout(8000),
                    onPartial: s => { UpdateBest(s); if (!string.IsNullOrWhiteSpace(s)) { try { onPartial(s); } catch { } } SetTimeout(5000); },
                    onEnd: () => SetTimeout(3000),
                    onFinal: s => { if (string.IsNullOrWhiteSpace(s)) s = GetBest(); Complete(s); },
                    onError: _ => { Complete(GetBest()); }
                );
                _activeRecognizer = SpeechRecognizer.CreateSpeechRecognizer(context);
                _activeRecognizer.SetRecognitionListener(_activeListener);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(() => Complete(GetBest(), canceled: true));
                }

                try { _activeRecognizer.StartListening(intent); } catch (Exception) { Complete(null); }
            }
            catch (Exception)
            {
                Complete(null);
            }
        });

        return tcs.Task;

        // local helpers
        void UpdateBest(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return;
            if (string.IsNullOrWhiteSpace(_bestPartial) || candidate.Length > _bestPartial!.Length)
                _bestPartial = candidate;
        }
        string? GetBest() => _bestPartial;
        void Complete(string? result, bool canceled = false)
        {
            lock (_sync)
            {
                if (!_inProgress) return;
                _inProgress = false;
            }
            try { _timeoutTimer?.Dispose(); } catch { }
            _timeoutTimer = null;
            MainThread.BeginInvokeOnMainThread(CleanupRecognizer);

            if (canceled)
            {
                tcs.TrySetCanceled(cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(result))
            {
                tcs.TrySetResult(result);
                return;
            }

            // Fallback: UI recognizer (no partials, but better than empty)
            var act = Platform.CurrentActivity;
            if (act != null && !cancellationToken.IsCancellationRequested)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var ui = await LaunchUiRecognizerAsync(prompt, cancellationToken);
                        if (cancellationToken.IsCancellationRequested)
                            tcs.TrySetCanceled(cancellationToken);
                        else
                            tcs.TrySetResult(ui);
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                    }
                    catch
                    {
                        tcs.TrySetResult(null);
                    }
                });
            }
            else
            {
                // No UI fallback available; normalize empty => null
                tcs.TrySetResult(null);
            }
        }
    }

    // Centralized cleanup for the active recognizer/listener
    private void CleanupRecognizer()
    {
        try
        {
            _activeRecognizer?.StopListening();
            _activeRecognizer?.Cancel();
            _activeRecognizer?.Destroy();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Cleanup error: {ex.Message}");
        }
        finally
        {
            _activeRecognizer = null;
            _activeListener = null;
        }
    }

    private sealed class BridgeListener : Java.Lang.Object, IRecognitionListener
    {
        private readonly Action _onReady;
        private readonly Action _onBegin;
        private readonly Action<string?> _onPartial;
        private readonly Action _onEnd;
        private readonly Action<string?> _onFinal;
        private readonly Action<string> _onError;

        public BridgeListener(Action onReady, Action onBegin, Action<string?> onPartial, Action onEnd, Action<string?> onFinal, Action<string> onError)
        { _onReady = onReady; _onBegin = onBegin; _onPartial = onPartial; _onEnd = onEnd; _onFinal = onFinal; _onError = onError; }

        public void OnReadyForSpeech(Android.OS.Bundle? @params) => _onReady();
        public void OnBeginningOfSpeech() => _onBegin();
        public void OnRmsChanged(float rmsdB) { }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEndOfSpeech() => _onEnd();
        public void OnError([Android.Runtime.GeneratedEnum] SpeechRecognizerError error) => _onError(error.ToString());
        public void OnResults(Android.OS.Bundle? results)
        {
            string? best = null;
            try
            {
                var matches = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
                if (matches != null && matches.Count > 0)
                {
                    best = matches[0];
                    for (int i = 1; i < matches.Count; i++)
                    {
                        var c = matches[i];
                        if (!string.IsNullOrWhiteSpace(c) && best != null && c.Length > best.Length)
                            best = c;
                    }
                }
            }
            catch { }
            _onFinal(best);
        }
        public void OnPartialResults(Android.OS.Bundle? partialResults)
        {
            try
            {
                var matches = partialResults?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
                if (matches != null && matches.Count > 0)
                {
                    string? best = null;
                    for (int i = 0; i < matches.Count; i++)
                    {
                        var c = matches[i];
                        if (string.IsNullOrWhiteSpace(c)) continue;
                        if (best == null || c.Length > best.Length) best = c;
                    }
                    _onPartial(best);
                }
            }
            catch { }
        }
        public void OnEvent(int eventType, Android.OS.Bundle? @params) { }
    }
}
#endif

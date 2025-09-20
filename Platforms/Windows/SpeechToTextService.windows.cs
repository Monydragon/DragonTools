#if WINDOWS
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;

namespace DragonTools.Services;

//TODO: Add Windows Voice Input Support
public partial class SpeechToTextService
{
    // Strong reference to prevent GC while async recognition runs
    private static SpeechRecognitionEngine? s_activeEngine;

    public partial Task<bool> PlatformEnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var engine = CreateEngine();
            return Task.FromResult(engine != null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public partial Task<string?> PlatformListenOnceAsync(string? prompt, CancellationToken cancellationToken)
        => RecognizeOnceAsync(cancellationToken);

    public partial Task<string?> PlatformListenWithProgressAsync(Action<string> onPartial, string? prompt, CancellationToken cancellationToken)
        => RecognizeWithProgressAsync(onPartial, cancellationToken);

    private static Task<string?> RecognizeOnceAsync(CancellationToken token)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        SpeechRecognitionEngine? engine = null;
        string? last = null;
        var started = Stopwatch.StartNew();
        var restarted = false;
        var audioHeard = false;

        void Cleanup()
        {
            try
            {
                if (engine != null)
                {
                    engine.AudioLevelUpdated -= OnLevel;
                    engine.SpeechDetected -= OnDetected;
                    engine.SpeechRecognitionRejected -= OnRejected;
                    engine.SpeechRecognized -= OnRecognized;
                    engine.RecognizeCompleted -= OnCompleted;
                }
            }
            catch { }
            try { engine?.Dispose(); } catch { }
            s_activeEngine = null;
        }

        void OnLevel(object? s, AudioLevelUpdatedEventArgs e) { if (e.AudioLevel > 0) audioHeard = true; }
        void OnDetected(object? s, SpeechDetectedEventArgs e) { audioHeard = true; }
        void OnRejected(object? s, SpeechRecognitionRejectedEventArgs e)
        {
            try
            {
                var alt = e.Result?.Alternates?.FirstOrDefault();
                var txt = (alt?.Text ?? e.Result?.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(txt)) last = txt;
            }
            catch { }
        }
        void OnRecognized(object? s, SpeechRecognizedEventArgs e)
        {
            try
            {
                var txt = (e.Result?.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(txt))
                    last = txt;
            }
            catch { }
        }
        void OnCompleted(object? s, RecognizeCompletedEventArgs e)
        {
            try
            {
                if (!restarted && string.IsNullOrWhiteSpace(last) && (started.ElapsedMilliseconds < 1500 || !audioHeard))
                {
                    restarted = true;
                    try
                    {
                        started.Restart();
                        audioHeard = false;
                        engine!.SetInputToDefaultAudioDevice();
                        engine.RecognizeAsync(RecognizeMode.Single);
                        return;
                    }
                    catch { /* fall through */ }
                }

                if (e.Cancelled || e.Error != null)
                {
                    tcs.TrySetResult(null);
                }
                else
                {
                    var text = !string.IsNullOrWhiteSpace(last) ? last : e.Result?.Text?.Trim();
                    tcs.TrySetResult(string.IsNullOrWhiteSpace(text) ? null : text);
                }
            }
            catch { tcs.TrySetResult(null); }
            finally { Cleanup(); }
        }

        try
        {
            engine = CreateEngine();
            if (engine == null) { tcs.TrySetResult(null); return tcs.Task; }
            s_activeEngine = engine;

            engine.InitialSilenceTimeout = TimeSpan.FromSeconds(12);
            engine.BabbleTimeout = TimeSpan.FromSeconds(5);
            engine.EndSilenceTimeout = TimeSpan.FromSeconds(2.5);

            engine.AudioLevelUpdated += OnLevel;
            engine.SpeechDetected += OnDetected;
            engine.SpeechRecognitionRejected += OnRejected;
            engine.SpeechRecognized += OnRecognized;
            engine.RecognizeCompleted += OnCompleted;

            if (token.CanBeCanceled)
                token.Register(() => { try { engine.RecognizeAsyncCancel(); } catch { } });

            engine.RecognizeAsync(RecognizeMode.Single);
        }
        catch { Cleanup(); tcs.TrySetResult(null); }

        return tcs.Task;
    }

    private static Task<string?> RecognizeWithProgressAsync(Action<string> onPartial, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        SpeechRecognitionEngine? engine = null;
        string? lastHypo = null;
        string? lastFinal = null;
        var started = Stopwatch.StartNew();
        var restarted = false;
        var audioHeard = false;

        System.Timers.Timer? silenceTimer = null;
        void ResetSilence(int ms)
        {
            try { silenceTimer?.Stop(); } catch { }
            silenceTimer ??= new System.Timers.Timer { AutoReset = false };
            silenceTimer.Interval = ms;
            silenceTimer.Elapsed += (_, __) => { try { engine?.RecognizeAsyncCancel(); } catch { } };
            try { silenceTimer.Start(); } catch { }
        }

        void Finish(string? text)
        {
            try { silenceTimer?.Stop(); silenceTimer?.Dispose(); } catch { }
            try
            {
                if (engine != null)
                {
                    engine.AudioLevelUpdated -= OnLevel;
                    engine.SpeechDetected -= OnDetected;
                    engine.SpeechHypothesized -= OnHypo;
                    engine.SpeechRecognitionRejected -= OnRejected;
                    engine.SpeechRecognized -= OnRec;
                    engine.RecognizeCompleted -= OnComp;
                }
            }
            catch { }
            try { engine?.Dispose(); } catch { }
            s_activeEngine = null;
            tcs.TrySetResult(string.IsNullOrWhiteSpace(text) ? null : text);
        }

        void OnLevel(object? s, AudioLevelUpdatedEventArgs e) { if (e.AudioLevel > 0) audioHeard = true; }
        void OnDetected(object? s, SpeechDetectedEventArgs e) { audioHeard = true; }
        void OnHypo(object? s, SpeechHypothesizedEventArgs e)
        {
            var h = (e.Result?.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(h))
            {
                lastHypo = h;
                try { onPartial(h); } catch { }
                ResetSilence(5000);
            }
        }
        void OnRejected(object? s, SpeechRecognitionRejectedEventArgs e)
        {
            try
            {
                var alt = e.Result?.Alternates?.FirstOrDefault();
                var txt = (alt?.Text ?? e.Result?.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    lastHypo = txt;
                    try { onPartial(txt); } catch { }
                    ResetSilence(4000);
                }
            }
            catch { }
        }
        void OnRec(object? s, SpeechRecognizedEventArgs e)
        {
            var t = (e.Result?.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(t))
            {
                lastFinal = t;
                try { onPartial(t); } catch { }
                ResetSilence(2000);
            }
        }
        void OnComp(object? s, RecognizeCompletedEventArgs e)
        {
            if (!restarted && string.IsNullOrWhiteSpace(lastHypo) && string.IsNullOrWhiteSpace(lastFinal) && (started.ElapsedMilliseconds < 1500 || !audioHeard))
            {
                restarted = true;
                try
                {
                    started.Restart();
                    audioHeard = false;
                    engine!.SetInputToDefaultAudioDevice();
                    engine.RecognizeAsync(RecognizeMode.Multiple);
                    ResetSilence(20000);
                    return;
                }
                catch { /* fall through */ }
            }
            Finish(!string.IsNullOrWhiteSpace(lastFinal) ? lastFinal : lastHypo);
        }

        try
        {
            engine = CreateEngine();
            if (engine == null) { tcs.TrySetResult(null); return tcs.Task; }
            s_activeEngine = engine;

            engine.InitialSilenceTimeout = TimeSpan.FromSeconds(10);
            engine.BabbleTimeout = TimeSpan.FromSeconds(5);
            engine.EndSilenceTimeout = TimeSpan.FromSeconds(2);

            engine.AudioLevelUpdated += OnLevel;
            engine.SpeechDetected += OnDetected;
            engine.SpeechHypothesized += OnHypo;
            engine.SpeechRecognitionRejected += OnRejected;
            engine.SpeechRecognized += OnRec;
            engine.RecognizeCompleted += OnComp;

            if (token.CanBeCanceled)
                token.Register(() => { try { engine.RecognizeAsyncCancel(); } catch { } Finish(null); });

            engine.RecognizeAsync(RecognizeMode.Multiple);
            ResetSilence(20000);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[STT][Cont] Error: {ex.Message}");
            try { engine?.Dispose(); } catch { }
            s_activeEngine = null;
            tcs.TrySetResult(null);
        }

        return tcs.Task;
    }

    private static SpeechRecognitionEngine? CreateEngine()
    {
        try
        {
            var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            if (recognizers == null || recognizers.Count == 0)
            {
                Debug.WriteLine("[STT] No SAPI recognizers installed.");
                return null;
            }

            var ui = CultureInfo.CurrentUICulture.Name;
            var ordered = recognizers
                .OrderByDescending(r => string.Equals(r.Culture.Name, ui, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(r => string.Equals(r.Culture.Name, "en-US", StringComparison.OrdinalIgnoreCase))
                .ThenBy(r => r.Culture.Name)
                .ToList();

            foreach (var info in ordered)
            {
                SpeechRecognitionEngine? eng = null;
                try
                {
                    eng = new SpeechRecognitionEngine(info);
                    eng.SetInputToDefaultAudioDevice();
                    eng.LoadGrammar(new DictationGrammar());
                    Debug.WriteLine($"[STT] Using recognizer: {info.Name} ({info.Culture})");
                    return eng;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[STT] Skipping recognizer {info.Name} ({info.Culture}): {ex.Message}");
                    try { eng?.Dispose(); } catch { }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[STT] CreateEngine error: {ex.Message}");
            return null;
        }
    }
}
#endif

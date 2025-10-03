using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DragonTools.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
#if DEBUG
        // WinUI dispatcher-level unhandled exceptions (UI thread)
        UnhandledException += (sender, e) =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[WinUI UnhandledException] {e.Exception?.GetType().Name}: {e.Exception?.Message}\n{e.Exception?.StackTrace}");
            }
            catch { }
#if !DISABLE_XAML_GENERATED_BREAK_ON_UNHANDLED_EXCEPTION
            if (global::System.Diagnostics.Debugger.IsAttached)
            {
                global::System.Diagnostics.Debugger.Break();
            }
#endif
        };
#endif
        // Non-UI unobserved task exceptions
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Task Unobserved] {e.Exception}" );
        };
        // AppDomain level (should catch background thread exceptions that terminate process)
        System.AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Domain Unhandled] {e.ExceptionObject}" );
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace DragonTools.Navigation;

/// <summary>
/// Centralized guarded navigation helper to avoid race conditions between page transitions
/// and heavy UI list/layout updates (mitigating WinUI measure COM exceptions).
/// </summary>
public static class SafeNavigation
{
    private static readonly SemaphoreSlim _navLock = new(1, 1);

    static async Task<T> InvokeOnMainAsync<T>(Func<Task<T>> func)
    {
        if (MainThread.IsMainThread)
            return await func();
        return await MainThread.InvokeOnMainThreadAsync(func);
    }

    static async Task InvokeOnMainAsync(Func<Task> func)
    {
        if (MainThread.IsMainThread)
            await func();
        else
            await MainThread.InvokeOnMainThreadAsync(func);
    }

    /// <summary>
    /// Pushes a page onto the navigation stack safely.
    /// </summary>
    public static async Task<bool> PushAsync(INavigation? nav, Page? page, bool animated = true)
    {
        if (nav == null || page == null) return false;
        await _navLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await InvokeOnMainAsync(async () =>
            {
                try
                {
                    await nav.PushAsync(page, animated);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Nav] PushAsync failed: {ex}");
                    return false;
                }
            });
        }
        finally { _navLock.Release(); }
    }

    /// <summary>
    /// Pops the top page; if expectedTop is supplied and not on top, attempts to remove it directly.
    /// </summary>
    public static Task<bool> PopAsync(INavigation? nav, Page? expectedTop, bool animated = true) => PopInternal(nav, expectedTop, animated);

    public static Task<bool> PopAsync(INavigation? nav, bool animated = true) => PopInternal(nav, null, animated);

    static async Task<bool> PopInternal(INavigation? nav, Page? expectedTop, bool animated)
    {
        if (nav == null) return false;
        await _navLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await InvokeOnMainAsync(async () =>
            {
                try
                {
                    // Allow any pending UI dispatcher work (like CollectionView refresh) to settle
                    await Task.Yield();
                    await MainThread.InvokeOnMainThreadAsync(() => { });

                    var stack = nav.NavigationStack;
                    if (stack == null || stack.Count == 0) return false;
                    var top = stack.Last();
                    if (expectedTop != null && !ReferenceEquals(top, expectedTop))
                    {
                        // If expected still in stack, remove it instead of popping something else
                        if (stack.Contains(expectedTop))
                        {
                            nav.RemovePage(expectedTop);
                            return true;
                        }
                        return false;
                    }

                    await nav.PopAsync(animated);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Nav] PopAsync failed: {ex}");
                    return false;
                }
            });
        }
        finally { _navLock.Release(); }
    }

    /// <summary>
    /// Attempts to remove a specific page instance anywhere in the stack.
    /// </summary>
    public static async Task<bool> RemoveAsync(INavigation? nav, Page? page)
    {
        if (nav == null || page == null) return false;
        await _navLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await InvokeOnMainAsync(async () =>
            {
                try
                {
                    var stack = nav.NavigationStack;
                    if (stack.Contains(page))
                    {
                        nav.RemovePage(page);
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Nav] RemoveAsync failed: {ex}");
                    return false;
                }
            });
        }
        finally { _navLock.Release(); }
    }
}


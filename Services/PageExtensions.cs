using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace DragonTools.Services;

public static class PageExtensions
{
    public static Task DisplayAlertAsync(this Page page, string title, string message, string cancel)
        => page.DisplayAlert(title, message, cancel);

    public static Task<bool> DisplayAlertAsync(this Page page, string title, string message, string accept, string cancel)
        => page.DisplayAlert(title, message, accept, cancel);
}


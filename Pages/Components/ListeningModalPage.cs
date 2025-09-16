using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace DragonTools.Pages.Components;

public sealed class ListeningModalPage : ContentPage
{
    private readonly Label _transcript;
    private readonly Label _title;
    private readonly Label _hint;
    private readonly Button _cancel;

    public event Action? CancelRequested;

    public ListeningModalPage(string title = "Listening…", string? hint = null)
    {
        BackgroundColor = new Color(0f, 0f, 0f, 0.35f);
        Padding = new Thickness(24);

        var card = new Border
        {
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E1E1E") : Colors.White,
            Stroke = new SolidColorBrush(Color.FromArgb("#334155")),
            StrokeThickness = 1,
            Padding = 16,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    (_title = new Label { Text = title, FontAttributes = FontAttributes.Bold, FontSize = 18, HorizontalTextAlignment = TextAlignment.Center }),
                    new ActivityIndicator { IsRunning = true, HorizontalOptions = LayoutOptions.Center },
                    (_hint = new Label { Text = string.IsNullOrWhiteSpace(hint) ? "Say items separated by comma…" : hint, FontSize = 12, Opacity = 0.7, HorizontalTextAlignment = TextAlignment.Center }),
                    (_transcript = new Label { Text = string.Empty, FontSize = 16, LineBreakMode = LineBreakMode.WordWrap, HorizontalTextAlignment = TextAlignment.Center }),
                    (_cancel = new Button { Text = "Cancel", BackgroundColor = Color.FromArgb("#EF4444"), TextColor = Colors.White, CornerRadius = 8, Padding = new Thickness(16,10) })
                }
            }
        };

        _cancel.Clicked += (_, __) => CancelRequested?.Invoke();

        Content = new Grid
        {
            Children = { card },
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill
        };
    }

    public void UpdateTranscript(string text)
    {
        _transcript.Text = text;
    }

    public void UpdateTitle(string title)
    {
        _title.Text = title;
    }

    public void UpdateHint(string hint)
    {
        _hint.Text = hint;
    }
}

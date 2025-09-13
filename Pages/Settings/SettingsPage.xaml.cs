using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using DragonTools.Services;

namespace DragonTools.Pages.Settings;

public partial class SettingsPage : ContentPage
{
    readonly SettingsVm _vm = new();

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = _vm;

        // Dynamically add tool sections here
        _vm.ToolSections.Add(BuildRandomPickerSection());
    }

    private ToolSection BuildRandomPickerSection()
    {
        // Content for Random Picker section (built in code for flexibility)
        var dedupSwitch = new Switch();
        dedupSwitch.SetBinding(Switch.IsToggledProperty, nameof(SettingsVm.RP_Deduplicate), BindingMode.TwoWay);

        var dedupRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };
        dedupRow.Children.Add(dedupSwitch);
        Grid.SetColumn(dedupSwitch, 0);
        Grid.SetRow(dedupSwitch, 0);

        var dedupLabel = new Label { Text = "Deduplicate while adding (Bulk Add)", VerticalOptions = LayoutOptions.Center };
        dedupRow.Children.Add(dedupLabel);
        Grid.SetColumn(dedupLabel, 1);
        Grid.SetRow(dedupLabel, 0);

        var normSwitch = new Switch();
        normSwitch.SetBinding(Switch.IsToggledProperty, nameof(SettingsVm.RP_NormalizeSpaces), BindingMode.TwoWay);

        var normRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        normRow.Children.Add(normSwitch);
        Grid.SetColumn(normSwitch, 0);
        Grid.SetRow(normSwitch, 0);

        var normLabel = new Label { Text = "Normalize spaces (collapse multiple spaces)", VerticalOptions = LayoutOptions.Center };
        normRow.Children.Add(normLabel);
        Grid.SetColumn(normLabel, 1);
        Grid.SetRow(normLabel, 0);

        var stylePicker = new Picker { Title = "Bulk hint style" };
        stylePicker.ItemsSource = _vm.PlaceholderStyleOptions;
        stylePicker.SetBinding(Picker.SelectedItemProperty, nameof(SettingsVm.RP_PlaceholderStyle), BindingMode.TwoWay);

        var styleRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };
        var styleLbl = new Label { Text = "Bulk hint style", VerticalOptions = LayoutOptions.Center };
        styleRow.Children.Add(styleLbl);
        Grid.SetColumn(styleLbl, 0);
        Grid.SetRow(styleLbl, 0);

        styleRow.Children.Add(stylePicker);
        Grid.SetColumn(stylePicker, 1);
        Grid.SetRow(stylePicker, 0);

        var previewTitle = new Label { Text = "Preview:", FontAttributes = FontAttributes.Bold };
        var preview = new Label { LineBreakMode = LineBreakMode.WordWrap, Opacity = 0.8 /* use platform monospace if desired */ };
        preview.SetBinding(Label.TextProperty, nameof(SettingsVm.RP_BulkPlaceholderPreview));

        var layout = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label{ Text="Random Picker", FontSize=16, FontAttributes=FontAttributes.Bold, Opacity=0.8 },
                dedupRow,
                normRow,
                styleRow,
                previewTitle,
                preview
            }
        };

        return new ToolSection("Random Picker", new ContentView { Content = layout });
    }
}

/* ================= VM and helper models ================= */

public sealed class ToolSection
{
    public string Title { get; }
    public View Content { get; }

    public ToolSection(string title, View content) { Title = title; Content = content; }
}

public class SettingsVm : INotifyPropertyChanged
{
    public ObservableCollection<ToolSection> ToolSections { get; } = new();

    public SettingsVm()
    {
        // Load theme
        var t = SettingsService.GetTheme();
        Theme_System = t == AppThemePref.System;
        Theme_Light  = t == AppThemePref.Light;
        Theme_Dark   = t == AppThemePref.Dark;

        // Load Random Picker settings
        var rp = SettingsService.GetRandomPicker();
        _rpDedup = rp.Deduplicate;
        _rpNormalize = rp.NormalizeSpaces;
        _rpStyle = rp.PlaceholderStyle;
    }

    // ---- Appearance (bound to 3 radios in XAML via IsChecked) ----
    bool _themeSystem, _themeLight, _themeDark;
    public bool Theme_System { get => _themeSystem; set { if (Set(ref _themeSystem, value)) if (value) SaveTheme(AppThemePref.System); } }
    public bool Theme_Light  { get => _themeLight;  set { if (Set(ref _themeLight,  value)) if (value) SaveTheme(AppThemePref.Light); } }
    public bool Theme_Dark   { get => _themeDark;   set { if (Set(ref _themeDark,   value)) if (value) SaveTheme(AppThemePref.Dark); } }

    void SaveTheme(AppThemePref pref)
    {
        _themeSystem = pref == AppThemePref.System;
        _themeLight  = pref == AppThemePref.Light;
        _themeDark   = pref == AppThemePref.Dark;
        OnPropertyChanged(nameof(Theme_System));
        OnPropertyChanged(nameof(Theme_Light));
        OnPropertyChanged(nameof(Theme_Dark));
        SettingsService.SetTheme(pref);
    }

    // ---- Random Picker settings ----
    bool _rpDedup;
    public bool RP_Deduplicate
    {
        get => _rpDedup;
        set { if (Set(ref _rpDedup, value)) SaveRandomPicker(); }
    }

    bool _rpNormalize;
    public bool RP_NormalizeSpaces
    {
        get => _rpNormalize;
        set { if (Set(ref _rpNormalize, value)) SaveRandomPicker(); }
    }

    BulkPlaceholderStyle _rpStyle;
    public BulkPlaceholderStyle RP_PlaceholderStyle
    {
        get => _rpStyle;
        set
        {
            if (!Set(ref _rpStyle, value)) return;
            OnPropertyChanged(nameof(RP_BulkPlaceholderPreview));
            SaveRandomPicker();
        }
    }

    public string[] PlaceholderStyleOptions { get; } = new[] { "Single line", "Multi-line" };

    public string RP_BulkPlaceholderPreview =>
        _rpStyle == BulkPlaceholderStyle.SingleLine
            ? "Taco, Burrito[2], Pasta[15], Burger[4]"
            : "Taco\nBurrito[2]\nPasta[15]\nBurger[4]";

    void SaveRandomPicker()
    {
        SettingsService.SetRandomPicker(new RandomPickerSettings
        {
            Deduplicate = _rpDedup,
            NormalizeSpaces = _rpNormalize,
            PlaceholderStyle = _rpStyle
        });
    }

    /* INotifyPropertyChanged */
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? m = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(m));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? m = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(m);
        return true;
    }
}

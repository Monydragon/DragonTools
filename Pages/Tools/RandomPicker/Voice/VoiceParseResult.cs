namespace DragonTools.Pages.Tools.RandomPicker.Voice;

public sealed class VoiceParseResult
{
    public string Entry { get; set; } = string.Empty;
    public int? Weight { get; set; }
    public double? Percentage { get; set; }
    // Reserved for future attributes
    // public Dictionary<string, object>? Extras { get; set; }
}


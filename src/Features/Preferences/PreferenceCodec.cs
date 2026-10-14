using HidePlayers.Contract;

namespace HidePlayers;

/// <summary>Converts preferences to the string that the Cookies plugin stores, and back.</summary>
internal static class PreferenceCodec
{
    private const string Version = "1";
    private const char Separator = '|';

    public static string Encode(HidePreferences preferences, HideMode lastActiveMode) =>
        string.Join(Separator, Version, preferences.Mode, preferences.HideDead ? "1" : "0", lastActiveMode);

    /// <summary>Parses a stored value. Anything that is not a complete value of the current version counts as absent.</summary>
    public static bool TryParse(string? value, out HidePreferences preferences, out HideMode lastActiveMode)
    {
        preferences = default;
        lastActiveMode = HideMode.All;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var parts = value.Split(Separator);
        if (parts.Length != 4 || parts[0] != Version)
        {
            return false;
        }

        if (!TryParseMode(parts[1], out var mode) || !TryParseMode(parts[3], out var lastActive) || lastActive == HideMode.Off)
        {
            return false;
        }

        bool hideDead;
        switch (parts[2])
        {
            case "1":
                hideDead = true;
                break;
            case "0":
                hideDead = false;
                break;
            default:
                return false;
        }

        preferences = new HidePreferences(mode, hideDead);
        lastActiveMode = lastActive;
        return true;
    }

    // Enum.TryParse also accepts numbers and undefined values; only the exact member names are valid here.
    private static bool TryParseMode(string text, out HideMode mode) =>
        Enum.TryParse(text, ignoreCase: false, out mode) && Enum.GetName(mode) == text;
}

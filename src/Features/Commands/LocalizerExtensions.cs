using SwiftlyS2.Shared.Translation;

namespace HidePlayers;

internal static class LocalizerExtensions
{
    public static string Prefixed(this ILocalizer localizer, string key, string prefix, params object[] args)
    {
        var body = localizer[key, args].Trim();
        return string.IsNullOrWhiteSpace(prefix) ? body : $"{prefix.Trim()} {body}".TrimEnd();
    }
}

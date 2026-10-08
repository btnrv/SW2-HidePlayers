using System.Text.Json;

namespace HidePlayers.Configuration;

public sealed class SettingsService(string path)
{
    public PluginSettings Current { get; } = Parse(File.ReadAllText(path));

    public static PluginSettings Parse(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        return Validate(document.RootElement.GetProperty("Main").Deserialize<PluginSettings>()!);
    }

    private static PluginSettings Validate(PluginSettings settings)
    {
        if (!Enum.IsDefined(settings.DefaultPreferences.Mode))
            throw new ArgumentException("DefaultPreferences.Mode must be Off, All, Teammates, or Enemies.");
        if (!Enum.IsDefined(settings.MenuRenderer))
            throw new ArgumentException("MenuRenderer must be Framework or Panorama.");
        if (settings.MenuRenderer == MenuRenderer.Panorama && string.IsNullOrWhiteSpace(settings.PanoramaLayout))
            throw new ArgumentException("PanoramaLayout must identify a mounted layout resource.");
        var names = new[] { settings.MenuCommand.Name }.Concat(settings.MenuCommand.Aliases).ToArray();
        if (names.Any(name => string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace)))
            throw new ArgumentException("Menu command names must be nonempty and contain no whitespace.");
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            throw new ArgumentException("Menu command names and aliases must be distinct.");
        return settings;
    }
}

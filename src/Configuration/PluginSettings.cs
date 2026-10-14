using System.ComponentModel;
using System.Text.Json.Serialization;
using HidePlayers.Contract;
using Microsoft.Extensions.Options;

namespace HidePlayers;

[JsonConverter(typeof(JsonStringEnumConverter<MenuRenderer>))]
public enum MenuRenderer
{
    Framework,
    Panorama
}

public sealed class PluginSettings
{
    [Description("Choices a player starts with until their saved choices load. Mode is Off, All, Teammates or Enemies.")]
    public DefaultPreferencesSettings DefaultPreferences { get; set; } = new();

    [Description("Chat command that opens the menu or changes the choices. Name and aliases apply when the plugin loads.")]
    public CommandSettings MenuCommand { get; set; } = new();

    [Description("Menu that a bare chat command opens: Framework or Panorama. Applies when the plugin loads.")]
    public MenuRenderer MenuRenderer { get; set; } = MenuRenderer.Framework;

    [Description("Panorama layout resource that the Panorama menu loads. It must exist in a mounted addon.")]
    public string PanoramaLayout { get; set; } = "panorama/layout/custom_game/hideplayers/hideplayers.xml";

    [Description("Text in front of every chat reply. Uses chat color tokens such as [gold] and [default].")]
    public string Prefix { get; set; } = "[gold][HidePlayers][default] ";
}

public sealed class DefaultPreferencesSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter<HideMode>))]
    public HideMode Mode { get; set; } = HideMode.Off;

    public bool HideDead { get; set; } = true;
}

public sealed class CommandSettings
{
    public string Name { get; set; } = "hide";

    public List<string> Aliases { get; set; } = ["hidemodels"];

    [Description("Permission that players need to use the command. Empty allows everyone.")]
    public string Permission { get; set; } = "";
}

public sealed class PluginSettingsValidator : IValidateOptions<PluginSettings>
{
    public ValidateOptionsResult Validate(string? name, PluginSettings options)
    {
        var failures = new List<string>();
        if (!Enum.IsDefined(options.DefaultPreferences.Mode))
        {
            failures.Add("DefaultPreferences.Mode must be Off, All, Teammates or Enemies.");
        }

        if (!Enum.IsDefined(options.MenuRenderer))
        {
            failures.Add("MenuRenderer must be Framework or Panorama.");
        }

        if (options.MenuRenderer == MenuRenderer.Panorama && string.IsNullOrWhiteSpace(options.PanoramaLayout))
        {
            failures.Add("PanoramaLayout must name a mounted layout resource when MenuRenderer is Panorama.");
        }

        var commandNames = new List<string> { options.MenuCommand.Name };
        commandNames.AddRange(options.MenuCommand.Aliases);
        if (commandNames.Any(commandName => string.IsNullOrWhiteSpace(commandName) || commandName.Any(char.IsWhiteSpace)))
        {
            failures.Add("MenuCommand.Name and MenuCommand.Aliases must be non-empty and contain no whitespace.");
        }
        else if (commandNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != commandNames.Count)
        {
            failures.Add("MenuCommand.Name and MenuCommand.Aliases must be distinct.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

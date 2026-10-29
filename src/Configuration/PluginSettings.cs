using System.ComponentModel;
using System.Text.Json.Serialization;
using HidePlayers.Contract;
using Microsoft.Extensions.Options;

namespace HidePlayers;

public sealed class PluginSettings
{
    [Description("Choices a player starts with until their saved choices load. Mode is Off, All, Teammates or Enemies.")]
    public DefaultPreferencesSettings DefaultPreferences { get; set; } = new();

    [Description("Command that toggles hiding or changes choices. The legacy MenuCommand key is retained for existing configurations. Names and permissions apply when the plugin loads; hidemenu opens the native menu with the same permission.")]
    public CommandSettings MenuCommand { get; set; } = new();

    [Description("Text in front of every chat reply. Uses chat color tokens such as [gold] and [default].")]
    public string Prefix { get; set; } = "[darkblue][HidePlayers][default]";
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
        else if (commandNames.Contains("hidemenu", StringComparer.OrdinalIgnoreCase))
        {
            failures.Add("hidemenu is reserved for the native menu and cannot be a toggle command or alias.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

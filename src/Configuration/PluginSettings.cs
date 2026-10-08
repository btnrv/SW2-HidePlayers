using HidePlayers.Contract;
using System.Text.Json.Serialization;

namespace HidePlayers.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter<MenuRenderer>))]
public enum MenuRenderer { Framework, Panorama }

public sealed class PluginSettings
{
    public HidePreferences DefaultPreferences { get; set; } = new(HideMode.Off, true);
    public CommandSettings MenuCommand { get; set; } = new()
    {
        Name = "hide",
        Aliases = ["hidemodels"]
    };
    public MenuRenderer MenuRenderer { get; set; } = MenuRenderer.Framework;
    public string PanoramaLayout { get; set; } = "panorama/layout/custom_game/hideplayers.vxml_c";
    public string DatabaseConnection { get; set; } = "default";
    public string Prefix { get; set; } = "{Gold}[HidePlayers]{Default} ";
}

public sealed class CommandSettings
{
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = [];
    public string Permission { get; set; } = "";
}

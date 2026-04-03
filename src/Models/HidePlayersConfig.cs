namespace HidePlayers.Models;

public sealed class HidePlayersConfig
{
    public string Prefix { get; set; } = "[gold][HidePlayers][default] ";
    public List<string> Commands { get; set; } = ["sw_hide"];
    public string CommandPermission { get; set; } = "";
    public string ReloadCommand { get; set; } = "hide_reload";
    public string ReloadPermission { get; set; } = "hideplayers.reload";
    public string HideMode { get; set; } = "@all";
}

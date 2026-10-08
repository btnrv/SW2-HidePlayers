namespace HidePlayers.Configuration;

public static class PanoramaMenu
{
    public const string RootPanel = "hideplayers_root";
    public const string TitlePanel = "hideplayers_title";
    public const string OverridePanel = "hideplayers_override";
    public const string DeadPanel = "hideplayers_dead_label";
    public const string ClosePanel = "hideplayers_close_label";

    public static string? CommandArguments(string buttonId) => buttonId switch
    {
        "hideplayers_off" => "off",
        "hideplayers_all" => "all",
        "hideplayers_team" => "team",
        "hideplayers_enemy" => "enemy",
        "hideplayers_dead_on" => "dead on",
        "hideplayers_dead_off" => "dead off",
        _ => null
    };
}

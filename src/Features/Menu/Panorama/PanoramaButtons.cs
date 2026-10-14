using HidePlayers.Contract;

namespace HidePlayers;

/// <summary>One button of the Panorama layout: it selects a mode, sets corpse hiding, or closes the menu.</summary>
internal readonly record struct PanoramaButton(string Id, HideMode? Mode, bool? HideDead, bool Close = false);

/// <summary>The panel and button ids of hideplayers.xml. Keep them in step with the layout.</summary>
internal static class PanoramaIds
{
    public const string RootPanel = "hideplayers_root";
    public const string TitlePanel = "hideplayers_title";
    public const string OverridePanel = "hideplayers_override";
    public const string DeadPanel = "hideplayers_dead_label";
    public const string ClosePanel = "hideplayers_close_label";
    public const string DismissedClass = "Dismissed";
    public const string SelectedClass = "Selected";

    // The one table that maps a clicked button id to its action. Labels sit in the panel "<id>_label".
    public static readonly PanoramaButton[] Buttons =
    [
        new("hideplayers_off", HideMode.Off, null),
        new("hideplayers_all", HideMode.All, null),
        new("hideplayers_team", HideMode.Teammates, null),
        new("hideplayers_enemy", HideMode.Enemies, null),
        new("hideplayers_dead_on", null, true),
        new("hideplayers_dead_off", null, false),
        new("hideplayers_close", null, null, Close: true)
    ];

    public static bool TryGetButton(string id, out PanoramaButton button)
    {
        foreach (var candidate in Buttons)
        {
            if (candidate.Id == id)
            {
                button = candidate;
                return true;
            }
        }

        button = default;
        return false;
    }
}

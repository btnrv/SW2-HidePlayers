using HidePlayers.Contract;

namespace HidePlayers.Configuration;

public static class HideCommandParser
{
    public static bool TryParse(string[] arguments, HidePreferences current, out HidePreferences result)
    {
        result = current;
        if (arguments.Length == 1)
        {
            HideMode? mode = arguments[0].ToLowerInvariant() switch
            {
                "off" => HideMode.Off,
                "all" => HideMode.All,
                "team" or "teammates" => HideMode.Teammates,
                "enemy" or "enemies" => HideMode.Enemies,
                _ => null
            };
            if (mode is not { } selected)
                return false;
            result = current with { Mode = selected };
            return true;
        }

        if (arguments.Length == 2 && arguments[0].Equals("dead", StringComparison.OrdinalIgnoreCase))
        {
            bool? hideDead = arguments[1].ToLowerInvariant() switch
            {
                "on" => true,
                "off" => false,
                "toggle" => !current.HideDead,
                _ => null
            };
            if (hideDead is not { } selected)
                return false;
            result = current with { HideDead = selected };
            return true;
        }

        return false;
    }
}

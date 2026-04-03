namespace HidePlayers.Models;

public enum HideMode : byte
{
    All = 0,
    Team,
    Enemy
}

public static class HideModeParser
{
    public static HideMode Parse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "all" or "@all" => HideMode.All,
            "team" or "@team" => HideMode.Team,
            "enemy" or "@enemy" => HideMode.Enemy,
            _ => HideMode.All
        };
    }
}

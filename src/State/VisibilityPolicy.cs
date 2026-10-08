using HidePlayers.Contract;

namespace HidePlayers.State;

public static class VisibilityPolicy
{
    public static bool ShouldHide(bool viewerAlive, int viewerTeam, HidePreferences preferences, bool isSelf, int targetTeam, bool targetDead)
    {
        if (targetDead)
            return preferences.HideDead;
        if (isSelf || !viewerAlive || viewerTeam is not (2 or 3) || targetTeam is not (2 or 3))
            return false;
        return preferences.Mode switch
        {
            HideMode.All => true,
            HideMode.Teammates => viewerTeam == targetTeam,
            HideMode.Enemies => viewerTeam != targetTeam,
            _ => false
        };
    }
}

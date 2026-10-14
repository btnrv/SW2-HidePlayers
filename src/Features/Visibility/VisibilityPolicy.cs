using HidePlayers.Contract;
using SwiftlyS2.Shared.Players;

namespace HidePlayers;

/// <summary>What the policy needs to know about the player who looks. Indexes are entity indexes, or -1 for none.</summary>
internal readonly record struct ViewerView(bool Alive, Team Team, int OwnPawnIndex, int ObservedIndex);

/// <summary>What the policy needs to know about a player pawn that might be hidden.</summary>
internal readonly record struct TargetView(int Index, Team Team, bool Alive);

internal static class VisibilityPolicy
{
    /// <summary>Decides whether a viewer must not receive a pawn. The checks run in this order.</summary>
    public static bool ShouldHide(HidePreferences preferences, ViewerView viewer, TargetView target)
    {
        // A client that cannot see its own pawn or the pawn it observes loses its camera.
        if (target.Index == viewer.OwnPawnIndex || target.Index == viewer.ObservedIndex)
        {
            return false;
        }

        if (!target.Alive)
        {
            return preferences.HideDead;
        }

        if (!viewer.Alive || !IsPlayingTeam(viewer.Team) || !IsPlayingTeam(target.Team))
        {
            return false;
        }

        return preferences.Mode switch
        {
            HideMode.All => true,
            HideMode.Teammates => viewer.Team == target.Team,
            HideMode.Enemies => viewer.Team != target.Team,
            _ => false
        };
    }

    private static bool IsPlayingTeam(Team team) => team is Team.T or Team.CT;
}

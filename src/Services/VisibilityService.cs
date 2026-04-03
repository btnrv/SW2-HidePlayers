using HidePlayers.Models;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace HidePlayers.Services;

public sealed class VisibilityService
{
    private readonly ISwiftlyCore _core;
    private readonly Dictionary<int, bool> _hideEnabled = [];
    private readonly Dictionary<int, HashSet<int>> _activeBlocks = [];
    private bool _recalcScheduled;

    public VisibilityService(ISwiftlyCore core)
    {
        _core = core;
    }

    public void InitPlayer(int playerId)
    {
        _hideEnabled.TryAdd(playerId, false);
        _activeBlocks.TryAdd(playerId, []);
    }

    public void RemovePlayer(int playerId)
    {
        ClearBlocksForViewer(playerId);
        _hideEnabled.Remove(playerId);
        _activeBlocks.Remove(playerId);
    }

    public bool Toggle(int playerId)
    {
        _hideEnabled.TryGetValue(playerId, out var current);
        _hideEnabled[playerId] = !current;
        return !current;
    }

    public bool IsHideEnabled(int playerId)
    {
        return _hideEnabled.TryGetValue(playerId, out var enabled) && enabled;
    }

    public void ScheduleRecalculation(HideMode mode)
    {
        if (_recalcScheduled)
            return;

        _recalcScheduled = true;
        _core.Scheduler.NextTick(() =>
        {
            _recalcScheduled = false;
            RecalculateAll(mode);
        });
    }

    public void RecalculateAll(HideMode mode)
    {
        foreach (var viewer in _core.PlayerManager.GetAllPlayers())
        {
            InitPlayer(viewer.PlayerID);
            RecalculateForViewer(viewer, mode);
        }
    }

    public void RecalculateForViewer(IPlayer viewer, HideMode mode)
    {
        if (!viewer.IsValid)
            return;

        if (!_activeBlocks.TryGetValue(viewer.PlayerID, out var currentBlocks))
        {
            currentBlocks = [];
            _activeBlocks[viewer.PlayerID] = currentBlocks;
        }

        var desiredBlocks = ComputeDesiredBlocks(viewer, mode);

        foreach (var entityId in currentBlocks)
        {
            if (!desiredBlocks.Contains(entityId))
                viewer.ShouldBlockTransmitEntity(entityId, false);
        }

        foreach (var entityId in desiredBlocks)
        {
            if (!currentBlocks.Contains(entityId))
                viewer.ShouldBlockTransmitEntity(entityId, true);
        }

        currentBlocks.Clear();
        currentBlocks.UnionWith(desiredBlocks);
    }

    public void ClearAll()
    {
        foreach (var playerId in _activeBlocks.Keys)
            ClearBlocksForViewer(playerId);

        _hideEnabled.Clear();
        _activeBlocks.Clear();
    }

    private HashSet<int> ComputeDesiredBlocks(IPlayer viewer, HideMode mode)
    {
        var blocks = new HashSet<int>();

        if (!ShouldApplyHiding(viewer))
            return blocks;

        if (!TryGetAliveTeam(viewer, out var viewerTeam))
            return blocks;

        foreach (var target in _core.PlayerManager.GetAllPlayers())
        {
            if (target.PlayerID == viewer.PlayerID)
                continue;

            if (!TryGetAliveTeam(target, out var targetTeam))
                continue;

            if (!MatchesMode(mode, viewerTeam, targetTeam))
                continue;

            var pawn = target.PlayerPawn;
            if (pawn is null || !pawn.IsValid)
                continue;

            blocks.Add((int)pawn.Index);
        }

        return blocks;
    }

    private bool ShouldApplyHiding(IPlayer viewer)
    {
        if (!TryGetAliveTeam(viewer, out _))
            return false;

        return _hideEnabled.TryGetValue(viewer.PlayerID, out var enabled) && enabled;
    }

    private static bool TryGetAliveTeam(IPlayer player, out Team team)
    {
        team = default;

        if (!player.IsValid)
            return false;

        var controller = player.Controller;
        if (controller is null || !controller.IsValid || !controller.PawnIsAlive)
            return false;

        team = controller.Team;
        return team is Team.T or Team.CT;
    }

    private static bool MatchesMode(HideMode mode, Team viewerTeam, Team targetTeam)
    {
        return mode switch
        {
            HideMode.All => true,
            HideMode.Team => viewerTeam == targetTeam,
            HideMode.Enemy => viewerTeam != targetTeam,
            _ => true
        };
    }

    private void ClearBlocksForViewer(int playerId)
    {
        if (!_activeBlocks.TryGetValue(playerId, out var blocks) || blocks.Count == 0)
            return;

        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player is null || !player.IsValid)
        {
            blocks.Clear();
            return;
        }

        foreach (var entityId in blocks)
            player.ShouldBlockTransmitEntity(entityId, false);

        blocks.Clear();
    }
}

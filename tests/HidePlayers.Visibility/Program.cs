using System.Reflection;
using HidePlayers;
using HidePlayers.Contract;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

var checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var preferences = new HidePreferences(HideMode.All, true);
var viewer = new ViewerView(true, true, Team.T, 1, new(10, null, null), new(10, null, null), default, null);
var target = new TargetView(20, 20, Team.T, LifeState_t.LIFE_ALIVE, false, 2, 2, 2);
var corpse = target with { LifeState = LifeState_t.LIFE_DEAD };
Check(!VisibilityPolicy.ShouldHide(preferences, default, corpse),
    "A viewer with no controller/pawn must fail open, including corpse hiding.");

foreach (var mode in Enum.GetValues<HideMode>())
{
    foreach (var hideDead in new[] { false, true })
    {
        var choice = new HidePreferences(mode, hideDead);
        Check(VisibilityPolicy.ShouldHide(choice, viewer, target) == (mode is HideMode.All or HideMode.Teammates),
            "Living teammate policy changed.");
        Check(VisibilityPolicy.ShouldHide(choice, viewer, target with { Team = Team.CT }) == (mode is HideMode.All or HideMode.Enemies),
            "Living enemy policy changed.");
        Check(VisibilityPolicy.ShouldHide(choice, viewer, corpse) == hideDead, "Corpse preference must be independent.");
        Check(VisibilityPolicy.ShouldHide(choice, viewer with { Alive = false }, corpse) == hideDead,
            "Stable spectators can still hide other corpses.");
        Check(!VisibilityPolicy.ShouldHide(choice, viewer with { Alive = false }, target), "Dead viewers see living players.");
        Check(!VisibilityPolicy.ShouldHide(choice, viewer with { Team = Team.Spectator }, target), "Spectators see living players.");
        Check(!VisibilityPolicy.ShouldHide(choice, viewer, target with { Team = Team.None }), "Unassigned targets remain visible.");
        Check(!VisibilityPolicy.ShouldHide(choice, viewer, target with { Controller = null }), "Unowned living pawns remain visible.");

        foreach (var state in new[] { LifeState_t.LIFE_DYING, LifeState_t.LIFE_RESPAWNABLE, LifeState_t.LIFE_RESPAWNING })
        {
            Check(!VisibilityPolicy.ShouldHide(choice, viewer, target with { LifeState = state }), "Transient state was hidden.");
        }
        Check(!VisibilityPolicy.ShouldHide(choice, viewer, corpse with { Respawning = true }), "Respawning corpse was hidden.");

        foreach (var protectedTarget in new[]
        {
            target with { Handle = 10 }, corpse with { Handle = 10 },
            target with { Controller = 1 }, corpse with { Controller = 1 },
            target with { OriginalController = 1 }, corpse with { OriginalController = 1 },
            target with { DefaultController = 1 }, corpse with { DefaultController = 1 }
        })
        {
            Check(!VisibilityPolicy.ShouldHide(choice, viewer, protectedTarget), "An own/current/former pawn was hidden.");
        }
    }
}

foreach (var reference in new[] { new PawnReferences(20, null, null), new(null, 20, null), new(null, null, 20) })
{
    foreach (var protectedViewer in new[]
    {
        viewer with { ControlledPawn = reference },
        viewer with { PlayerPawn = reference },
        viewer with { ObserverPawn = reference }
    })
    {
        Check(!VisibilityPolicy.ShouldHide(preferences, protectedViewer, target), "A pawn/camera dependency was hidden.");
        Check(!VisibilityPolicy.ShouldHide(preferences, protectedViewer, corpse), "A dead camera dependency was hidden.");
    }
}
Check(!VisibilityPolicy.ShouldHide(preferences, viewer with { DesiredObserverTarget = 20 }, corpse),
    "The pending observer target must remain visible.");

var player = DispatchProxy.Create<IPlayer, TransmitPlayer>();
var network = (TransmitPlayer)(object)player;
var visibility = new ViewerVisibility();
void Frame(ViewerView view, params TargetView[] targets) => visibility.Reconcile(player, preferences, view, targets);
void Reset()
{
    // The plugin releases owned live bits before clearing history at lifecycle boundaries.
    foreach (var index in visibility.Blocks.Keys)
    {
        network.Bits.Remove(index);
    }
    visibility.Clear();
    network.Writes.Clear();
}

Frame(viewer, target);
Check(!network.Bits.Contains(20), "A new pawn/connection needs one visible frame.");
Frame(viewer, target);
Check(network.Bits.Contains(20), "Stable living targets must be hidden.");
var writes = network.Writes.Count;
Frame(viewer, target);
Check(network.Writes.Count == writes, "An unchanged block should not be rewritten.");
network.Bits.Clear();
Frame(viewer, target);
Check(network.Bits.Contains(20), "A cleared native bit must be repaired even with an unchanged pawn handle.");

// Death can finish after player_death without another event. Every frame must reevaluate it.
Frame(viewer, target with { LifeState = LifeState_t.LIFE_DYING });
Check(!network.Bits.Contains(20), "Death transition must reveal an already hidden pawn.");
Frame(viewer, corpse);
Check(!network.Bits.Contains(20), "A newly dead pawn must settle first.");
Frame(viewer, corpse);
Check(network.Bits.Contains(20), "A settled corpse must become hidden without another event.");
Frame(viewer, corpse with { Respawning = true });
Check(!network.Bits.Contains(20), "Same-handle respawn must clear a corpse block immediately.");
Frame(viewer, target);
Check(!network.Bits.Contains(20), "Respawned target needs a stable frame.");
Frame(viewer, target);
Check(network.Bits.Contains(20), "Hiding must recover after same-handle respawn.");

Frame(viewer with { ControlledPawn = new(20, null, null) }, target);
Check(!network.Bits.Contains(20), "An ownership switch must release the new local pawn in the same frame.");
Frame(viewer with { ControlledPawn = new(20, null, null) }, target);
Check(!network.Bits.Contains(20), "A stable controlled pawn must never be hidden.");
Frame(viewer, target);
Frame(viewer, target);
Frame(default, corpse);
Check(!network.Bits.Contains(20), "Loss of viewer state must release old blocks.");

Reset();
Frame(viewer, corpse);
Frame(viewer, corpse);
Frame(viewer with { ObserverPawn = new(30, 20, null) }, corpse);
Check(!network.Bits.Contains(20), "Changing spectator target must immediately release an existing corpse block.");
Reset();
Frame(viewer, target);
Frame(viewer, target);
Frame(viewer with { Team = Team.CT }, target);
Check(!network.Bits.Contains(20), "A team switch needs a visible frame.");
Frame(viewer with { Team = Team.CT }, target);
Check(network.Bits.Contains(20), "Hiding must recover after a team switch.");

// Engine deletion clears the old bit; a different serial at the same index is a new entity.
network.Bits.Clear();
var replacement = target with { Handle = 2020 };
Frame(viewer, replacement);
Check(!network.Bits.Contains(20), "A recycled index must not inherit the old decision.");
Frame(viewer, replacement);
Check(network.Bits.Contains(20) && visibility.Blocks[20] == 2020, "A replacement needs its own tracked block.");
network.Bits.Clear();
visibility.ForgetEntity(20);
Frame(viewer);
Check(visibility.Blocks.Count == 0, "Deleted entities must leave no ownership records.");

Reset();
network.Bits.Add(20); // Another plugin owns this bit.
Frame(viewer, target);
Frame(viewer, target);
Check(visibility.Blocks.Count == 0, "A pre-existing block must not be claimed.");
preferences = new HidePreferences(HideMode.Off, false);
Frame(viewer, target);
Check(network.Bits.Contains(20) && network.Writes.Count == 0, "Disabling HidePlayers must preserve foreign blocks.");
network.Bits.Clear();
preferences = new HidePreferences(HideMode.All, true);
Frame(viewer, target);
Check(network.Bits.Contains(20) && visibility.Blocks.Count == 1, "A foreign block cleared later can be acquired.");
network.Bits.Clear(); // Deleted, then another plugin blocks a replacement in the same index.
network.Bits.Add(20);
Frame(viewer, replacement);
Check(network.Bits.Contains(20) && visibility.Blocks.Count == 0, "Stale ownership must not clear a replacement's foreign block.");
network.Bits.Clear();

Reset();
Frame(viewer, target);
Frame(viewer, target);
preferences = new HidePreferences(HideMode.Off, false);
Frame(viewer, target);
Check(!network.Bits.Contains(20), "Preference changes must take effect without a lifecycle event.");
preferences = new HidePreferences(HideMode.All, true);
Frame(viewer, target);
Reset(); // Round/map/hot reload starts with no history and no owned bits.
Frame(viewer, target);
Check(!network.Bits.Contains(20), "Lifecycle reset must require a new stable frame.");
Frame(viewer, target);
Check(network.Bits.Contains(20), "Hiding must resume after lifecycle reset.");

Console.WriteLine($"{checks} visibility safety checks passed.");

public class TransmitPlayer : DispatchProxy
{
    public HashSet<int> Bits { get; } = [];
    public List<(int Index, bool Block)> Writes { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IPlayer.IsTransmitEntityBlocked))
        {
            return Bits.Contains((int)args![0]!);
        }
        if (targetMethod?.Name == nameof(IPlayer.ShouldBlockTransmitEntity))
        {
            var index = (int)args![0]!;
            var block = (bool)args[1]!;
            Writes.Add((index, block));
            if (block)
            {
                Bits.Add(index);
            }
            else
            {
                Bits.Remove(index);
            }
            return null;
        }
        throw new NotSupportedException(targetMethod?.Name);
    }
}

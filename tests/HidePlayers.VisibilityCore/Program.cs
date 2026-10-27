using HidePlayers;
using HidePlayers.Contract;
using SwiftlyS2.Shared.Players;

var checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

// Deletions can arrive before OnMapUnload, after the engine clock is gone.
var engine = new TestEngine();
var visibility = new VisibilityCore(engine);
visibility.AddViewer(0);
visibility.Recalculate();
Check(engine.Blocked.SetEquals([20, 30]), "Living viewers should hide other players and corpses.");
engine.ClockAvailable = false;
visibility.OnEntityDeleted(10);
Check(engine.Blocked.Count == 0, "Losing the camera must release all owned blocks immediately, without reading game time.");
visibility.Stop();
visibility.Recalculate();
Check(engine.Blocked.Count == 0, "Queued recalculation after map unload must not read game time or restore blocks.");
visibility.OnEntityDeleted(20);

// During play, camera deletion must still give the viewer a full settling window.
engine = new TestEngine();
visibility = new VisibilityCore(engine);
visibility.AddViewer(0);
visibility.Recalculate();
engine.ClockAvailable = false;
visibility.OnEntityDeleted(10);
engine.Alive = false;
engine.ClockAvailable = true;
engine.Time = 101f;
visibility.Recalculate();
Check(engine.Blocked.Count == 0, "A new spectator must see living players and old corpses during camera settling.");
Check(engine.WakeAt == 107.5f, "Camera settling must start at the first recalculation after release.");
engine.Time = 107.4f;
visibility.Recalculate();
Check(engine.Blocked.Count == 0, "Repeated recalculation must preserve the settling deadline.");
engine.Time = 107.5f;
visibility.Recalculate();
Check(engine.Blocked.SetEquals([30]), "After settling, only the old corpse should be hidden.");

// Death/team callbacks can also release the viewer during teardown.
engine.ClockAvailable = false;
visibility.ReleaseViewer(0);
visibility.ReleaseViewer(0);
Check(engine.Blocked.Count == 0, "Repeated releases must be safe without an engine clock.");
visibility.Stop();
engine.Time = 1f;
engine.ClockAvailable = true;
engine.Alive = true;
visibility.Start();
visibility.Recalculate();
Check(engine.Blocked.SetEquals([20, 30]), "Hiding must resume after map load.");

Console.WriteLine($"Passed {checks} visibility lifecycle checks.");

internal sealed class TestEngine : IVisibilityEngine
{
    public bool ClockAvailable { get; set; } = true;
    public bool Alive { get; set; } = true;
    public float Time { get; set; } = 100f;
    public float Now => ClockAvailable ? Time : throw new InvalidOperationException("GlobalVars is null.");
    public float DeathCameraSeconds => 4.1f;
    public float WakeAt { get; private set; }
    public HashSet<int> Blocked { get; } = [];

    public void ReadPawns(List<PawnInfo> pawns)
    {
        pawns.Clear();
        pawns.Add(new(20, Team.T, true, 0f, 2, 2, 2));
        pawns.Add(new(30, Team.T, false, 0f, 3, 3, 3));
    }

    public bool TryReadViewer(int slot, HashSet<int> camera, out ViewerInfo viewer)
    {
        camera.Clear();
        camera.Add(Alive ? 10 : 11);
        viewer = new(new HidePreferences(HideMode.All, true), Alive, Team.T, 1, 0f);
        return true;
    }

    public void ReadAttachments(int entity, List<int> attachments) { }
    public bool IsBlocked(int slot, int entity) => Blocked.Contains(entity);

    public void SetBlocked(int slot, int entity, bool blocked)
    {
        if (blocked)
        {
            Blocked.Add(entity);
        }
        else
        {
            Blocked.Remove(entity);
        }
    }

    public void RecalculateAtEndOfFrame() { }
    public void RecalculateAt(float time) => WakeAt = time;
}

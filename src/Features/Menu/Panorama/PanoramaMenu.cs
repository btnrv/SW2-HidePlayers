using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    private const string PanoramaTextVariable = "text";

    // The layout entity belongs to the map. A handle shows when it is gone, and the plugin never touches it then.
    private CHandle<CCSCustomHudLayout> _hud = CHandle<CCSCustomHudLayout>.Invalid;

    // Slots that have the menu open, with the session that opened it.
    private readonly Dictionary<int, ulong> _panoramaOpen = [];

    private CCSCustomHudLayout? ResolveHud() => _hud.IsValid ? _hud.Value : null;

    private CCSCustomHudLayout EnsureHud()
    {
        if (ResolveHud() is { } existing)
        {
            return existing;
        }

        var hud = Core.EntitySystem.CreateEntity<CCSCustomHudLayout>();
        hud.Observable = false;
        hud.ObservableUpdated();
        hud.StrLayout = Settings.PanoramaLayout;
        hud.StrLayoutUpdated();
        hud.DispatchSpawn();
        _hud = Core.EntitySystem.GetRefEHandle(hud);
        return hud;
    }

    private void OpenPanoramaMenu(IPlayer player)
    {
        if (GetSession(player) is not { } session)
        {
            return;
        }

        Core.Menu.CloseFor(player);
        var hud = EnsureHud();
        _panoramaOpen[player.PlayerID] = session.SessionId;
        RefreshPanoramaMenu(player);
        SetPanoramaClass(hud, player.PlayerID, PanoramaIds.RootPanel, PanoramaIds.DismissedClass, false);
        hud.SetInputCaptureEnabledForPlayer(player.PlayerID, true);
    }

    private void RefreshPanoramaMenu(IPlayer player)
    {
        var slot = player.PlayerID;
        if (!_panoramaOpen.TryGetValue(slot, out var sessionId)
            || GetSession(player) is not { } session
            || session.SessionId != sessionId
            || ResolveHud() is not { } hud)
        {
            return;
        }

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var preferences = session.Preferences;
        SetPanoramaText(hud, slot, PanoramaIds.TitlePanel, localizer["menu.title"]);
        SetPanoramaText(hud, slot, PanoramaIds.OverridePanel, localizer["menu.override"]);
        SetPanoramaClass(hud, slot, PanoramaIds.OverridePanel, PanoramaIds.DismissedClass, preferences == session.EffectivePreferences);
        SetPanoramaText(hud, slot, PanoramaIds.DeadPanel, localizer["menu.hide_dead"]);
        SetPanoramaText(hud, slot, PanoramaIds.ClosePanel, localizer["menu.close"]);
        foreach (var button in PanoramaIds.Buttons)
        {
            if (button.Mode is { } mode)
            {
                SetPanoramaText(hud, slot, button.Id + "_label", localizer[$"menu.mode.{mode}"]);
                SetPanoramaClass(hud, slot, button.Id, PanoramaIds.SelectedClass, preferences.Mode == mode);
            }
            else if (button.HideDead is { } hideDead)
            {
                SetPanoramaText(hud, slot, button.Id + "_label", localizer[hideDead ? "status.on" : "status.off"]);
                SetPanoramaClass(hud, slot, button.Id, PanoramaIds.SelectedClass, preferences.HideDead == hideDead);
            }
        }
    }

    private static void SetPanoramaText(CCSCustomHudLayout hud, int slot, string panel, string text) =>
        hud.SetDialogVariableStringForPlayer(slot, panel, PanoramaTextVariable, text);

    private static void SetPanoramaClass(CCSCustomHudLayout hud, int slot, string panel, string className, bool present) =>
        hud.SetHasClassForPlayer(slot, panel, className, present
            ? EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass
            : EHudPanelClassStatus_t.k_eHudPanelClassStatus_DoesNotHaveClass);

    [EventListener<EventDelegates.OnCustomHudClicked>]
    private void OnPanoramaClicked(IOnCustomHudClickedEvent @event)
    {
        if (!_active
            || !_hud.IsValid
            || Core.EntitySystem.GetRefEHandle(@event.CustomHudLayout).Raw != _hud.Raw
            || !_panoramaOpen.TryGetValue(@event.PlayerId, out var sessionId)
            || Core.PlayerManager.GetPlayer(@event.PlayerId) is not { } player
            || player.SessionId != sessionId
            || GetSession(player) is not { } session
            || !PanoramaIds.TryGetButton(@event.ButtonId, out var button))
        {
            return;
        }

        if (button.Close)
        {
            ClosePanoramaMenu(player.PlayerID);
            return;
        }

        ChangePreferences(player, session.Preferences with
        {
            Mode = button.Mode ?? session.Preferences.Mode,
            HideDead = button.HideDead ?? session.Preferences.HideDead
        });
    }

    private void ClosePanoramaMenu(int slot)
    {
        if (!_panoramaOpen.Remove(slot) || ResolveHud() is not { } hud)
        {
            return;
        }

        hud.SetInputCaptureEnabledForPlayer(slot, false);
        SetPanoramaClass(hud, slot, PanoramaIds.RootPanel, PanoramaIds.DismissedClass, true);
    }

    private void ClosePanoramaMenus()
    {
        foreach (var slot in _panoramaOpen.Keys.ToArray())
        {
            ClosePanoramaMenu(slot);
        }
    }

    // On map unload the engine deletes the entity, so only the plugin's records are dropped.
    private void ForgetPanoramaEntity()
    {
        _panoramaOpen.Clear();
        _hud = CHandle<CCSCustomHudLayout>.Invalid;
    }

    private void ReleasePanorama()
    {
        ClosePanoramaMenus();
        ResolveHud()?.Despawn();
        _hud = CHandle<CCSCustomHudLayout>.Invalid;
    }
}

using HidePlayers.Configuration;
using HidePlayers.Contract;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HidePlayers.Runtime;

public sealed partial class HidePlayersRuntime
{
    private CCSCustomHudLayout? _panoramaHud;
    private readonly Dictionary<int, ulong> _panoramaSessions = [];

    private void OpenPanoramaMenu(IPlayer player)
    {
        if (_sessions.Get(player.PlayerID) is not { } session)
            return;
        CloseMenu(player.PlayerID);
        _core.MenusAPI.CloseActiveMenu(player);
        if (_panoramaHud is null)
        {
            _panoramaHud = _core.EntitySystem.CreateEntity<CCSCustomHudLayout>();
            _panoramaHud.Observable = false;
            _panoramaHud.ObservableUpdated();
            _panoramaHud.StrLayout = _settings.Current.PanoramaLayout;
            _panoramaHud.StrLayoutUpdated();
            _panoramaHud.DispatchSpawn();
        }
        _panoramaSessions[player.PlayerID] = session.SessionId;
        RefreshPanoramaMenu(player);
        SetPanoramaClass(player.PlayerID, PanoramaMenu.RootPanel, "Dismissed", false);
        _panoramaHud.SetInputCaptureEnabledForPlayer(player.PlayerID, true);
    }

    private void RefreshPanoramaMenu(IPlayer player)
    {
        if (!_panoramaSessions.TryGetValue(player.PlayerID, out var sessionId)
            || ResolveMenuSession(player.PlayerID, sessionId) is null)
            return;
        var session = _sessions.Get(player.PlayerID)!;
        var slot = player.PlayerID;
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        _panoramaHud!.SetDialogVariableStringForPlayer(slot, PanoramaMenu.TitlePanel, "text", localizer["menu.title"]);
        _panoramaHud.SetDialogVariableStringForPlayer(slot, PanoramaMenu.OverridePanel, "text", localizer["menu.override"]);
        SetPanoramaClass(slot, PanoramaMenu.OverridePanel, "Dismissed", session.Preferences == session.EffectivePreferences);
        _panoramaHud.SetDialogVariableStringForPlayer(slot, PanoramaMenu.DeadPanel, "text", localizer["menu.hide_dead"]);
        _panoramaHud.SetDialogVariableStringForPlayer(slot, PanoramaMenu.ClosePanel, "text", localizer["menu.close"]);
        SetMode("hideplayers_off", HideMode.Off);
        SetMode("hideplayers_all", HideMode.All);
        SetMode("hideplayers_team", HideMode.Teammates);
        SetMode("hideplayers_enemy", HideMode.Enemies);
        _panoramaHud.SetDialogVariableStringForPlayer(slot, "hideplayers_dead_on_label", "text", localizer["status.on"]);
        _panoramaHud.SetDialogVariableStringForPlayer(slot, "hideplayers_dead_off_label", "text", localizer["status.off"]);
        SetPanoramaClass(slot, "hideplayers_dead_on", "Selected", session.Preferences.HideDead);
        SetPanoramaClass(slot, "hideplayers_dead_off", "Selected", !session.Preferences.HideDead);

        void SetMode(string button, HideMode mode)
        {
            _panoramaHud.SetDialogVariableStringForPlayer(slot, button + "_label", "text", localizer[$"menu.mode.{mode}"]);
            SetPanoramaClass(slot, button, "Selected", session.Preferences.Mode == mode);
        }
    }

    private void SetPanoramaClass(int slot, string panel, string className, bool present) =>
        _panoramaHud!.SetHasClassForPlayer(slot, panel, className, present
            ? EHudPanelClassStatus_t.k_eHudPanelClassStatus_HasClass
            : EHudPanelClassStatus_t.k_eHudPanelClassStatus_DoesNotHaveClass);

    private void OnPanoramaClicked(IOnCustomHudClickedEvent @event)
    {
        if (!_running || _panoramaHud is null
            || _core.EntitySystem.GetRefEHandle(@event.CustomHudLayout).Raw != _core.EntitySystem.GetRefEHandle(_panoramaHud).Raw
            || !_panoramaSessions.TryGetValue(@event.PlayerId, out var sessionId)
            || ResolveMenuSession(@event.PlayerId, sessionId) is not { } player)
            return;
        if (@event.ButtonId == "hideplayers_close")
        {
            ClosePanoramaMenu(@event.PlayerId);
            return;
        }
        if (PanoramaMenu.CommandArguments(@event.ButtonId) is { } arguments)
            player.ExecuteCommand("sw_" + _settings.Current.MenuCommand.Name + " " + arguments);
    }

    private void ClosePanoramaMenu(int slot)
    {
        if (!_panoramaSessions.Remove(slot))
            return;
        SetPanoramaClass(slot, PanoramaMenu.RootPanel, "Dismissed", true);
        _panoramaHud!.SetInputCaptureEnabledForPlayer(slot, false);
    }

    private void ClosePanoramaMenus()
    {
        foreach (var slot in _panoramaSessions.Keys.ToArray())
            ClosePanoramaMenu(slot);
    }
}

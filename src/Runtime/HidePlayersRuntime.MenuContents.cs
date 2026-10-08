using HidePlayers.Contract;
using HidePlayers.State;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace HidePlayers.Runtime;

public sealed partial class HidePlayersRuntime
{
    private sealed class PersonalMenu(IMenuAPI menu, Dictionary<HideMode, ButtonMenuOption> modes, ToggleMenuOption hideDead) : IDisposable
    {
        public IMenuAPI Menu { get; } = menu;
        public Dictionary<HideMode, ButtonMenuOption> Modes { get; } = modes;
        public ToggleMenuOption HideDead { get; } = hideDead;
        public bool Refreshing { get; set; }

        public void Dispose()
        {
            // MenuAPI currently clears options without disposing their text animation timers.
            foreach (var option in Modes.Values)
                option.Dispose();
            HideDead.Dispose();
            Menu.Dispose();
        }
    }

    private PersonalMenu CreatePersonalMenu(IPlayer player, PlayerSession session)
    {
        var slot = player.PlayerID;
        var sessionId = session.SessionId;
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        var builder = _core.MenusAPI.CreateBuilder();
        var modes = new Dictionary<HideMode, ButtonMenuOption>();
        foreach (var mode in Enum.GetValues<HideMode>())
        {
            var button = new ButtonMenuOption(localizer[$"menu.mode.{mode}"]);
            button.Click += (_, _) => new ValueTask(_core.Scheduler.NextTickAsync(() =>
            {
                if (ResolveMenuSession(slot, sessionId) is not { } current)
                    return;
                var preferences = _sessions.Get(slot)!.Preferences;
                ChangePreferences(current, preferences with { Mode = mode });
            }));
            modes.Add(mode, button);
            builder.AddOption(button);
        }
        var hideDead = new ToggleMenuOption(localizer["menu.hide_dead"], session.Preferences.HideDead);
        builder.AddOption(hideDead);
        var owned = new PersonalMenu(builder.Build(), modes, hideDead);
        hideDead.ValueChanged += (_, args) =>
        {
            if (owned.Refreshing)
                return;
            var value = args.NewValue;
            _core.Scheduler.NextTick(() =>
            {
                if (ResolveMenuSession(slot, sessionId) is not { } current)
                    return;
                var preferences = _sessions.Get(slot)!.Preferences;
                ChangePreferences(current, preferences with { HideDead = value });
            });
        };
        return owned;
    }

    private void RefreshMenuContents(IPlayer player, PersonalMenu owned)
    {
        if (_sessions.Get(player.PlayerID) is not { } session)
            return;
        var localizer = _core.Translation.GetPlayerLocalizer(player);
        owned.Menu.Configuration.Title = localizer["menu.title"]
            + (session.Preferences != session.EffectivePreferences ? " — " + localizer["menu.override"] : "");
        foreach (var (mode, button) in owned.Modes)
            button.Text = (mode == session.Preferences.Mode ? "✔ " : "") + localizer[$"menu.mode.{mode}"];
        owned.HideDead.Text = localizer["menu.hide_dead"];
        owned.Refreshing = true;
        try
        {
            owned.HideDead.SetToggleState(player, session.Preferences.HideDead);
        }
        finally
        {
            owned.Refreshing = false;
        }
    }
}

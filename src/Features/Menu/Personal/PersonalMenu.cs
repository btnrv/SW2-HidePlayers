using HidePlayers.Contract;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    // Server owners tune keys, renderer and input method of this menu in menus.jsonc under this id.
    private const string FrameworkMenuId = "hideplayers.menu";

    private IMenu? _frameworkMenu;

    private void OpenFrameworkMenu(IPlayer player)
    {
        ClosePanoramaMenu(player.PlayerID);
        _frameworkMenu ??= BuildFrameworkMenu();
        _frameworkMenu.Open(player);
    }

    // One menu serves every player. Texts and visibility are computed per session, so no player data lives here.
    private IMenu BuildFrameworkMenu()
    {
        var builder = Core.Menu.CreateMenu(FrameworkMenuId)
            .AddHeader(new TitleComponent { TextProvider = session => Localize(session)["menu.title"] })
            .AddHeader(new TextComponent
            {
                TextProvider = session => Localize(session)["menu.override"],
                VisibleWhen = HasOverride
            });
        foreach (var mode in Enum.GetValues<HideMode>())
        {
            builder.Add(new ButtonComponent(onSelect: context => SelectMode(context, mode))
            {
                TextProvider = session => ModeText(session, mode)
            });
        }

        builder.Add(new ButtonComponent(onSelect: ToggleHideDead)
        {
            TextProvider = HideDeadText
        });
        return builder.Build();
    }

    private void DisposeFrameworkMenu()
    {
        _frameworkMenu?.Dispose();
        _frameworkMenu = null;
    }

    private ILocalizer Localize(IMenuSession session) => Core.Translation.GetPlayerLocalizer(session.Player);

    private bool HasOverride(IMenuSession session) =>
        GetSession(session.Player) is { } current && current.Preferences != current.EffectivePreferences;

    private string ModeText(IMenuSession session, HideMode mode)
    {
        var localizer = Localize(session);
        var text = localizer[$"menu.mode.{mode}"];
        return GetSession(session.Player) is { } current && current.Preferences.Mode == mode
            ? localizer["menu.selected", text]
            : text;
    }

    private string HideDeadText(IMenuSession session)
    {
        var localizer = Localize(session);
        var hideDead = GetSession(session.Player)?.Preferences.HideDead ?? false;
        return localizer["menu.hide_dead_state", localizer["menu.hide_dead"], localizer[hideDead ? "status.on" : "status.off"]];
    }

    // Menu callbacks run on the game thread, so they apply the change directly.
    private ValueTask SelectMode(MenuActionContext context, HideMode mode)
    {
        if (GetSession(context.Player) is { } session)
        {
            ChangePreferences(context.Player, session.Preferences with { Mode = mode });
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask ToggleHideDead(MenuActionContext context)
    {
        if (GetSession(context.Player) is { } session)
        {
            ChangePreferences(context.Player, session.Preferences with { HideDead = !session.Preferences.HideDead });
        }

        return ValueTask.CompletedTask;
    }
}

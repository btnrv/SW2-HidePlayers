using HidePlayers.Contract;
using SwiftlyS2.Shared.Commands;

namespace HidePlayers;

public sealed partial class HidePlayers
{
    private const string ConsolePrefix = "sw_";

    // The command name and aliases come from the settings, so they are registered here and not with [Command].
    private void RegisterCommands()
    {
        var command = Settings.MenuCommand;
        Core.Command.RegisterCommand(
            command.Name, OnHideCommand, registerRaw: false, permission: command.Permission,
            helpText: "Hide other players and corpses.");
        foreach (var alias in command.Aliases)
        {
            Core.Command.RegisterCommandAlias(command.Name, alias, registerRaw: false);
        }
    }

    // CommandContext.Reply in SwiftlyS2 makes the same test: the sw_ prefix means the console, anything else means
    // chat. That holds for commands registered with registerRaw: false, as here.
    private static bool IsChatInvocation(ICommandContext context) => context.Prefix != ConsolePrefix;

    private void OnHideCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is not { IsFakeClient: false } player || EnsureSession(player) is not { } session)
        {
            context.Reply(Settings.Prefix + Core.Localizer["command.player_only"]);
            return;
        }

        if (context.Args.Length == 0 && IsChatInvocation(context))
        {
            OpenMenu(player);
            return;
        }

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        HidePreferences selected;
        if (context.Args.Length == 0)
        {
            selected = session.ToggledPreferences;
        }
        else if (!HideCommandParser.TryParse(context.Args, session.Preferences, out selected))
        {
            context.Reply(Settings.Prefix + localizer["command.usage", context.Prefix, context.CommandName]);
            return;
        }

        ChangePreferences(player, selected);
        var status = localizer[selected.HideDead ? "status.on" : "status.off"];
        context.Reply(Settings.Prefix + localizer["command.applied", localizer[$"menu.mode.{selected.Mode}"], status]);
    }
}

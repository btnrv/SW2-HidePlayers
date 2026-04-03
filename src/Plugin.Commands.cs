using SwiftlyS2.Shared.Commands;

namespace HidePlayers;

public sealed partial class Plugin
{
    private void RegisterCommands()
    {
        UnregisterCommands();

        foreach (var alias in _config.Commands)
        {
            if (string.IsNullOrWhiteSpace(alias))
                continue;

            var guid = Core.Command.RegisterCommand(
                alias.Trim(),
                OnToggleCommand,
                registerRaw: true,
                permission: _config.CommandPermission
            );
            _commandGuids.Add(guid);
        }

        if (!string.IsNullOrWhiteSpace(_config.ReloadCommand))
        {
            var guid = Core.Command.RegisterCommand(
                _config.ReloadCommand.Trim(),
                OnReloadCommand,
                registerRaw: true,
                permission: _config.ReloadPermission
            );
            _commandGuids.Add(guid);
        }
    }

    private void UnregisterCommands()
    {
        foreach (var guid in _commandGuids)
            Core.Command.UnregisterCommand(guid);

        _commandGuids.Clear();
    }

    private void OnToggleCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
            return;

        var player = context.Sender;
        var enabled = _visibility.Toggle(player.PlayerID);
        _visibility.RecalculateForViewer(player, _mode);

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var status = localizer[enabled ? "status.enabled" : "status.disabled"];
        context.Reply(localizer["command.toggle", _config.Prefix, status]);
    }

    private void OnReloadCommand(ICommandContext context)
    {
        try
        {
            ReloadConfigValues();
            RegisterCommands();
            _visibility.RecalculateAll(_mode);

            var message = GetLocalizedReply(context, "reload.success");
            context.Reply(message);
        }
        catch
        {
            var message = GetLocalizedReply(context, "reload.failed");
            context.Reply(message);
        }
    }

    private string GetLocalizedReply(ICommandContext context, string key)
    {
        if (context.IsSentByPlayer && context.Sender is not null)
            return Core.Translation.GetPlayerLocalizer(context.Sender)[key, _config.Prefix];

        return Core.Localizer[key, _config.Prefix];
    }
}

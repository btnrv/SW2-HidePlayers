using HidePlayers.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace HidePlayers;

[PluginMetadata(Id = "HidePlayers", Version = "2.0.0", Name = "HidePlayers", Author = "gyro",
    Description = "Personal player and corpse visibility preferences.")]
public sealed partial class HidePlayers : BasePlugin
{
    private readonly HidePlayersApi _api;
    private readonly VisibilityCore _visibility;
    private ServiceProvider? _services;
    private IDisposable? _settingsSubscription;
    private volatile PluginSettings _settings = new();
    private bool _active;

    public HidePlayers(ISwiftlyCore core) : base(core)
    {
        _api = new HidePlayersApi(this);
        _visibility = new VisibilityCore(this);
    }

    /// <summary>The latest valid settings. An invalid edit of settings.jsonc keeps the previous values.</summary>
    internal PluginSettings Settings => _settings;

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeJsonWithModel<PluginSettings>("settings.jsonc", "Main", addMissingKeys: true)
            .Configure(builder => builder.AddJsonFile(
                Core.Configuration.GetConfigPath("settings.jsonc"), optional: false, reloadOnChange: true));

        var services = new ServiceCollection();
        services.AddSwiftly(Core);
        services.AddOptions<PluginSettings>().BindConfiguration("Main");
        services.AddSingleton<IValidateOptions<PluginSettings>, PluginSettingsValidator>();
        _services = services.BuildServiceProvider();

        // Reading CurrentValue throws for an invalid file, which fails the load early. After that, OnChange only
        // fires for valid files, so Settings never exposes a half-valid edit.
        var options = _services.GetRequiredService<IOptionsMonitor<PluginSettings>>();
        _settings = options.CurrentValue;
        _settingsSubscription = options.OnChange(changed =>
        {
            changed.Prefix = _settings.Prefix;
            _settings = changed;
        });

        _active = true;
        RegisterCommands();
        HookVisibilityEvents();
        SeedSessions();
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IHidePlayersApi, HidePlayersApi>(IHidePlayersApi.InterfaceKey, _api);
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        UseCookies(interfaceManager);
    }

    public override void OnSharedInterfaceInjected(IInterfaceManager interfaceManager)
    {
        BeginPreferenceLoads();
    }

    public override void Unload()
    {
        _active = false;
        try
        {
            _visibility.Stop();
            DisposeFrameworkMenu();
        }
        finally
        {
            ClearPlayerState();
            _menuProvider = null;
            _store = null;
            _settingsSubscription?.Dispose();
            _settingsSubscription = null;
            _services?.Dispose();
            _services = null;
        }
    }
}

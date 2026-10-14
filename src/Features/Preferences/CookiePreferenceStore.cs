using System.Runtime.CompilerServices;
using Cookies.Contract;
using SwiftlyS2.Shared;

namespace HidePlayers;

/// <summary>
/// The only type that uses the Cookies contract. It is created after the interface is known to exist, so a server
/// without Cookies never loads the contract assembly. Members stay out of line for the same reason.
/// </summary>
internal sealed class CookiePreferenceStore
{
    public const string InterfaceKey = "Cookies.Player.v1";
    private const string CookieKey = "hideplayers.preferences";

    private readonly IPlayerCookiesAPIv1 _cookies;

    private CookiePreferenceStore(IPlayerCookiesAPIv1 cookies)
    {
        _cookies = cookies;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static CookiePreferenceStore? Resolve(IInterfaceManager interfaceManager, CookiePreferenceStore? current)
    {
        if (!interfaceManager.TryGetSharedInterface<IPlayerCookiesAPIv1>(InterfaceKey, out var api))
        {
            return null;
        }

        return current is not null && ReferenceEquals(current._cookies, api) ? current : new CookiePreferenceStore(api);
    }

    /// <summary>
    /// Reads the stored value, or null when the player has none. Cookies answers an uncached player with a synchronous
    /// database query, so call this off the game thread. Has comes first because Get throws for a missing key.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string? TryRead(long steamId) => _cookies.Has(steamId, CookieKey) ? _cookies.Get<string>(steamId, CookieKey) : null;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Write(long steamId, string value) => _cookies.Set(steamId, CookieKey, value);
}

using System.Linq;
using RepoSteamNetworking.Networking.Data;
using BepInEx.Unity.IL2CPP;

namespace RepoSteamNetworking.Networking.Registries;

internal static class ModNetworkGuidRegistry
{
    public static ModNetworkGuidPalette Palette { get; private set; } = new();
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized)
            return;

        var modGuids = IL2CPPChainloader.Instance.Plugins.Keys.ToArray();
        Palette = new ModNetworkGuidPalette(modGuids);
        
        _initialized = true;
    }
}
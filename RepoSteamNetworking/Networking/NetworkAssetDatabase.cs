using System.Collections.Generic;
using System.Linq;
using RepoSteamNetworking.API.Asset;
using RepoSteamNetworking.Utils;
using UnityEngine;

namespace RepoSteamNetworking.Networking;

internal static class NetworkAssetDatabase
{
    private static readonly Dictionary<AssetBundleReference, NetworkAssetBundle> AssetBundles = new();
    
    public static AssetBundleReference RegisterAssetBundle(AssetBundle assetBundle, string modGuid, string bundleName, bool managed)
    {
        AssetBundleReference bundleRef = (modGuid, bundleName);
        
        AssetBundles[bundleRef] = new NetworkAssetBundle
        {
            Bundle = assetBundle,
            Managed = managed
        };
        
        Logging.Info($"Registered Networked AssetBundle {bundleRef.ToString()}!");
        
        return bundleRef;
    }

    public static T LoadAsset<T>(PrefabReference prefabRef)
        where T : Object
    {
        throw new System.NotImplementedException();
    }

    public static AssetBundleRequest LoadAssetAsync<T>(PrefabReference prefabRef)
        where T : Object
    {
        throw new System.NotImplementedException();
    }

    public static IEnumerable<PrefabReference> GetAllAssets(AssetBundleReference bundleRef)
    {
        throw new System.NotImplementedException();
    }

    private struct NetworkAssetBundle
    {
        public AssetBundle Bundle { get; set; }
        public bool Managed { get; set; }
    }
}
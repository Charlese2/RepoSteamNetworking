using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using RepoSteamNetworking.API.Asset;
using RepoSteamNetworking.API.VersionCompat;
using RepoSteamNetworking.Networking;
using RepoSteamNetworking.Networking.Packets;
using RepoSteamNetworking.Networking.Registries;
using RepoSteamNetworking.Networking.Serialization;
using RepoSteamNetworking.Networking.Unity;
using RepoSteamNetworking.Utils;
using BepInEx.Unity.IL2CPP;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoSteamNetworking.API;

public static class RepoSteamNetwork
{
    private static readonly RPCMethodHelper RPCHelper = new();

    public static SteamId CurrentSteamId
    {
        get
        {
            if (!field.IsValid)
                field = SteamClient.SteamId;
            
            return field;
        }
    }
    
    /// <summary>
    /// Are you the server Host?
    /// </summary>
    public static bool IsServer => RepoNetworkingServer.Instance.ServerActive;
    
    /// <summary>
    /// Are you in SinglePlayer mode?
    /// </summary>
    public static bool IsSinglePlayer => !RepoNetworkingServer.Instance.ServerActive && !RepoNetworkingClient.Instance.ClientActive;
    
    /// <summary>
    /// Determines if you have host-like authority.
    /// </summary>
    public static bool HasHostAuthority => IsServer || IsSinglePlayer;

    internal static void OnHostReceivedMessage(byte[] data, Connection connection, NetIdentity identity)
    {
        var message = new SocketMessage(data);
        var header = message.ReadPacketHeader();
        var sender = identity.SteamId;
        
        var packet = NetworkPacketRegistry.CreatePacket(header.PacketId);
        packet.Header = header;

        if (IsServer && identity.IsSteamId && header.Sender != sender)
        {
            Logging.Warn($"{sender.GetLobbyName()} sent the incorrect SteamID {header.Sender} for their connection! Dropping connection...");
            if (RepoNetworkingServer.Instance.SocketManager!.TryGetSteamUserConnection(sender, out var userConnection))
            {
                userConnection.Close();
                return;
            }
            connection.Close();
            return;
        }
        
        if (IsSinglePlayer && header.Destination is NetworkDestination.HostOnly or NetworkDestination.Everyone || header.Destination == NetworkDestination.HostOnly)
        {
            packet.Deserialize(message);
            NetworkPacketRegistry.InvokeCallbacks(packet);
            return;
        }

        if (header.Destination == NetworkDestination.PacketTarget)
        {
            if (!header.Target.IsValid)
            {
                Logging.Warn("Invalid target specified for packet! Dropping packet...");
                return;
            }
            
            // Is the host the target of the packet?
            if (header.Target == CurrentSteamId)
            {
                packet.Deserialize(message);
                NetworkPacketRegistry.InvokeCallbacks(packet);
                return;
            }

            if (IsSinglePlayer)
            {
                Logging.Info("Target of packet is unavailable in SinglePlayer!");
                return;
            }
        }

        if (header.Destination == NetworkDestination.EveryoneExcludingSender)
        {
            RepoNetworkingServer.Instance.SendSocketMessageToClients(new SocketMessage(data), packet.Header.Sender);
            return;
        }

        RepoNetworkingServer.Instance.SendSocketMessageToClients(new SocketMessage(data));
    }
    
    internal static void OnClientMessageReceived(byte[] data)
    {
        var message = new SocketMessage(data);
        var header = message.ReadPacketHeader();

        var packet = NetworkPacketRegistry.CreatePacket(header.PacketId);
        packet.Header = header;

        // Don't process packets that are for clients only on the host.
        if (header.Destination == NetworkDestination.ClientsOnly && RepoNetworkingServer.Instance.ServerActive)
            return;
        
        packet.Deserialize(message);
        NetworkPacketRegistry.InvokeCallbacks(packet);
    }

    public static Steamworks.Data.Lobby GetCurrentLobby()
    {
        if (RepoNetworkingServer.Instance.ServerActive)
            return RepoNetworkingServer.Instance.CurrentLobby;

        return RepoNetworkingClient.Instance.CurrentLobby;
    }

    public static void RegisterPacket<TPacket>()
        where TPacket : NetworkPacket<TPacket>
    {
        NetworkPacketRegistry.RegisterPacket(typeof(TPacket));
    }

    public static void AddCallback<TPacket>(Action<TPacket> callback)
        where TPacket : NetworkPacket<TPacket>
    {
        var callbackHandler = MethodHandler.FromAction(callback);
        NetworkPacketRegistry.AddCallback<TPacket>(callbackHandler);
    }

    public static void RemoveCallback<TPacket>(Action<TPacket> callback)
        where TPacket : NetworkPacket<TPacket>
    {
        var callbackHandler = MethodHandler.FromAction(callback);
        NetworkPacketRegistry.RemoveCallback<TPacket>(callbackHandler);
    }

    public static void SetVersionCompatibility(string modGuid, SemanticVersioning.Version modVersion, VersionCompatibility compatibility, bool optional = false)
    {
        VersionCompatRegistry.RegisterMod(modGuid, modVersion, new RSNVersionCompatibilityAttribute(compatibility, optional));
    }

    public static void SetVersionCompatibility(VersionCompatibility compatibility, bool optional = false, BasePlugin? plugin = null)
    {
        BepInPlugin pluginInfo;
        
        if (plugin is null)
        {
            var assembly = Assembly.GetCallingAssembly();

            var plugins = assembly.GetLoadableTypes()
                .SelectMany(type => type.GetCustomAttributes<BepInPlugin>())
                .ToArray();

            if (plugins.Length != 1)
                throw new InvalidOperationException($"Couldn't determine BepInPlugin from calling assembly! please set {nameof(plugin)} parameter.");
            
            pluginInfo = plugins[0];
        }
        else
        {
            pluginInfo = plugin.GetType().GetCustomAttribute<BepInPlugin>();
        }

        VersionCompatRegistry.RegisterMod(pluginInfo.GUID, pluginInfo.Version, new RSNVersionCompatibilityAttribute(compatibility, optional));
    }

    public static void SendPackets<TPacket>(IEnumerable<TPacket> packets, NetworkDestination destination = NetworkDestination.Everyone)
        where TPacket : NetworkPacket
    {
        foreach (var packet in packets)
            SendPacket(packet, destination);
    }

    public static void SendPacket<TPacket>(TPacket packet, NetworkDestination destination = NetworkDestination.Everyone)
        where TPacket : NetworkPacket
    {
        packet.Header.Sender = CurrentSteamId;
        
        var message = packet.Serialize(destination);

        if (IsSinglePlayer)
        {
            OnHostReceivedMessage(message.GetBytes(), default, default);;
            return;
        }
        
        if (destination == NetworkDestination.HostOnly)
        {
            RepoNetworkingClient.Instance.SendSocketMessageToServer(message);
        }
        else if (IsServer)
        {
            if (destination == NetworkDestination.PacketTarget && packet.Header.Target.IsValid)
            {
                RepoNetworkingServer.Instance.SendSocketMessageToTarget(message, packet.Header.Target);
            }
            else if (destination == NetworkDestination.EveryoneExcludingSender && packet.Header.Sender.IsValid)
            {
                RepoNetworkingServer.Instance.SendSocketMessageToClients(message, packet.Header.Sender);
            }
            else
            {
                RepoNetworkingServer.Instance.SendSocketMessageToClients(message);
            }
        }
        else
        {
            RepoNetworkingClient.Instance.SendSocketMessageToServer(message);
        }
    }

    public static void CallRPC(RPCTarget target, uint networkId, string modGuid, uint subId, string methodName,
        params object[] parameters) => CallRPC((int)target, networkId, modGuid, subId, methodName, parameters);

    public static void CallRPC(int target, uint networkId, string modGuid, uint subId, string methodName, params object[] parameters)
    {
        var guidPaletteId = RepoSteamNetworkManager.Instance.GetGuidPaletteId(modGuid);
        
        var packet = new CallRPCPacket
        {
            NetworkId = networkId,
            GuidPaletteId = guidPaletteId,
            SubId = subId,
            MethodName = methodName,
            Parameters = parameters
        };

        var destination = (NetworkDestination)target;
        
        SendPacket(packet, destination);
    }

    internal static void InvokeRPC(uint networkId, uint guidPaletteId, uint subId, string methodName, params object[] parameters)
    {
        var networkIdentity = RepoSteamNetworkManager.Instance.GetNetworkIdentity(networkId);
        var modGuid = RepoSteamNetworkManager.Instance.GetModGuid(guidPaletteId);
        var subIdentity = networkIdentity.GetSubIdentity(modGuid, subId);
        
        RPCHelper.InvokeRPC(subIdentity, methodName, parameters);
    }
    
    public static void InstantiatePrefab(PrefabReference prefab) => InstantiatePrefab(prefab, Vector3.zero, Quaternion.identity);
    
    public static void InstantiatePrefab(PrefabReference prefab, Quaternion rotation) => InstantiatePrefab(prefab, Vector3.zero, rotation);

    public static void InstantiatePrefab(PrefabReference prefab, Vector3 position) => InstantiatePrefab(prefab, position, Quaternion.identity);
    
    public static void InstantiatePrefab(PrefabReference prefab, Vector3 position, Quaternion rotation) => InstantiatePrefab(prefab, null,  position, rotation);
    
    public static void InstantiatePrefab(PrefabReference prefab, Transform target) => InstantiatePrefab(prefab, target, Vector3.zero, Quaternion.identity);
    
    public static void InstantiatePrefab(PrefabReference prefab, Transform target, Vector3 position) => InstantiatePrefab(prefab, target, position, Quaternion.identity);
    
    public static void InstantiatePrefab(PrefabReference prefab, Transform target, Quaternion rotation) => InstantiatePrefab(prefab, target, Vector3.zero, rotation);
    
    public static void InstantiatePrefab(PrefabReference prefabRef, Transform? target, Vector3 position,
        Quaternion rotation)
    {
        if (!IsServer && !IsSinglePlayer)
        {
            Logging.Warn("Only the host may instantiate prefabs!");
            return;
        }
        
        var networkId = RepoSteamNetworkManager.Instance.NewNetworkId;
        var packet = new InstantiateNetworkedPrefabPacket
        {
            NetworkId = networkId,
            Prefab = prefabRef,
            Position = position,
            Rotation = rotation
        };
        
        packet.SetTargetTransform(target);
        
        SendPacket(packet);
    }

    /// <summary>
    /// Registers an AssetBundle containing prefabs to be used within the networked environment.
    /// </summary>
    /// <param name="assetBundle">The AssetBundle containing the prefabs to register.</param>
    /// <param name="modGuid">The GUID of the mod that the AssetBundle belongs to. If left unset, the GUID will be automatically detected based on the calling assembly.</param>
    /// <param name="bundleName">The reference name for the AssetBundle. Defaults to the name property of the AssetBundle if left unset.</param>
    /// <returns>An AssetBundleReference representing the registered AssetBundle.</returns>
    public static AssetBundleReference RegisterAssetBundle(AssetBundle assetBundle, string modGuid = "", string bundleName = "")
    {
        // Set bundleName to the name of assetBundle if not already set.
        if (string.IsNullOrWhiteSpace(bundleName))
            bundleName = assetBundle.name;

        // Try and get the modGuid from the calling assembly.
        if (string.IsNullOrWhiteSpace(modGuid))
        {
            var pluginInfo = Assembly.GetCallingAssembly()
                .GetPluginInfoFromAssembly();

            if (pluginInfo is null)
            {
                Logging.Error($"Failed to register AssetBundle {bundleName}!\n\tPlease pass in your mods GUID with the `modGuid` parameter!");
                return default;
            }
            
            modGuid = pluginInfo.GUID;
        }
        
        return NetworkAssetDatabase.RegisterAssetBundle(assetBundle, modGuid, bundleName, false);
    }
}
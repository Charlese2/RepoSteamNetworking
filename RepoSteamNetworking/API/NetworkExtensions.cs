using System.Collections.Generic;
using System.Linq;
using RepoSteamNetworking.Networking;
using RepoSteamNetworking.Utils;
using Steamworks;

namespace RepoSteamNetworking.API;

/// <summary>
/// Provides extension methods for retrieving Steam IDs, PlayerAvatars, and sending network packets to players or groups of players.
/// </summary>
public static class NetworkExtensions
{
    /// <summary>
    /// Sends a network packet to a specific Steam ID.
    /// </summary>
    /// <typeparam name="TPacket">The type of the network packet to be sent. Must inherit from the NetworkPacket class.</typeparam>
    /// <param name="steamId">The Steam ID of the target to which the packet will be sent.</param>
    /// <param name="packet">The network packet to be sent to the specified Steam ID.</param>
    public static void SendPacket<TPacket>(this SteamId steamId, TPacket packet)
        where TPacket : NetworkPacket
    {
        packet.Header.Target = steamId;
        RepoSteamNetwork.SendPacket(packet, NetworkDestination.PacketTarget);
    }

    /// <summary>
    /// Sends a network packet to a collection of Steam IDs.
    /// </summary>
    /// <typeparam name="TPacket">The type of the network packet to be sent. Must inherit from the NetworkPacket class.</typeparam>
    /// <param name="steamIds">The collection of Steam IDs representing the targets to which the packet will be sent.</param>
    /// <param name="packet">The network packet to be sent to the specified Steam IDs.</param>
    public static void SendPacket<TPacket>(this IEnumerable<SteamId> steamIds, TPacket packet)
        where TPacket : NetworkPacket
    {
        foreach (var steamId in steamIds)
            steamId.SendPacket(packet);
    }
}
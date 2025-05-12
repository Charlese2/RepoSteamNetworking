using System.Collections.Generic;
using System.Linq;
using RepoSteamNetworking.API;
using Steamworks;

namespace RepoSteamNetworking.Utils;

internal static class SteamIdUtils
{
    private static readonly Dictionary<ulong, string> CachedSteamIds = new();
    
    public static string GetLobbyName(this SteamId steamId)
    {
        if (CachedSteamIds.TryGetValue(steamId, out var cachedName))
            return cachedName;

        if (!steamId.IsValid)
            return "Invalid SteamId";
        
        var lobby = RepoSteamNetwork.GetCurrentLobby();

        var lobbyMembers = (IEnumerable<Friend>)lobby.Members;

        if (!lobbyMembers.Any(friend => friend.Id == steamId))
        {
            Logging.Warn($"Requesting name from non lobby member {steamId}");
            return "No Member for steamId";
        }

        if (SteamFriends.RequestUserInformation(steamId))
        {
            var tempName = $"SteamId: {steamId.Value}";
            CachedSteamIds[steamId] = tempName;
            return tempName;
        }

        // Use NameHistory so we don't accidentally dox people who use nicknames to refer to their friends real names.
        var names = lobbyMembers.Where(friend => friend.Id == steamId)
            .Single().NameHistory as IEnumerable<string>;

        if (names.Count() <= 0)
            return $"SteamId: {steamId.Value}";
        
        var name = names.First();
        if (string.IsNullOrEmpty(name))
            return $"SteamId: {steamId.Value}";
        
        CachedSteamIds[steamId] = name;
        
        return name;
    }

    internal static void OnPersonaStateChange(Friend friend)
    {
        var nameHistory = (IEnumerable<string>)friend.NameHistory;
        CachedSteamIds[friend.Id] = nameHistory.First();
    }
}
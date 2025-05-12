using System.Diagnostics.CodeAnalysis;
using BepInEx.Logging;
using HarmonyLib;
using RepoSteamNetworking.Networking.Unity;
using RepoSteamNetworking.Utils;
using SteamSample;
using Steamworks;
using Steamworks.Data;

namespace RepoSteamNetworking.Patches;

public static class SteamManagerPatches
{
    [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Start))]
    public static class OnStartPatch
    {
        // ReSharper disable once InconsistentNaming
        public static void Postfix(SteamManager __instance)
        {
            try
            {
                RepoSteamNetworkManager.CreateSingleton(__instance.gameObject);
                RepoNetworkingServer.CreateSingleton(__instance.gameObject);
                RepoNetworkingClient.CreateSingleton(__instance.gameObject);
            }
            catch (System.Exception e)
            {
                Logging.Error(e);
            }

        }
    }

    [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.HostGame), [typeof(bool)])]
    public static class OnJoinGamePatch
    {
        // ReSharper disable once InconsistentNaming
        [SuppressMessage("Method Declaration", "Harmony003:Harmony non-ref patch parameters modified")]
        public static void Postfix(SteamManager __instance)
        {
            if (__instance.activeLobby.HasValue)
            {
                RepoNetworkingServer.Instance.StartSocketServer(__instance.activeLobby.Value);
            }
            else
            {
                Logging.Error("No active lobby steam lobby after hosting game.");
            }

        }
    }

    [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.JoinGame), [])]
    public static class OnLobbyEnteredPatch
    {
        // ReSharper disable once InconsistentNaming
        [SuppressMessage("Method Declaration", "Harmony003:Harmony non-ref patch parameters modified")]
        public static void Postfix(SteamManager __instance)
        {
            if (__instance.activeLobby.HasValue)
            {
                RepoNetworkingServer.Instance.StartSocketServer(__instance.activeLobby.Value);
            }
            else
            {
                Logging.Error("No active lobby steam lobby after joining game.");
            }
        }
    }
    
    [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Disconnect))]
    public static class OnLeaveLobbyPatch
    {
        // ReSharper disable once InconsistentNaming
        [SuppressMessage("Method Declaration", "Harmony003:Harmony non-ref patch parameters modified")]
        public static void Prefix(SteamManager __instance)
        {
            if (__instance.activeLobby.HasValue && __instance.activeLobby.Value.IsOwnedBy(SteamClient.SteamId))
                RepoNetworkingServer.Instance.StopSocketServer();
            
            RepoNetworkingClient.Instance.DisconnectFromServer();
        }
    }
}
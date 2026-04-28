using System.Collections;
using FishNet;
using FishNet.Managing;
using UnityEngine;

public sealed class ServerAutoStart : MonoBehaviour
{
    private static bool s_startRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartHeadlessServerAfterSceneLoad()
    {
        if (!Application.isBatchMode || s_startRequested)
        {
            return;
        }

        s_startRequested = true;
        GameObject runner = new GameObject("ServerAutoStart");
        DontDestroyOnLoad(runner);
        runner.AddComponent<ServerAutoStart>();
    }

    private IEnumerator Start()
    {
        yield return null;

        NetworkManager networkManager = InstanceFinder.NetworkManager != null
            ? InstanceFinder.NetworkManager
            : FindFirstObjectByType<NetworkManager>();

        if (networkManager == null)
        {
            Debug.LogError("[Server] Headless mode detected, but FishNet NetworkManager was not found.");
            yield break;
        }

        if (networkManager.IsServerStarted)
        {
            Debug.Log("[Server] FishNet server is already started.");
            yield break;
        }

        Debug.Log("[Server] Headless mode detected. Starting FishNet server...");
        networkManager.ServerManager.StartConnection();
    }
}

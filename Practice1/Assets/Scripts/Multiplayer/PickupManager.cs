using System.Collections;
using FishNet;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

public class PickupManager : MonoBehaviour
{
    [SerializeField] private GameObject _healthPickupPrefab;
    [SerializeField] private float _defaultRespawnDelay = 10f;

    private NetworkManager _networkManager;
    private bool _hasSpawnedInitialPickups;

    public GameObject HealthPickupPrefab => _healthPickupPrefab;

    private void Awake()
    {
        CacheNetworkManager();
    }

    private void OnEnable()
    {
        CacheNetworkManager();
        RegisterCallbacks();

        if (_networkManager != null && _networkManager.IsServerStarted)
        {
            HandleServerStarted();
        }
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
        StopAllCoroutines();
    }

    public void NotifyPickedUp(Vector3 position, float respawnDelay)
    {
        if (_networkManager == null || !_networkManager.IsServerStarted)
        {
            return;
        }

        float delay = respawnDelay > 0f ? respawnDelay : _defaultRespawnDelay;
        StartCoroutine(RespawnAfterDelay(position, delay));
    }

    private void HandleServerStarted()
    {
        if (_hasSpawnedInitialPickups)
        {
            return;
        }

        SpawnAll();
        _hasSpawnedInitialPickups = true;
    }

    private void HandleServerStopped(bool _)
    {
        _hasSpawnedInitialPickups = false;
        StopAllCoroutines();
    }

    private void SpawnAll()
    {
        for (int i = 0; i < PickupSpawnPoint.Count; i++)
        {
            PickupSpawnPoint spawnPoint = PickupSpawnPoint.GetByIndex(i);
            if (spawnPoint != null)
            {
                SpawnPickup(spawnPoint.transform.position);
            }
        }
    }

    private IEnumerator RespawnAfterDelay(Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_networkManager == null || !_networkManager.IsServerStarted)
        {
            yield break;
        }

        SpawnPickup(position);
    }

    private void SpawnPickup(Vector3 position)
    {
        if (_healthPickupPrefab == null)
        {
            return;
        }

        GameObject pickupObject = Instantiate(_healthPickupPrefab, position, Quaternion.identity);
        HealthPickup pickup = pickupObject.GetComponent<HealthPickup>();
        pickup?.Initialize(this, position);

        _networkManager.ServerManager.Spawn(pickupObject);
    }

    private void RegisterCallbacks()
    {
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
        _networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;
    }

    private void UnregisterCallbacks()
    {
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
    }

    private void CacheNetworkManager()
    {
        _networkManager = InstanceFinder.NetworkManager != null
            ? InstanceFinder.NetworkManager
            : FindFirstObjectByType<NetworkManager>();
    }

    private void HandleServerConnectionState(FishNet.Transporting.ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started)
        {
            HandleServerStarted();
        }
        else if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped)
        {
            HandleServerStopped(false);
        }
    }
}

using System.Collections;
using Unity.Netcode;
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

        if (_networkManager != null && _networkManager.IsServer)
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
        if (_networkManager == null || !_networkManager.IsServer)
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

        if (_networkManager == null || !_networkManager.IsServer)
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

        NetworkObject networkObject = pickupObject.GetComponent<NetworkObject>();
        networkObject.Spawn();
    }

    private void RegisterCallbacks()
    {
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.OnServerStarted -= HandleServerStarted;
        _networkManager.OnServerStarted += HandleServerStarted;
        _networkManager.OnServerStopped -= HandleServerStopped;
        _networkManager.OnServerStopped += HandleServerStopped;
    }

    private void UnregisterCallbacks()
    {
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.OnServerStarted -= HandleServerStarted;
        _networkManager.OnServerStopped -= HandleServerStopped;
    }

    private void CacheNetworkManager()
    {
        _networkManager = NetworkManager.Singleton != null
            ? NetworkManager.Singleton
            : FindFirstObjectByType<NetworkManager>();
    }
}
